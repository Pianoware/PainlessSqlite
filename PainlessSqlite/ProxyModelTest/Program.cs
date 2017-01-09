using System;
using SQLinq;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pianoware.PainlessSqlite;
using System.IO;

namespace ProxyModelTest
{
	class Program
	{
		static void Main(string[] args)
		{
			var iterations = 1000;
			var t1 = DateTime.Now;
			using (var context = new Context("Data Source=" + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "db.sqlite")))
			{
				Console.WriteLine("Adding models");
				for (int i = 0; i < iterations; i++)
				{
					var newModel = new Model { Name = "Arash " + i, Date = DateTime.UtcNow };
					context.Models.Add(newModel);
					//Console.WriteLine(newModel.Id);
				}

				Console.WriteLine(DateTime.Now.Subtract(t1).TotalSeconds + " seconds to insert " + iterations + " records");

				Console.WriteLine("Deleting odd models");
				foreach (var model in context.Models.AsEnumerable().Where(m => m.Id % 2 == 1))
				{
					context.Models.Delete(model);
					//Console.WriteLine(model.Id);
				}

				Console.WriteLine("Updating models > " + iterations / 2);
				foreach (var model in context.Models.Where(m => m.Id > iterations / 2))
				{
					model.Date = DateTime.MinValue;
					context.Models.Update(model);
					//Console.WriteLine(model.Id);
				}

				Console.WriteLine("Printing all models");
				foreach (var model in context.Models)
				{
					if (model.Id % 1000 == 0)
						Console.WriteLine(model.Id + " " + model.Name + " " + model.Date);
				}
			}

			Console.WriteLine(DateTime.Now.Subtract(t1).TotalSeconds + " seconds to insert, delete, update " + iterations + " records");
			Console.ReadLine();
		}
	}

	class Context : SqliteContext
	{
		public Context(string connectionString)
			:base(connectionString: connectionString) { }

		public SqliteSet<Model> Models;
	}

	class Model
	{
		public long Id;
		public string Name;
		public DateTime Date;
	}

}
