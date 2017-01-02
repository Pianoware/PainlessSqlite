## Painless Sqlite

Better than barebones SQLite, but not quite Entity Framework.

This is **Painless Sqlite**, for those who think that one's too bare, and that other one's too hairy! 

## Install

Add Nuget Package [Pianoware.PainlessSqlite](https://www.nuget.org/packages/Pianoware.PainlessSqlite/) (Pre-release)

`PM> Install-Package Pianoware.PainlessSqlite -Pre`

## Usage
Inherit from `SqliteContext` and add fields and/or properties of type `SqliteSet<TModel>` where `TModel` is your model type:

```C#
class MyContext : SqliteContext 
{
    public SqliteSet<Employee> Employees;
}

class Employee 
{
    public long Id;
    public string Name;
}
```

That's it folks! Now use it like so:

```C#
using (var myContext = new MyContext()) 
{
    myContext.Employees.Add(new Employee { Name = "Arash Motamedi" });
    foreach (var employee in myContext.Employees)
    {
        GiveRaiseTo(employee); // ;) 
    }
}
```

## What it is
* Lightweight, painless ORM that doesn't try to do too much. 

## What it isn't
* It's not Entity Framework. 
* It won't do fancy migrations. 
* It won't generate crazy SQL queries. 
* It doesn't offer too many customizations. 

## Support or Contact

Fellas, this is provided for your convenience and pleasure and inspiration. If you have any questions or suggestions, email me at arash.motamedi@gmail.com. Contributions that keep with the spirit of the library - lightweight and straightforward - will be greatly appreciated. 
