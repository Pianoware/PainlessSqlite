using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Pianoware.PainlessSqlite
{
	static class ContextHelper
	{
		// Returns array of SetInfo describing context Set members and their Model types
		static readonly ConcurrentDictionary<Type, ContextInfo> contextInfoCache = new ConcurrentDictionary<Type, ContextInfo>();
		internal static ContextInfo GetContextInfo(Type contextType)
		{
			if (contextInfoCache.ContainsKey(contextType))
				return contextInfoCache[contextType];

			// Ok not to lock or double check conditions

			var sets = contextType.GetVariables()
				.Where(m => m.VariableType.IsGenericType
							&& m.VariableType.GetGenericTypeDefinition() == typeof(SqliteSet<>));

			var contextInfo = new ContextInfo(sets.Select(s => new SetInfo(s)).ToArray());

			contextInfoCache[contextType] = contextInfo;
			return contextInfo;
		}
	}
}
