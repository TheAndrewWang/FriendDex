# Phase 1: Databases, EF Core, and Friends CRUD

## Why a database at all?
Right now your app forgets everything when it stops. A database stores data on disk in a
structured way so it survives restarts and can be searched quickly.

**Relational database** = data lives in *tables* (rows and columns). Tables link to each other
with keys. Here: one `Friend` row has many `FriendNote` rows.

- **Primary key (PK):** a unique ID for each row (`Friend.Id`).
- **Foreign key (FK):** a column pointing at another table's PK (`FriendNote.FriendId`).
  The database refuses a note that points at a friend who doesn't exist.

## Why SQLite first?
SQLite is a database in a single file (`frienddex.db`). No server, no install, no password.
Perfect for learning. Its limits (one writer at a time, no network access) are exactly why
Phase 3 moves to PostgreSQL. The code you write now will mostly carry over.

## What is an ORM / EF Core?
You *could* write SQL by hand (`INSERT INTO Friends ...`). An **ORM** (object-relational mapper)
lets you work with C# objects and writes the SQL for you. **Entity Framework Core** is .NET's ORM.

- **Entity:** a C# class that maps to a table (`Friend`).
- **DbContext:** your gateway to the database. It has a `DbSet<T>` per table, tracks changes
  to objects, and saves them with `SaveChangesAsync()`.
- **Migration:** a versioned script describing a schema change ("create Friends table").
  Migrations live in git, so every machine (yours, Docker, the cloud) builds the same schema.

## Source of truth vs. derived data
Your raw notes are the **source of truth**. Interests, dislikes, and gift ideas (Phase 2) are
**derived** from them by AI, so they can be thrown away and regenerated. Never store the same
fact in two places by hand. That's also why we **compute age** from `Birthday` instead of
storing it: a stored age goes stale every year.

## Dependency injection (DI) in this phase
You register `AppDbContext` once in `Program.cs`; ASP.NET then hands it to anything that asks.
In Blazor, use `IDbContextFactory<AppDbContext>` rather than injecting the context directly.
Interactive Server components live for a whole browser session, so one long-lived context would
accumulate stale tracked data. The factory gives you a short-lived context per operation.

---

# Your tasks

All paths are under `src/FriendDex.Web/`. Write these yourself; ask me when stuck and I'll
review each step.

## Task 1: Entities (`Domain/`)
```csharp
namespace FriendDex.Web.Domain;

public class Friend
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateOnly? Birthday { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<FriendNote> Notes { get; set; } = new();   // navigation property
}

public class FriendNote
{
    public int Id { get; set; }
    public int FriendId { get; set; }                       // foreign key
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
```
Think about: should `Name` be required? What max length makes sense?

## Task 2: `Data/AppDbContext.cs`
- Inherit `DbContext`, constructor takes `DbContextOptions<AppDbContext>`.
- `DbSet<Friend> Friends` and `DbSet<FriendNote> Notes`.
- In `OnModelCreating`: `Name` required with a max length; `Friend` has many `Notes` with
  cascade delete (deleting a friend deletes their notes).

## Task 3: Register it in `Program.cs`
```csharp
builder.Services.AddDbContextFactory<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));
```
(Already prepared for you: the `ConnectionStrings:Default` entry in `appsettings.json`.)

## Task 4: First migration
From the repo root:
```bash
dotnet ef migrations add Initial --project src/FriendDex.Web
dotnet ef database update --project src/FriendDex.Web
```
Open the generated file in `Migrations/` and read it. It's plain C# describing the tables.
Applying pending migrations automatically at app startup is a Phase 3 topic; for now
use `database update`.

## Task 5: `Services/AgeCalculator.cs` + tests
```csharp
public static class AgeCalculator
{
    public static int? GetAge(DateOnly? birthday, DateOnly today);
    public static DateOnly? NextBirthday(DateOnly? birthday, DateOnly today);
}
```
Pass `today` in instead of calling `DateTime.Now` inside, so tests are deterministic.
Write xUnit tests in `tests/FriendDex.Tests/` for: birthday today, birthday tomorrow,
birthday yesterday, null birthday, and **Feb 29 birthdays** (what's the next birthday in a
non-leap year? Pick a rule: Feb 28 or Mar 1. Document it in a test name).

## Task 6: Pages (`Components/Pages/`)
- `FriendList.razor` at `/friends`: table of name, age, next birthday, with a link to each friend
  and an "Add friend" button.
- `FriendEdit.razor` at `/friends/new` and `/friends/{id:int}/edit`: form for name and birthday.
- `FriendDetail.razor` at `/friends/{id:int}`: shows the friend, lists notes, a box to add a note,
  and a delete button.
- Add a nav link in `Components/Layout/NavMenu.razor`.

Hints: `@rendermode InteractiveServer` at the top of each page; `@inject IDbContextFactory<AppDbContext> Factory`;
`await using var db = await Factory.CreateDbContextAsync();` in each handler;
`EditForm` + `DataAnnotationsValidator` for the form; `.Include(f => f.Notes)` to load notes.

## Done when
- Unit tests for `AgeCalculator` pass: `dotnet test`
- You can create a friend, add two notes, stop the app, restart it, and the data is still there.
- Deleting a friend also removes their notes (check with the `sqlite3` CLI or a DB viewer).

## Concepts to be able to explain afterwards
1. What's the difference between an entity and a table row?
2. Why did we use a DbContext *factory* instead of injecting the context?
3. What does a migration give you that "just edit the database" doesn't?
4. Why is age computed rather than stored?
