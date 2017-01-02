## Welcome to GitHub Pages

Better than barebones SQLite, but not quite Entity Framework either... This is **Painess Sqlite**, for those who think that one's too bare, and that other one's too hairy! 

## Usage
Inherit from SqliteContext and add fields and/or properties of type `SqliteSet<TModel>` where `TModel` is your model type:

`class MyContext : SqliteContext 
{
  SqliteSet<Employee> Employees;
}

class Employee 
{
  public long Id;
  public string Name;
}
`

That's it folks! Now use it like so:

`using (var myContext = new MyContext()) 
{
  myContext.Employees.Add(new Employee { Name = "Arash Motamedi" });
  foreach (var employee in myContext.Employees)
  {
    GiveRaiseTo(employee); // ;) 
  }
}`

## What it is
Lightweight, painless ORM that doesn't try to do too much. 

## What it is not
It's not Entity Framework. 
It won't do crazy migrations. 
It won't generate fancy SQL queries. 
It doesn't offer too many customizations. 

## Support or Contact

Fellas, this is provided for your convenience and pleasure and inspiration. If you have any questions or suggestions, email me at arash.motamedi@gmail.com. Contributions that keep with the spirit of the library - that is, lightweight and straightforward, it'd be greatly appreciated. 
