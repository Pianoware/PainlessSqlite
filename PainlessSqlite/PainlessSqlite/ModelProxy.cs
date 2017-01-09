using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Pianoware.PainlessSqlite
{
	public static class MyTypeBuilder
	{
		public static void CreateNewObject()
		{
			var myType = CompileResultType();
			var myObject = Activator.CreateInstance(myType);
		}

		public static Type CompileResultType()
		{
			TypeBuilder tb = GetTypeBuilder();
			ConstructorBuilder constructor = tb.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);

			// NOTE: assuming your list contains Field objects with fields FieldName(string) and FieldType(Type)
			//foreach (var field in yourListOfFields)
			//	CreateProperty(tb, field.FieldName, field.FieldType);

			Type objectType = tb.CreateType();
			return objectType;
		}

		private static TypeBuilder GetTypeBuilder()
		{
			var typeSignature = "MyDynamicType";
			var an = new AssemblyName(typeSignature);
			AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
			ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
			TypeBuilder tb = moduleBuilder.DefineType(typeSignature,
					TypeAttributes.Public |
					TypeAttributes.Class |
					TypeAttributes.AutoClass |
					TypeAttributes.AnsiClass |
					TypeAttributes.BeforeFieldInit |
					TypeAttributes.AutoLayout,
					null);
			return tb;
		}

		private static void CreateProperty(TypeBuilder tb, string propertyName, Type propertyType)
		{
			FieldBuilder fieldBuilder = tb.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);

			PropertyBuilder propertyBuilder = tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);
			MethodBuilder getPropMthdBldr = tb.DefineMethod("get_" + propertyName, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, propertyType, Type.EmptyTypes);
			ILGenerator getIl = getPropMthdBldr.GetILGenerator();

			getIl.Emit(OpCodes.Ldarg_0);
			getIl.Emit(OpCodes.Ldfld, fieldBuilder);
			getIl.Emit(OpCodes.Ret);

			MethodBuilder setPropMthdBldr =
				tb.DefineMethod("set_" + propertyName,
				  MethodAttributes.Public |
				  MethodAttributes.SpecialName |
				  MethodAttributes.HideBySig,
				  null, new[] { propertyType });

			ILGenerator setIl = setPropMthdBldr.GetILGenerator();
			Label modifyProperty = setIl.DefineLabel();
			Label exitSet = setIl.DefineLabel();

			setIl.MarkLabel(modifyProperty);
			setIl.Emit(OpCodes.Ldarg_0);
			setIl.Emit(OpCodes.Ldarg_1);
			setIl.Emit(OpCodes.Stfld, fieldBuilder);

			setIl.Emit(OpCodes.Nop);
			setIl.MarkLabel(exitSet);
			setIl.Emit(OpCodes.Ret);

			propertyBuilder.SetGetMethod(getPropMthdBldr);
			propertyBuilder.SetSetMethod(setPropMthdBldr);
		}
	}

	class ModelProxy
	{
		public ModelProxy(Type modelType)
		{

		}

		public object GetModel(Dictionary<string, object> dictionary)
		{
			return null;
		}

		public Dictionary<string, object> GetDictionary(object model)
		{
			return null;
		}
	}

	class ModelProxy<TModel> : ModelProxy
	{
		public ModelProxy() : base(typeof(TModel)) { }
		public TModel GetModel(Dictionary<string, object> dictionary)
		{
			return (TModel)base.GetModel(dictionary);
		}
	}

	abstract class Convertor<T>
	{
		public abstract SQLiteParameter ToParameter(T value);
		public abstract T ToValue(SQLiteDataReaderValue value);
	}

	class IntConvertor : Convertor<int>
	{
		public override SQLiteParameter ToParameter(int value)
		=> new SQLiteParameter();

		public override int ToValue(SQLiteDataReaderValue value)
		{
			throw new NotImplementedException();
		}
	}



}
