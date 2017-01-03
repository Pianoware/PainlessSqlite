using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
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
		//TableInfo tableInfo;
		string tableName; //, insertQueryWithId, insertQueryWithoutId, selectQuery;
		//string[] columnNames, columnNamesWithoutId;
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
			//this.tableName = tableInfo.Name;
			this.sqlinq = sqlinq ?? new SQLinq<TModel>(tableName);

			//columnNames = tableInfo.Columns.Select(c => c.Name).ToArray();
			//var columnList = string.Join(", ", columnNames.Select(c => $"\"{c}\""));

			//// Select query template
			//selectQuery = $"SELECT {columnList} FROM \"{tableName}\" ";

			//// Insert query template
			//insertQueryWithId = $"INSERT INTO \"{tableName}\" ({columnList}) " +
			//	$"VALUES ({string.Join(", ", Enumerable.Range(0, columnNames.Length).Select(i => "?")) })";

			//// Insert without Id (auto-incremented)
			//columnNamesWithoutId = columnNames.Where(c => !string.Equals("Id", c, StringComparison.OrdinalIgnoreCase)).ToArray();
			//var columnListWithoutId = string.Join(", ", columnNamesWithoutId.Select(c => $"\"{c}\""));
			//insertQueryWithoutId = $"INSERT INTO \"{tableName}\" ({columnListWithoutId}) " +
			//	$"VALUES ({string.Join(", ", Enumerable.Range(0, columnNamesWithoutId.Length).Select(i => "?")) })";
		}

		// Iterate over table and return model objects
		//[Obsolete]
		//IEnumerable<TModel> Select(string where = null, string groupBy = null, string having = null, string orderBy = null, int limit = -1, int offset = -1)
		//{
		//	var query = new StringBuilder(selectQuery);

		//	#region Clauses
		//	if (!string.IsNullOrWhiteSpace(where))
		//		query.Append($" WHERE {where} ");

		//	if (!string.IsNullOrWhiteSpace(groupBy))
		//		query.Append($" GROUP BY {groupBy} ");

		//	if (!string.IsNullOrWhiteSpace(having))
		//		query.Append($" HAVING {having} ");

		//	if (!string.IsNullOrWhiteSpace(orderBy))
		//		query.Append($" ORDER BY {orderBy} ");

		//	if (limit >= 0)
		//		query.Append($" LIMIT {limit} ");

		//	if (offset >= 0)
		//		query.Append($" OFFSET {offset} ");

		//	#endregion

		//	var command = new SQLiteCommand(query.ToString(), connection);

		//	// Using Dapper as mapper!
		//	return connection.Query<TModel>(query.ToString());
		//	// return command.ExecuteQuery<TModel>();
		//}

		// Add
		public TModel Add(TModel obj)
		{
			var variables = typeof(TModel).GetVariables().Where(v => !string.Equals("Id", v.Name, StringComparison.OrdinalIgnoreCase));
			// Todo parameterize
			var command = new SQLiteCommand($"INSERT INTO \"{tableName}\" ({string.Join(",", variables.Select(v => $"\"{v.Name}\""))}) VALUES ({string.Join(",", variables.Select(v => $"@{v.Name}"))})", connection);
			command.Parameters.AddRange(variables.Select(v => new SQLiteParameter { ParameterName = v.Name, Value = v.GetValue(obj) }).ToArray());
			long newId;
			
			// To get last rowid, lock on connection
			// Todo: don't bother for models that don't have Id variable
			lock (connection)
			{
				command.ExecuteNonQuery();
				command = new SQLiteCommand("SELECT last_insert_rowid()", connection);
				newId = (long)command.ExecuteScalar();
			}

			var idVariable = typeof(TModel).GetVariable("Id");
			idVariable?.SetValue(obj, newId);
			return obj;
		}

		// Implementation for IEnumerable 
		public IEnumerator<TModel> GetEnumerator()
		{
			return connection.Query(sqlinq).GetEnumerator();
			//return Select().GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
			//return Select().GetEnumerator();
		}


		// SQLinq operations
		public SqliteSet<TModel> Distinct(bool distinct = true) =>
			new SqliteSet<TModel>(connection, tableName, sqlinq.Distinct(distinct));

		public SqliteSet<TModel> OrderBy(Expression<Func<TModel, object>> keySelector) =>
			new SqliteSet<TModel>(connection, tableName, sqlinq.OrderBy(keySelector));

		public SqliteSet<TModel> OrderByDescending(Expression<Func<TModel, object>> keySelector) =>
			new SqliteSet<TModel>(connection, tableName, sqlinq.OrderByDescending(keySelector));

		// public SQLinq<T> Select(Expression<Func<T, object>> selector);

		public SqliteSet<TModel> Skip(int skip) =>
			new SqliteSet<TModel>(connection, tableName, sqlinq.Skip(skip));

		public SqliteSet<TModel> Take(int take) =>
			new SqliteSet<TModel>(connection, tableName, sqlinq.Take(take));

		public SqliteSet<TModel> ThenBy(Expression<Func<TModel, object>> keySelector) =>
			new SqliteSet<TModel>(connection, tableName, sqlinq.ThenBy(keySelector));

		public SqliteSet<TModel> ThenByDescending(Expression<Func<TModel, object>> keySelector) =>
			new SqliteSet<TModel>(connection, tableName, sqlinq.ThenByDescending(keySelector));

		// public SQLinqResult ToSQL(int existingParameterCount = 0, string parameterNamePrefix = "sqlinq_");

		public SqliteSet<TModel> Where(Expression expression) =>
			new SqliteSet<TModel>(connection, tableName, sqlinq.Where(expression));

		public SqliteSet<TModel> Where(Expression<Func<TModel, bool>> expression) =>
			new SqliteSet<TModel>(connection, tableName, sqlinq.Where(expression));

	}
}
