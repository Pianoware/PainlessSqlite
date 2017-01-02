using System;
using System.Reflection;

namespace Pianoware.PainlessSqlite
{
	// Simplify access to types' fields and properties
	class VariableInfo
	{
		internal Type VariableType { get; }
		internal string Name { get; }

		// Only one of the following variables will be populated
		FieldInfo fieldInfo;
		PropertyInfo propertyInfo;

		internal VariableInfo(MemberInfo memberInfo)
		{
			// Passed member should be fieldInfo or propertyInfo
			if (!(memberInfo is FieldInfo || memberInfo is PropertyInfo))
				throw new ArgumentException("Member must be field or property");

			Name = memberInfo.Name;
			if (memberInfo is FieldInfo)
			{
				fieldInfo = memberInfo as FieldInfo;
				VariableType = fieldInfo.FieldType;
			}
			else
			{
				propertyInfo = memberInfo as PropertyInfo;
				VariableType = propertyInfo.PropertyType;
			}
		}

		// Get field or property value
		internal object GetValue(object instance)
		{
			return fieldInfo != null ? fieldInfo.GetValue(instance) : propertyInfo.GetValue(instance);
		}

		// Set field or property value
		internal void SetValue(object instance, object value)
		{
			if (fieldInfo != null)
				fieldInfo.SetValue(instance, value);
			else
				propertyInfo.SetValue(instance, value);
		}
	}
}
