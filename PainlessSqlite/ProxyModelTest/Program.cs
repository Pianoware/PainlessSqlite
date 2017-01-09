using System;
using SQLinq;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pianoware.PainlessSqlite;

namespace ProxyModelTest
{
	class Program
	{
		static void Main(string[] args)
		{
			using (var context = new Context())
			{
				Console.WriteLine("Adding models");
				for (int i = 0; i < 30; i++)
				{
					var newModel = new Model { Name = "Arash " + i, Date = DateTime.UtcNow };
					context.Models.Add(newModel);
					Console.WriteLine(newModel.Id);
				}

				Console.WriteLine("Deleting odd models");
				foreach (var model in context.Models.AsEnumerable().Where(m => m.Id % 2 == 1))
				{
					context.Models.Delete(model);
					Console.WriteLine(model.Id);
				}

				Console.WriteLine("Updating models > 20");
				foreach (var model in context.Models.Where(m => m.Id > 20))
				{
					Console.WriteLine(model.Id);
					model.Date = DateTime.MinValue;
					context.Models.Update(model);
				}

				Console.WriteLine("Printing all models");
				foreach (var model in context.Models)
				{
					Console.WriteLine(model.Id + " " + model.Name + " " + model.Date);
				}
			}

			Console.ReadLine();
		}
	}

	class Context : SqliteContext
	{
		public SqliteSet<Model> Models;
	}

	class Model
	{
		public long Id;
		public string Name;
		public DateTime Date;
	}

}
