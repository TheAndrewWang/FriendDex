# Phase 1: Databases, EF Core, and Friends CRUD

## Why a database at all?
Right now your app forgets everything when it stops. A database stores data on disk in a
structured way so it survives restarts and can be searched quickly.

**Relational database** = data lives in *tables* (rows and columns). Tables link to each other
with keys. Here: one `Friend` row has many `Interest` rows. Each interest is either a like or a
dislike.

- **Primary key (PK):** a unique ID for each row (`Friend.Id`).
- **Foreign key (FK):** a column pointing at another table's PK (`Interest.FriendId`).
  The database refuses an interest that points at a friend who doesn't exist.

## Why one table for likes and dislikes?
A column holds one value, so a list like "hiking, sushi" can't live in a single column of
`Friends`. Each interest becomes its own row. Likes and dislikes have exactly the same shape
(label, notes, which friend), so they share one `Interests` table with a `Kind` column saying
`Like` or `Dislike`. Two tables would mean writing the same class and code twice. The tradeoff:
every query that wants only likes must filter by `Kind`.

## Why SQLite first?
SQLite is a database in a single file (`frienddex.db`). No server, no install, no password.
Perfect for learning. Its limits (one writer at a time, no network access) are exactly why
Phase 3 moves to PostgreSQL. The code you write now will mostly carry over.

## What is an ORM / EF Core?
You *could* write SQL by hand (`INSERT INTO Friends ...`). An **ORM** (object-relational mapper)
lets you work with C# objects and writes the SQL for you. **Entity Framework Core** is .NET's ORM.

- **Entity:** a C# class that maps to a table (`Friend`, `Interest`).
- **DbContext:** your gateway to the database. It has a `DbSet<T>` per table, tracks changes
  to objects, and saves them with `SaveChangesAsync()`.
- **Migration:** a versioned script describing a schema change ("create Friends table").
  Migrations live in git, so every machine (yours, Docker, the cloud) builds the same schema.

## What's stored, and what isn't
The `Friend` and `Interest` rows *are* the record. FriendDex does not keep the raw text you type
about a friend; in Phase 2 the agent reads your text and writes interests directly. That keeps the
data simple to track, but it's a tradeoff: if the agent misreads something, the original wording
is gone, and you fix it by editing the interest by hand.

Still, never store a fact you can calculate. That's why we **compute age** from `Birthday`
instead of storing it: a stored age goes stale every year.

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
Write these in `namespace FriendDex.Web.Domain;`.

**`InterestKind`**: an `enum` with two values, `Like` and `Dislike`.

**`Friend`**

| Property | Type | Notes |
|---|---|---|
| `Id` | `int` | Primary key. EF fills it in when you save |
| `Name` | `string` | Required |
| `Gender` | `string?` | Free text, optional |
| `Birthday` | `DateOnly?` | Optional: you may not know it |
| `FavoriteColor` | `string?` | Optional |
| `Interests` | `List<Interest>` | Navigation property: start it as an empty list |

**`Interest`**

| Property | Type | Notes |
|---|---|---|
| `Id` | `int` | Primary key |
| `FriendId` | `int` | Foreign key to `Friend.Id` |
| `Friend` | `Friend` | Navigation property back to the owner |
| `Label` | `string` | Required: the short word, e.g. "hiking" |
| `Notes` | `string?` | Optional free-form detail, e.g. "got into bouldering last spring" |
| `Kind` | `InterestKind` | `Like` or `Dislike` |

Think about:
- `string` vs `string?` with `<Nullable>enable</Nullable>`: what does the compiler do
  differently, and how will EF treat each column?
- How do you initialize a required `string` so the compiler doesn't warn? (`= ""`, or the
  `required` keyword: what's the difference?)
- What max length makes sense for `Name` and `Label`? Should `Notes` have one?

## Task 2: `Data/AppDbContext.cs`
- Inherit `DbContext`, constructor takes `DbContextOptions<AppDbContext>`.
- `DbSet<Friend> Friends` and `DbSet<Interest> Interests`.
- In `OnModelCreating`:
  - `Friend.Name` required with a max length; `Interest.Label` required with a max length.
  - `Friend` has many `Interests` with cascade delete (deleting a friend deletes their interests).
  - Store `Kind` as text with `.HasConversion<string>()`. Without it, EF stores the enum as a
    number (`0`/`1`). Open the migration with and without it and compare.

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
Find the foreign key, the cascade rule, and the index EF added on `FriendId`. Why would EF index
that column?
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
- `FriendEdit.razor` at `/friends/new` and `/friends/{id:int}/edit`: form for name, gender,
  birthday, and favorite color.
- `FriendDetail.razor` at `/friends/{id:int}`: shows the friend's details, then two lists,
  **Likes** and **Dislikes**, each showing interest labels.
  - Clicking an interest shows its notes, with a way to edit or delete it.
  - A small form to add an interest: label, optional notes, and Like/Dislike.
  - A delete button for the friend.
- Add a nav link in `Components/Layout/NavMenu.razor`.

Hints: `@rendermode InteractiveServer` at the top of each page; `@inject IDbContextFactory<AppDbContext> Factory`;
`await using var db = await Factory.CreateDbContextAsync();` in each handler;
`EditForm` + `DataAnnotationsValidator` for the forms; `.Include(f => f.Interests)` to load
interests; `friend.Interests.Where(i => i.Kind == InterestKind.Like)` to split the lists.

## Done when
- Unit tests for `AgeCalculator` pass: `dotnet test`
- You can create a friend, add a like and a dislike (one with notes), stop the app, restart it,
  and the data is still there.
- Deleting a friend also removes their interests (check with the `sqlite3` CLI or a DB viewer).

## Concepts to be able to explain afterwards
1. What's the difference between an entity and a table row?
2. Why did we use a DbContext *factory* instead of injecting the context?
3. What does a migration give you that "just edit the database" doesn't?
4. Why is age computed rather than stored?
5. Why one `Interests` table with a `Kind` column instead of separate likes and dislikes tables?
6. What do you lose by not keeping the raw text you type, and how would you recover from an
   agent mistake?
