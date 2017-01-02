using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;

namespace Pianoware.PainlessSqlite
{
	static class DatabaseHelper
	{
		// Cache and write lock variables
		readonly static ConcurrentDictionary<DatabaseId, DatabaseInfo> databaseInfoCache = new ConcurrentDictionary<DatabaseId, DatabaseInfo>();

		// Get database info, distinguishing connections by their connection string
		// Todo: special case of :memory: databases to distinguish same or different :memory: dbs
		static DatabaseInfo GetDatabaseInfo(SQLiteConnection connection, bool useCache = true)
		{
			var databaseId = GetDatabaseId(connection);
			if (useCache && databaseInfoCache.ContainsKey(databaseId))
				return databaseInfoCache[databaseId];

			// Ok not to lock nor double-check conditions
			// Proceed

			// Don't use cache, scan database
			var databaseInfo = ScanDatabase(connection);

			// Update cache
			databaseInfoCache[databaseId] = databaseInfo;

			// Return
			return databaseInfo;
		}

		// Scan database
		static DatabaseInfo ScanDatabase(SQLiteConnection connection)
		{
			// Get tables
			var command = new SQLiteCommand("SELECT Name FROM sqlite_master WHERE Type = 'table' AND Name NOT LIKE 'sqlite_%'", connection);
			var tableNames = command.ExecuteQuery<string>().ToArray();

			var tables = new TableInfo[tableNames.Length];
			for (int i = 0; i < tables.Length; i++)
			{
				// Prepare table info
				var tableName = tableNames[i];
				var tableInfo = new TableInfo { Name = tableName };

				// Get columns
				command = new SQLiteCommand($"SELECT * FROM \"{tableName}\" LIMIT 0", connection);
				ColumnInfo[] columns = null;
				using (var reader = command.ExecuteReader())
				{
					columns = Enumerable.Range(0, reader.FieldCount)
						.Select(columnIndex => new ColumnInfo { Name = reader.GetName(columnIndex) }).ToArray();
				}

				tableInfo.Columns = columns;
				tables[i] = tableInfo;
			}

			// Prepare database info
			var databaseInfo = new DatabaseInfo
			{
				Id = GetDatabaseId(connection),
				Tables = tables
			};

			return databaseInfo;
		}

		// Update database based on a desired schema
		readonly static HashSet<Tuple<DatabaseId, ContextInfo>> updatedDatabases = new HashSet<Tuple<DatabaseId, ContextInfo>>();
		readonly static ConcurrentDictionary<DatabaseId, object> databaseLocks = new ConcurrentDictionary<DatabaseId, object>();
		internal static void UpdateDatabase(SQLiteConnection connection, ContextInfo contextInfo, bool forceUpdate = false)
		{
			// Shortcut if this database has already been visited and an update is not being forced
			var databaseId = GetDatabaseId(connection);
			var databaseContextPair = new Tuple<DatabaseId, ContextInfo>(databaseId, contextInfo);
			if (!forceUpdate && updatedDatabases.Contains(databaseContextPair))
				return;

			object databaseLock;
			if (databaseLocks.ContainsKey(databaseId))
				databaseLock = databaseLocks[databaseId];
			else
			{
				lock (databaseLocks)
				{
					// Check conditions again 
					if (databaseLocks.ContainsKey(databaseId))
					{
						databaseLock = databaseLocks[databaseId];
					}
					else
					{
						databaseLock = new object();
						databaseLocks[databaseId] = databaseLock;
					}
				}
			}

			// Synchronize database updates globally
			lock (databaseLock)
			{
				// Get the latest schema
				var currentSchema = GetDatabaseInfo(connection, useCache: false);

				// Iterate through desired tables
				var databaseWasUpdated = false;
				foreach (var desiredTable in contextInfo.Sets.Select(s => s.TableInfo))
				{
					// Does a table with matching name already exist?
					var existingTable = currentSchema.Tables.SingleOrDefault(t => t.Name == desiredTable.Name);

					if (existingTable == null)
					{
						// Table with matching name doesn't exist. Create.
						databaseWasUpdated = true;

						// Leave out Id from model fields
						var columns = string.Join(", ",
							desiredTable.Columns.Where(c => !"Id".Equals(c.Name, StringComparison.OrdinalIgnoreCase))
							.Select(c => $"\"{c.Name}\""));

						// Prepend comma if there are any columns
						if (!string.IsNullOrWhiteSpace(columns))
							columns = ", " + columns;

						// Add Id as a special column (integer primary key autoincrement)
						var command = new SQLiteCommand($"CREATE TABLE \"{desiredTable.Name}\" (Id INTEGER PRIMARY KEY AUTOINCREMENT {columns})", connection);
						command.ExecuteNonQuery();
					}
					else
					{
						// Table exists. Check columns
						var missingColumns = desiredTable.Columns
							.Where(desiredColumn => !existingTable.Columns
								.Any(existingColumn => string.Equals(existingColumn.Name, desiredColumn.Name, StringComparison.OrdinalIgnoreCase)));

						foreach (var column in missingColumns)
						{
							databaseWasUpdated = true;
							var command = new SQLiteCommand($"ALTER TABLE \"{desiredTable.Name}\" ADD COLUMN \"{column.Name}\"", connection);
							command.ExecuteNonQuery();
						}
					}
				}

				// If any changes were made, refresh database info
				if (databaseWasUpdated)
					GetDatabaseInfo(connection, useCache: false);

				// Remmeber that this database has been updated
				updatedDatabases.Add(databaseContextPair);
			}
		}


		static readonly ConcurrentDictionary<SQLiteConnection, DatabaseId> idsByConnection = new ConcurrentDictionary<SQLiteConnection, DatabaseId>();
		static readonly ConcurrentDictionary<string, DatabaseId> idsByConnectionString = new ConcurrentDictionary<string, DatabaseId>();
		static DatabaseId GetDatabaseId(SQLiteConnection connection)
		{
			// Seen this connection?
			if (idsByConnection.ContainsKey(connection))
				return idsByConnection[connection];

			// Seen this connection string?
			var connectionString = connection.ConnectionString;
			if (idsByConnectionString.ContainsKey(connectionString))
				return idsByConnectionString[connectionString];

			// Create appropriate Id
			if (IsMemoryDatabase(connection))
			{
				// Memory databases
				lock (idsByConnection)
				{
					// Check conditions again
					if (idsByConnection.ContainsKey(connection))
						return idsByConnection[connection];

					var id = new DatabaseId();
					idsByConnection[connection] = id;
					return id;
				}
			}
			else
			{
				// File database
				lock (idsByConnectionString)
				{
					// Check conditions again
					if (idsByConnectionString.ContainsKey(connectionString))
						return idsByConnectionString[connectionString];

					var id = new DatabaseId();
					idsByConnectionString[connectionString] = id;
					return id;
				}
			}
		}


		static readonly string memorySearchStringEnd = "datasource=:memory:";
		static readonly string memorySearchStringAnywhere = "datasource=:memory:;";
		static bool IsMemoryDatabase(SQLiteConnection connection)
		{
			var connectionString = connection.ConnectionString;
			var transformedConnectionString = connectionString.Replace(" ", "").ToLowerInvariant();

			return transformedConnectionString.EndsWith(memorySearchStringEnd)
				|| transformedConnectionString.Contains(memorySearchStringAnywhere);
		}

	}
}
