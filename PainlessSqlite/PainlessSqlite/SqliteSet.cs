using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Pianoware.PainlessSqlite
{

	// Similar to DbSet
	public sealed class SqliteSet<TModel> : IEnumerable<TModel> where TModel : new()
	{
		readonly SQLiteConnection connection;
		readonly QueryOperations queryOperations;
		readonly string tableName;

		#pragma warning disable RECS0108 // Warns about static fields in generic types
		// These are static fields specific to each generic definition, not a mistake
		static readonly VariableInfo idVariable;
		static readonly VariableInfo[] dataVariables;
		#pragma warning restore RECS0108 // Warns about static fields in generic types

		// Static constructor
		static SqliteSet() {
			var type = typeof(TModel);
			var variables = type.GetVariables();
			idVariable = type.GetVariable("Id", true);
			dataVariables = variables.Where(v => v != idVariable).ToArray();
		}

		// Hold on to the connection and model info
		SqliteSet(SQLiteConnection connection, string tableName)
			: this(connection, tableName, new QueryOperations()) { }

		// Sqlinq integration
		SqliteSet(SQLiteConnection connection, string tableName, QueryOperations queryOperations)
		{
			this.connection = connection;
			this.tableName = tableName;
			this.queryOperations = queryOperations;
		}

		// Add
		public TModel Add(TModel obj)
		{
			var command = new SQLiteCommand($"INSERT INTO \"{tableName}\" ({string.Join(",", dataVariables.Select(v => $"\"{v.Name}\""))}) VALUES ({string.Join(",", dataVariables.Select(v => $"@{v.Name}"))})", connection);
			command.Parameters.AddRange(dataVariables.Select(v => new SQLiteParameter { ParameterName = v.Name, Value = v.GetValue(obj) }).ToArray());
			long newId;

			// To get last rowid, lock on connection
			if (idVariable != null)
			{
				// Only lock on connection if Model contains an Id variable
				lock (connection)
				{
					command.ExecuteNonQuery();
					command = new SQLiteCommand("SELECT last_insert_rowid()", connection);
					newId = (long)command.ExecuteScalar();
				}

				idVariable.SetValue(obj, newId);
			}
			else
			{
				command.ExecuteNonQuery();
			}

			return obj;
		}

		// Update 
		public void Update(TModel obj)
		{
			if (idVariable == null)
				throw new Exception($"Model does not contain an Id variable and cannot be updated or deleted");

			long id = (long)idVariable.GetValue(obj);

			var command = new SQLiteCommand($"UPDATE \"{tableName}\" SET {string.Join(",", dataVariables.Select(v => $"\"{v.Name}\" = @{v.Name}"))} WHERE Id = {id}", connection);
			command.Parameters.AddRange(dataVariables.Select(v => new SQLiteParameter { ParameterName = v.Name, Value = v.GetValue(obj) }).ToArray());
			command.ExecuteNonQuery();
		}

		// Delete
		public void Delete(TModel obj)
		{
			if (idVariable == null)
				throw new Exception($"Model does not contain an Id variable and cannot be updated or deleted");

			long id = (long)idVariable.GetValue(obj);
			var command = new SQLiteCommand($"DELETE FROM \"{tableName}\" WHERE Id = {id}", connection);
			command.ExecuteNonQuery();
		}

		// Implementation for IEnumerable 
		public IEnumerator<TModel> GetEnumerator()
		{
			var command = new SQLiteCommand(connection);
			var query = queryOperations.GetQuery(tableName);
			command.CommandText = query.QueryText;

			if (query.Parameters?.Length > 0)
				command.Parameters.AddRange(query.Parameters);

			return command.ExecuteQuery<TModel>().GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}


		// Selection operations
		public SqliteSet<TModel> Distinct(bool distinct = true) =>
			AppendOperation(queryOperations.SetDistinct(distinct));

		public SqliteSet<TModel> Where(Expression<Func<TModel, bool>> expression) =>
			AppendOperation(queryOperations.SetWhere(expression));

		public SqliteSet<TModel> OrderBy(Expression<Func<TModel, object>> keySelector) =>
			OrderBy(keySelector, ascending: true);

		public SqliteSet<TModel> OrderByDescending(Expression<Func<TModel, object>> keySelector) =>
			OrderBy(keySelector, ascending: false);

		public SqliteSet<TModel> ThenBy(Expression<Func<TModel, object>> keySelector) =>
			OrderBy(keySelector, ascending: true);

		public SqliteSet<TModel> ThenByDescending(Expression<Func<TModel, object>> keySelector) =>
			OrderBy(keySelector, ascending: false);

		public SqliteSet<TModel> Skip(int skip) =>
			AppendOperation(queryOperations.SetSkip(skip));

		public SqliteSet<TModel> Take(int take) =>
			AppendOperation(queryOperations.SetTake(take));

		// Order by function
		SqliteSet<TModel> OrderBy(Expression<Func<TModel, object>> keySelector, bool ascending)
		{
			var lambdaExpression = keySelector as LambdaExpression;
			MemberExpression lambdaBody = null;
			if (lambdaExpression.Body.NodeType == ExpressionType.Convert)
			{
				var convertExpression = lambdaExpression.Body as UnaryExpression;
				lambdaBody = convertExpression.Operand as MemberExpression;
			}
			else
			{
				lambdaBody = lambdaExpression?.Body as MemberExpression;
			}

			if (lambdaBody != null && (lambdaBody.Member is FieldInfo || lambdaBody.Member is PropertyInfo))
				return AppendOperation(queryOperations.AddOrderBy(new OrderBySegment { Column = lambdaBody.Member.Name, Ascending = ascending }));

			throw new Exception("Not a valid field or property expression: " + keySelector);
		}

		// Append query operation
		SqliteSet<TModel> AppendOperation(QueryOperations newQueryOperations)
		{
			return new SqliteSet<TModel>(connection, tableName, newQueryOperations);
		}

		class QueryOperations
		{
			public bool Distinct { get; private set; }
			public int? Skip { get; private set; }
			public int? Take { get; private set; }
			public Expression Where { get; private set; }
			public OrderBySegment[] OrderBy { get; private set; }

			public QueryOperations SetDistinct(bool distinct) =>
				new QueryOperations(this) { Distinct = distinct };

			public QueryOperations SetSkip(int? skip) =>
				new QueryOperations(this) { Skip = skip };

			public QueryOperations SetTake(int? take) =>
				new QueryOperations(this) { Take = take };

			public QueryOperations SetWhere(Expression where)
			{
				// One where clause per query
				if (Where != null)
					throw new Exception("Where clause already specified");

				var newQueryOperations = new QueryOperations(this);
				newQueryOperations.Where = where;
				return newQueryOperations;
			}

			public QueryOperations AddOrderBy(OrderBySegment orderBy)
			{
				var newQueryOperations = new QueryOperations(this);
				if (OrderBy?.Length > 0)
				{
					if (OrderBy.Any(o => o.Column == orderBy.Column))
						throw new Exception("Duplicate order for column " + orderBy.Column);
					newQueryOperations.OrderBy = OrderBy.Union(new OrderBySegment[] { orderBy }).ToArray();
				}
				else
					newQueryOperations.OrderBy = new OrderBySegment[] { orderBy };

				return newQueryOperations;
			}

			// Public constructor
			public QueryOperations()
			{ }

			QueryOperations(QueryOperations source)
			{
				Distinct = source.Distinct;
				Where = source.Where;
				OrderBy = source.OrderBy;
				Skip = source.Skip;
				Take = source.Take;
			}

			public Query GetQuery(string tableName)
			{
				var result = new Query();

				// Projection
				var queryBuilder = new StringBuilder("SELECT ");
				if (Distinct)
					queryBuilder.Append("DISTINCT ");

				queryBuilder.Append("* FROM \"");
				queryBuilder.Append(tableName);
				queryBuilder.Append("\" ");

				// Where
				if (Where != null)
				{
					var whereBuilder = new WhereQueryBuilder();
					var whereQuery = whereBuilder.BuildWhereQuery(Where as LambdaExpression);
					result.Parameters = whereQuery.Parameters.Select(p => new SQLiteParameter(p.Name, p.Value)).ToArray();
					queryBuilder.Append("WHERE (");
					queryBuilder.Append(whereQuery.Query);
					queryBuilder.Append(") ");
				}

				// Order by
				if (OrderBy?.Length > 0)
				{
					queryBuilder.Append("ORDER BY ");
					queryBuilder.Append(string.Join(", ", OrderBy.Select(o => $"\"{o.Column}\" " + (o.Ascending ? "ASC" : "DESC"))));
					queryBuilder.Append(" ");
				}

				// Skip (offset)
				if (Skip.HasValue)
				{
					queryBuilder.Append("OFFSET ");
					queryBuilder.Append(Skip.Value);
				}

				// Take (limit)
				if (Take.HasValue)
				{
					queryBuilder.Append("LIMIT ");
					queryBuilder.Append(Take.Value);
				}

				result.QueryText = queryBuilder.ToString();
				return result;
			}

			public override string ToString()
			{
				return base.ToString();
			}
		}

		class OrderBySegment
		{
			public bool Ascending = true;
			public string Column;
		}

		class Query
		{
			public string QueryText;
			public SQLiteParameter[] Parameters;
		}

		public override string ToString()
		{
			return queryOperations.GetQuery(tableName).QueryText;
		}
	}
}
