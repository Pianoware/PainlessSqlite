using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;

namespace Pianoware.PainlessSqlite
{
	public static class SqliteExtensions
	{
		// Basic types use simple conversion
		static Type[] basicTypes = new[] { typeof(string), typeof(int) };

		// Instantiate and populate object from reader
		static object CreateObject(Type type, SQLiteDataReader reader)
		{
			var instance = Activator.CreateInstance(type);
			foreach (var variable in type.GetVariables())
				variable.SetValue(instance, Convert.ChangeType(reader[variable.Name], variable.VariableType));

			return instance;
		}

		// Extension method to conveniently convert data reader to object
		public static IEnumerable ExecuteQuery(this SQLiteCommand command, Type type)
		{
			using (var reader = command.ExecuteReader())
			{
				if (basicTypes.Contains(type))
				{
					while (reader.Read())
						yield return reader[0];
				}

				else
				{
					while (reader.Read())
						yield return CreateObject(type, reader);
				}
			}
		}

		// Generic 
		public static IEnumerable<T> ExecuteQuery<T>(this SQLiteCommand command)
		{
			return ExecuteQuery(command, typeof(T)).Cast<T>();
		}
	}

}
