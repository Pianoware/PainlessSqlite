using System;
using System.Collections.Concurrent;
using System.Data;
using System.Data.SQLite;

namespace Pianoware.PainlessSqlite
{

	// Similar to DbContext
	public abstract class SqliteContext : IDisposable
	{
		SQLiteConnection connection;
		bool closeConnectionOnDispose;

		// Constructor with connection string
		protected SqliteContext(string connectionString)
			: this(new SQLiteConnection(connectionString), closeConnectionOnDispose: true) { }

		// Constructor with provided connection 
		protected SqliteContext(SQLiteConnection connection)
			: this(connection, closeConnectionOnDispose: false) { }

		// Constructor with singleton in-memory connection per context
		protected SqliteContext()
			: this(null, closeConnectionOnDispose: false) { }

		// Constructor with connection
		private SqliteContext(SQLiteConnection connection, bool closeConnectionOnDispose)
		{
			var contextType = GetType();

			// If no connection was provided, get the default
			if (connection == null)
				connection = GetDefaultConnection(contextType);

			// Open connection if not already open
			try
			{
				if (connection.State != ConnectionState.Open)
					connection.Open();
			}
			catch (Exception ex)
			{
				throw new Exception("Unable to open connection", ex);
			}

			this.connection = connection;
			this.closeConnectionOnDispose = closeConnectionOnDispose;

			// Populate set fields and properties
			var contextInfo = ContextHelper.GetContextInfo(contextType);

			// Update database
			DatabaseHelper.UpdateDatabase(connection, contextInfo);

			// Initialize Sets
			foreach (var setInfo in contextInfo.Sets)
			{
				// Initialize sets
				var setInstance = setInfo.Create(connection, setInfo.TableInfo);
				setInfo.SetMember.SetValue(this, setInstance);
			}
		}

		// Provide default in-memory connections for contexts
		static readonly ConcurrentDictionary<Type, SQLiteConnection> defaultConnections = new ConcurrentDictionary<Type, SQLiteConnection>();
		private static SQLiteConnection GetDefaultConnection(Type contextType)
		{
			if (defaultConnections.ContainsKey(contextType))
				return defaultConnections[contextType];

			lock (defaultConnections)
			{
				// Check conditions again
				if (defaultConnections.ContainsKey(contextType))
					return defaultConnections[contextType];

				var connection = new SQLiteConnection("Data Source = :memory:;");
				defaultConnections[contextType] = connection;
				return connection;
			}
		}

		public void Dispose()
		{
			if (closeConnectionOnDispose)
			{
				connection.Close();
			}
		}
	}
}