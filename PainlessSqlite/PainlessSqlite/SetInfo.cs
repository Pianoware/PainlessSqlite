using System;
using System.Data.SQLite;
using System.Linq;
using System.Reflection;

namespace Pianoware.PainlessSqlite
{
	class SetInfo
	{
		internal VariableInfo SetMember { get; }
		internal TableInfo TableInfo { get; }
		ConstructorInfo constructorInfo;

		internal SetInfo(VariableInfo setMember)
		{
			this.SetMember = setMember;
			this.constructorInfo = setMember.VariableType.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(SQLiteConnection), typeof(string) }, null);
			this.TableInfo = new TableInfo
			{
				Name = setMember.Name,

				Columns = SetMember.VariableType.GenericTypeArguments[0].GetVariables()
					.Select(v => new ColumnInfo { Name = v.Name }).ToArray()
			};
		}

		internal object Create(params object[] parameters)
		{
			return constructorInfo.Invoke(parameters);
		}
	}
}
