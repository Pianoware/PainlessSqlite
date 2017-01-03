using Newtonsoft.Json;
using Pianoware.PainlessSqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace PainlessSqlite.TestConsole
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine(test((DateTime d) => d < DateTime.Now));

			using (var context = new MyContext())
			{
				var employee = new Employee
				{
					Name = "Arash Motamedi",
					EmployeeType = EmployeeType.Contractor,
					//Address = new Address
					//{
					//	Street = "901 8th St",
					//	City = "Seattle",
					//	ZipCode = 98104
					//}
				};
				var id = context.Employees.Add(employee).Id;
				var employees = context.Employees.Where(e => e.Id > 0).OrderBy(e => e.Name);
				foreach (var emp in employees)
				{
					Console.WriteLine(JsonConvert.SerializeObject(emp, Formatting.Indented));
				}
			}

			Console.ReadLine();
		}

		static string test<T> (Expression<Predicate<T>> pred)
		{
			return pred.Body.ToString();
		}
	}

	enum EmployeeType
	{
		FullTime, Contractor
	}

	class Address
	{
		public string Street { get; set; }
		public string City { get; set; }
		public int ZipCode { get; set; }
	}

	class Employee
	{
		public long Id { get; set; }
		public string Name { get; set; }
		public int? SeocialSecurity { get; set; }
		public EmployeeType EmployeeType { get; set; }
		//public Address Address { get; set; }
	}

	class MyContext : SqliteContext
	{
		public SqliteSet<Employee> Employees { get; set; }
	}
}
