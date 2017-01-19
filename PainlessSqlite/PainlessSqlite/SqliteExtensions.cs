using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;

namespace Pianoware.PainlessSqlite
{
	// C# Built-in types: https://msdn.microsoft.com/en-us/library/ya5y69ds.aspx
	public static class SqliteExtensions
	{
		// Basic types use simple conversion
		static Type[] basicTypes = {
			typeof(bool),
			typeof(byte),
			typeof(sbyte),
			typeof(char),
			typeof(decimal),
			typeof(double),
			typeof(float),
			typeof(int),
			typeof(uint),
			typeof(long),
			typeof(ulong),
			typeof(object),
			typeof(short),
			typeof(ushort),
			typeof(string),
		};

		// Instantiate and populate object from reader
		static object CreateObject(Type type, SQLiteDataReader reader)
		{
			var instance = Activator.CreateInstance(type);
			foreach (var variable in type.GetVariables())
			{
				var variableType = variable.VariableType;
				var columnIndex = reader.GetOrdinal(variable.Name);
				var columnType = reader.GetFieldType(columnIndex);

				// Set nulls
				if (reader.IsDBNull(columnIndex))
				{
					variable.SetValue(instance, null);
				}

				// Matching types
				else if (variableType == columnType)
				{
					variable.SetValue(instance, reader.GetValue(columnIndex));
				}				

				// Enums
				else if (variableType.IsEnum)
				{
					variable.SetValue(instance, Convert.ChangeType(reader.GetValue(columnIndex), variableType.GetEnumUnderlyingType()));
				}

				// Guids
				else if (variableType == typeof(Guid))
				{
					variable.SetValue(instance, reader.GetGuid(columnIndex));
				}

				// Date time
				else if (variableType == typeof(DateTime))
				{
					variable.SetValue(instance, reader.GetDateTime(columnIndex));
				}

				// Byte arrays
				else if (variableType == typeof(byte[]))
				{
					using (var memoryStream = new MemoryStream())
					{
						reader.GetStream(columnIndex).CopyTo(memoryStream);
						variable.SetValue(instance, memoryStream.ToArray());
					}
				}

				// Other basic types
				else if (basicTypes.Contains(variableType))
				{
					variable.SetValue(instance, Convert.ChangeType(reader.GetValue(columnIndex), variableType));
				}

				// Otherwise, Json
				else
				{
					var obj = JsonConvert.DeserializeObject(reader.GetString(columnIndex), variableType);
					variable.SetValue(instance, obj);
				}
			}

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
