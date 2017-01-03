using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Dapper;
using SQLinq;
using SQLinq.Dapper;
using System.Linq.Expressions;

namespace Pianoware.PainlessSqlite
{
	// Similar to DbSet
	public sealed class SqliteSet<TModel>: IEnumerable<TModel> where TModel : new()
	{
		SQLiteConnection connection;
		string tableName; 
		SQLinq<TModel> sqlinq;

		// Hold on to the connection and model info
		SqliteSet (SQLiteConnection connection, string tableName) 
			: this(connection, tableName, null)
		{
		}

		// Sqlinq integration
		SqliteSet (SQLiteConnection connection, string tableName, SQLinq<TModel> sqlinq)
		{
			this.connection = connection;
			this.tableName = tableName;
			this.sqlinq = sqlinq ?? new SQLinq<TModel>(tableName);
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
			return connection.Query(sqlinq).GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}


		// SQLinq operations
		public SqliteSet<TModel> Distinct(bool distinct = true) =>
			new SqliteSet<TModel>(connection, tableName, sqlinq.Distinct(distinct));

		public SqliteSet<TModel> OrderBy(Expression<Func<TModel, object>> keySelector) =>
			new SqliteSet<TModel>(connection, tableName, sqlinq.OrderBy(keySelector));

		public SqliteSet<TModel> OrderByDescending(Expression<Func<TModel, object>> keySelector) =>
			new SqliteSet<TModel>(connection, tableName, sqlinq.OrderByDescending(keySelector));

		public SqliteSet<TModel> Skip(int skip) =>
			new SqliteSet<TModel>(connection, tableName, sqlinq.Skip(skip));

		public SqliteSet<TModel> Take(int take) =>
			new SqliteSet<TModel>(connection, tableName, sqlinq.Take(take));

		public SqliteSet<TModel> ThenBy(Expression<Func<TModel, object>> keySelector) =>
			new SqliteSet<TModel>(connection, tableName, sqlinq.ThenBy(keySelector));

		public SqliteSet<TModel> ThenByDescending(Expression<Func<TModel, object>> keySelector) =>
			new SqliteSet<TModel>(connection, tableName, sqlinq.ThenByDescending(keySelector));

		public SqliteSet<TModel> Where(Expression expression) =>
			new SqliteSet<TModel>(connection, tableName, sqlinq.Where(expression));

		public SqliteSet<TModel> Where(Expression<Func<TModel, bool>> expression) =>
			new SqliteSet<TModel>(connection, tableName, sqlinq.Where(expression));


		public override string ToString()
		{
			return sqlinq.ToSQL().ToQuery();
		}
	}
}
