using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;

namespace Pianoware.PainlessSqlite
{
	// Similar to DbSet
	public sealed class SqliteSet<TModel>: IEnumerable<TModel>
	{
		SQLiteConnection connection;
		string tableName, insertQueryWithId, insertQueryWithoutId, selectQuery;
		string[] columnNames, columnNamesWithoutId;

		// Hold on to the connection and model info
		SqliteSet (SQLiteConnection connection, TableInfo tableInfo)
		{
			this.connection = connection;
			this.tableName = tableInfo.Name;

			columnNames = tableInfo.Columns.Select(c => c.Name).ToArray();
			var columnList = string.Join(", ", columnNames.Select(c => $"\"{c}\""));

			// Select query template
			selectQuery = $"SELECT {columnList} FROM \"{tableName}\" ";

			// Insert query template
			insertQueryWithId = $"INSERT INTO \"{tableName}\" ({columnList}) " +
				$"VALUES ({string.Join(", ", Enumerable.Range(0, columnNames.Length).Select(i => "?")) })";

			// Insert with Id
			columnNamesWithoutId = columnNames.Where(c => !string.Equals("Id", c, StringComparison.OrdinalIgnoreCase)).ToArray();
			var columnListWithoutId = string.Join(", ", columnNamesWithoutId.Select(c => $"\"{c}\""));

			// Insert without Id (auto-incremented)
			insertQueryWithoutId = $"INSERT INTO \"{tableName}\" ({columnListWithoutId}) " +
				$"VALUES ({string.Join(", ", Enumerable.Range(0, columnNamesWithoutId.Length).Select(i => "?")) })";
		}

		// Iterate over table and return model objects
		public IEnumerable<TModel> Select(string where = null, string groupBy = null, string having = null, string orderBy = null, int limit = -1, int offset = -1)
		{
			var query = new StringBuilder(selectQuery);

			#region Clauses
			if (!string.IsNullOrWhiteSpace(where))
				query.Append($" WHERE {where} ");

			if (!string.IsNullOrWhiteSpace(groupBy))
				query.Append($" GROUP BY {groupBy} ");

			if (!string.IsNullOrWhiteSpace(having))
				query.Append($" HAVING {having} ");

			if (!string.IsNullOrWhiteSpace(orderBy))
				query.Append($" ORDER BY {orderBy} ");

			if (limit >= 0)
				query.Append($" LIMIT {limit} ");

			if (offset >= 0)
				query.Append($" OFFSET {offset} ");

			#endregion

			var command = new SQLiteCommand(query.ToString(), connection);
			return command.ExecuteQuery<TModel>();
		}

		// Add
		public TModel Add(TModel obj)
		{
			var command = new SQLiteCommand(insertQueryWithoutId, connection);
			command.Parameters.AddRange(columnNamesWithoutId.Select(c => new SQLiteParameter { Value = typeof(TModel).GetVariable(c).GetValue(obj) }).ToArray());
			long newId;
			
			// To get last rowid, lock on connection
			// Todo: don't bother for models that don't have Id variable
			lock (connection)
			{
				command.ExecuteNonQuery();
				command = new SQLiteCommand("SELECT last_insert_rowid()", connection);
				newId = (long)command.ExecuteScalar();
			}

			typeof(TModel).GetVariable("Id")?.SetValue(obj, newId);
			return obj;
		}

		// Implementation for IEnumerable 
		public IEnumerator<TModel> GetEnumerator()
		{
			return Select().GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return Select().GetEnumerator();
		}
	}
}
