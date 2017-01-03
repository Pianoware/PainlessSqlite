using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Dapper;
using SQLinq;
using SQLinq.Dapper;
using System.Linq.Expressions;
using System.Reflection;

namespace Pianoware.PainlessSqlite
{

	// Similar to DbSet
	public sealed class SqliteSet<TModel>: IEnumerable<TModel> where TModel : new()
	{
		SQLiteConnection connection;
		SqlinqOperation[] sqlinqOperations;
		string tableName; 
		//SQLinq<TModel> sqlinq;

		// Hold on to the connection and model info
		SqliteSet (SQLiteConnection connection, string tableName) 
			: this(connection, tableName, new SqlinqOperation[0]) { }

		// Sqlinq integration
		SqliteSet (SQLiteConnection connection, string tableName, SqlinqOperation[] sqlinqOperations)
		{
			this.connection = connection;
			this.tableName = tableName;
			//this.sqlinq = sqlinq ?? new SQLinq<TModel>(tableName);
			this.sqlinqOperations = sqlinqOperations;
		}

		// Add
		public TModel Add(TModel obj)
		{
			var variables = typeof(TModel).GetVariables().Where(v => !string.Equals("Id", v.Name, StringComparison.OrdinalIgnoreCase));
			// Todo parameterize
			var command = new SQLiteCommand($"INSERT INTO \"{tableName}\" ({string.Join(",", variables.Select(v => $"\"{v.Name}\""))}) VALUES ({string.Join(",", variables.Select(v => $"@{v.Name}"))})", connection);
			command.Parameters.AddRange(variables.Select(v => new SQLiteParameter { ParameterName = v.Name, Value = v.GetValue(obj) }).ToArray());
			long newId;

			// To get last rowid, lock on connection
			var idVariable = typeof(TModel).GetVariable("Id");
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

		// Implementation for IEnumerable 
		public IEnumerator<TModel> GetEnumerator()
		{
			// Instantiate sqlinq
			var sqlinq = new SQLinq<TModel>(tableName);

			// Run all operations
			foreach (var operation in sqlinqOperations)
			{
				operation.Run(sqlinq);
			}

			return connection.Query(sqlinq).GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}


		// SQLinq operations
		public SqliteSet<TModel> Distinct(bool distinct = true) =>
			AppendOperation("Distinct", distinct);

		public SqliteSet<TModel> OrderBy(Expression<Func<TModel, object>> keySelector) =>
			AppendOperation("OrderBy", keySelector);

		public SqliteSet<TModel> OrderByDescending(Expression<Func<TModel, object>> keySelector) =>
			AppendOperation("OrderByDescending", keySelector);

		public SqliteSet<TModel> Skip(int skip) =>
			AppendOperation("Skip", skip);

		public SqliteSet<TModel> Take(int take) =>
			AppendOperation("Take", take);

		public SqliteSet<TModel> ThenBy(Expression<Func<TModel, object>> keySelector) =>
			AppendOperation("ThenBy", keySelector);

		public SqliteSet<TModel> ThenByDescending(Expression<Func<TModel, object>> keySelector) =>
			AppendOperation("ThenByDescending", keySelector);

		public SqliteSet<TModel> Where(Expression expression) =>
			AppendOperation("Where1", expression);

		public SqliteSet<TModel> Where(Expression<Func<TModel, bool>> expression) =>
			AppendOperation("Where2", expression);

		SqliteSet<TModel> AppendOperation(string method, params object[] parameters)
		{
			return new SqliteSet<TModel>(connection, tableName,
				sqlinqOperations.Union(new[] { new SqlinqOperation(method, parameters) }).ToArray());
		}

		class SqlinqOperation
		{
			static Dictionary<string, MethodInfo> methods = new Dictionary<string, MethodInfo>
			{
				{ "Distinct", typeof(SQLinq<TModel>).GetMethod("Distinct") },
				{ "OrderBy", typeof(SQLinq<TModel>).GetMethod("OrderBy") },
				{ "OrderByDescending", typeof(SQLinq<TModel>).GetMethod("OrderByDescending") },
				{ "Skip", typeof(SQLinq<TModel>).GetMethod("Skip") },
				{ "Take", typeof(SQLinq<TModel>).GetMethod("Take") },
				{ "ThenBy", typeof(SQLinq<TModel>).GetMethod("ThenBy") },
				{ "ThenByDescending", typeof(SQLinq<TModel>).GetMethod("ThenByDescending") },
				{ "Where1", typeof(SQLinq<TModel>).GetMethod("Where", new[] { typeof(Expression<Func<TModel, bool>>) }) },
				{ "Where2", typeof(SQLinq<TModel>).GetMethod("Where", new[] {typeof(Expression) }) }
			};

			MethodInfo Method { get; }
			object[] Parameters { get; }

			public SqlinqOperation(string method, params object[] parameters)
			{
				this.Method = methods[method];
				this.Parameters = parameters;
			}

			public void Run(SQLinq<TModel> sqlinq)
			{
				Method.Invoke(sqlinq, Parameters);
			}
		}


		public override string ToString()
		{
			// Todo: Deduplicate

			// Instantiate sqlinq
			var sqlinq = new SQLinq<TModel>(tableName);

			// Run all operations
			foreach (var operation in sqlinqOperations)
			{
				operation.Run(sqlinq);
			}

			// Get query text
			return sqlinq.ToSQL().ToQuery();
		}
	}
}
