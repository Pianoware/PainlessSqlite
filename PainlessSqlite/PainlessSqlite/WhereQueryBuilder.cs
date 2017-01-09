using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Pianoware.PainlessSqlite
{
	class WhereQueryBuilder
	{
		static readonly Dictionary<ExpressionType, string> binaryOperators = new Dictionary<ExpressionType, string>
		{
			{ ExpressionType.AndAlso, "AND" },
			{ ExpressionType.OrElse, "OR" },
			{ ExpressionType.Equal, "=" },
			{ ExpressionType.NotEqual, "<>" },
			{ ExpressionType.GreaterThan, ">" },
			{ ExpressionType.GreaterThanOrEqual, ">=" },
			{ ExpressionType.LessThan, "<" },
			{ ExpressionType.LessThanOrEqual, "<=" },
		};

		public class QueryParameter
		{
			public string Name;
			public object Value;
		}

		QueryParameter AddParameter(List<QueryParameter> parameters, object value)
		{
			lock (parameters)
			{
				var index = parameters.Count;
				var name = "@param_" + index;
				var parameter = new QueryParameter { Name = name, Value = value };
				parameters.Add(parameter);
				return parameter;
			}
		}

		abstract class QuerySegment
		{
			public abstract override string ToString();
		}

		class StringSegment : QuerySegment
		{
			public string Text;
			public StringSegment(string text)
			{
				this.Text = text;
			}

			public override string ToString()
			{
				return Text;
			}
		}

		class ColumnSegment : QuerySegment
		{
			public MemberInfo MemberInfo;
			public ColumnSegment(MemberInfo memberInfo)
			{
				this.MemberInfo = memberInfo;
			}

			public override string ToString()
			{
				return $"\"{MemberInfo.Name}\"";
			}
		}

		class ParameterSegment : QuerySegment
		{
			public QueryParameter Parameter;
			public ParameterSegment(QueryParameter parameter)
			{
				this.Parameter = parameter;
			}

			public override string ToString()
			{
				return Parameter.Name;
			}
		}

		class NullSegment : QuerySegment
		{
			public override string ToString()
			{
				return "NULL";
			}
		}

		public class WhereQuery
		{
			public string Query;
			public QueryParameter[] Parameters;
		}

		public WhereQuery BuildWhereQuery(LambdaExpression expression)
		{
			var parameters = new List<QueryParameter>();
			var query = GetQuery(expression.Body, expression.Parameters[0], parameters);
			return new WhereQuery
			{
				Query = query.ToString(),
				Parameters = parameters.ToArray()
			};
		}


		QuerySegment GetQuery(Expression expression, ParameterExpression parameter, List<QueryParameter> parameters)
		{
			// Prepare parameters
			parameters = parameters ?? new List<QueryParameter>();

			// If expression doesn't reference parameter, then simply compile and calculate its value
			try
			{
				var evaluation = Expression.Lambda(expression).Compile().DynamicInvoke();
				if (evaluation == null) return new NullSegment();
				return new ParameterSegment(AddParameter(parameters, evaluation));
			}
			catch (InvalidOperationException) { } // Proceed to break down the expression
			catch (Exception e)
			{
				throw e;
			}

			switch (expression.NodeType)
			{
				// Conditional
				case ExpressionType.AndAlso:
				case ExpressionType.OrElse:

				// Comparison ops:
				case ExpressionType.Equal:
				case ExpressionType.NotEqual:
				case ExpressionType.GreaterThan:
				case ExpressionType.GreaterThanOrEqual:
				case ExpressionType.LessThan:
				case ExpressionType.LessThanOrEqual:
					{
						var binaryExpression = expression as BinaryExpression;
						var leftExpression = binaryExpression.Left;
						var rightExpression = binaryExpression.Right;
						var leftSegment = GetQuery(leftExpression, parameter, parameters);
						var rightSegment = GetQuery(rightExpression, parameter, parameters);

						// Special case for NULL equality (IS, IS NOT)
						if ((expression.NodeType == ExpressionType.Equal || expression.NodeType == ExpressionType.NotEqual)
							&& (leftSegment is NullSegment || rightSegment is NullSegment))
						{
							var nullOperator = expression.NodeType == ExpressionType.Equal ? "IS" : "IS NOT";

							if (leftSegment is NullSegment)
								return new StringSegment($"({rightSegment} {nullOperator} {leftSegment})");

							if (rightSegment is NullSegment)
								return new StringSegment($"({leftSegment} {nullOperator} {rightSegment})");
						}

						return new StringSegment($"(({leftSegment}) {binaryOperators[expression.NodeType]} ({rightSegment}))");
					}
				// Member Access
				case ExpressionType.MemberAccess:
					{
						var memberExpression = expression as MemberExpression;

						// Substitute column name only if member access is being performed on the provided parameter 
						if (memberExpression.Expression == parameter && (memberExpression.Member is FieldInfo || memberExpression.Member is PropertyInfo))
							return new ColumnSegment(memberExpression.Member);

						// Otherwise, try to evaluate
						break;
					}
				// Call
				case ExpressionType.Call:
					{
						var callExpression = expression as MethodCallExpression;
						if (callExpression.Method.DeclaringType == typeof(string))
						{
							switch (callExpression.Method.Name)
							{
								case "StartsWith":
								case "EndsWith":
								case "Contains":
									{
										var leftExpression = callExpression.Object;
										var rightExpression = callExpression.Arguments[0];
										var leftSegment = GetQuery(leftExpression, parameter, parameters);
										var rightSegment = GetQuery(rightExpression, parameter, parameters);

										// Make sure we have COLUMN LIKE STRING format
										if (leftSegment is ColumnSegment && rightSegment is ParameterSegment)
										{
											var likeParameter = (rightSegment as ParameterSegment).Parameter;
											if (likeParameter.Value is string)
											{
												switch (callExpression.Method.Name)
												{
													case "StartsWith":
														likeParameter.Value = likeParameter.Value + "%";
														break;
													case "EndsWith":
														likeParameter.Value = "%" + likeParameter.Value;
														break;
													case "Contains":
														likeParameter.Value = "%" + likeParameter.Value + "%";
														break;
												}

												return new StringSegment($"({leftSegment} LIKE {rightSegment})");
											}
										}
										break;
									}
								default:
									break;
							}
						}
						else if (callExpression.Method.DeclaringType == typeof(Enumerable))
						{
							if (callExpression.Method.Name == "Contains")
							{
								var list = GetQuery(callExpression.Arguments[0], parameter, parameters);
								var column = GetQuery(callExpression.Arguments[1], parameter, parameters);
								if (column is ColumnSegment && list is ParameterSegment)
								{
									return new StringSegment($"({column} IN {list})");
								}
							}
						}

						break;
					}
				// Convert
				case ExpressionType.Convert:
					{
						var convertExpression = expression as UnaryExpression;
						if (convertExpression.IsLiftedToNull)
							return GetQuery(convertExpression.Operand, parameter, parameters);

						break;
					}

				default:
					break;
			}
			throw new Exception("Unable to process expression " + expression);
		}
	}
}
