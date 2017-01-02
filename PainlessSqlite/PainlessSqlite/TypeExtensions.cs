using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace Pianoware.PainlessSqlite
{
	static class TypeExtensions
	{
		static ConcurrentDictionary<MemberInfo, VariableInfo> memberVariableCache = new ConcurrentDictionary<MemberInfo, VariableInfo>();
		static ConcurrentDictionary<Type, VariableInfo[]> typeVariablesCache = new ConcurrentDictionary<Type, VariableInfo[]>();

		internal static VariableInfo GetVariableInfo(this MemberInfo memberInfo)
		{
			if (memberVariableCache.ContainsKey(memberInfo))
				return memberVariableCache[memberInfo];

			var variableInfo = new VariableInfo(memberInfo);
			memberVariableCache[memberInfo] = variableInfo;
			return variableInfo;
		}

		internal static VariableInfo[] GetVariables(this Type type)
		{
			if (typeVariablesCache.ContainsKey(type))
				return typeVariablesCache[type];

			var variables = new MemberInfo[0].Union(type.GetFields()).Union(type.GetProperties()).Select(m => m.GetVariableInfo()).ToArray();
			typeVariablesCache[type] = variables;
			return variables;
		}

		internal static VariableInfo GetVariable(this Type type, string name)
		{
			return type.GetVariables().Where(v => v.Name == name).SingleOrDefault();
		}
	}
}
