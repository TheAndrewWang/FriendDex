# Phase 0: How .NET and C# work

Read this alongside the generated files in `src/FriendDex.Web/`. Open each file as you read.

## 1. The big picture: C# vs .NET

- **C#** is the *language*: the syntax you type.
- **.NET** is the *platform*: the runtime that executes your program, plus a huge standard
  library (files, networking, JSON, dates...) and tools (`dotnet` CLI).
- **ASP.NET Core** is the web framework built on .NET. **Blazor** is the part of it that lets
  you build UI in C# instead of JavaScript. **EF Core** is the database library.

What happens when you run `dotnet run`:
```
.cs / .razor files  --compile (Roslyn)-->  IL code in a .dll  --run-->  CLR (runtime) JIT-compiles IL to machine code
```
- **IL** (intermediate language) is a portable half-compiled form. That's why the same .dll
  runs on Linux, Windows, and Mac.
- **CLR** (common language runtime) runs your code and does **garbage collection**: you never
  free memory by hand, the runtime cleans up objects nothing references anymore.
- **Static typing:** the compiler knows every variable's type and refuses to build if they
  don't fit. Most bugs show up as red squiggles, not as crashes at 2am.

## 2. Anatomy of the solution

| Thing | What it is |
|---|---|
| `FriendDex.slnx` | **Solution**: a list of projects that belong together |
| `*.csproj` | **Project**: one buildable unit (becomes one .dll). XML listing the target framework, packages, and references |
| NuGet package | A library someone published (`Microsoft.EntityFrameworkCore.Sqlite`). `dotnet add package` writes a `<PackageReference>` into the csproj |
| `bin/` and `obj/` | Build output. Generated, git-ignored, safe to delete |
| `Properties/launchSettings.json` | Dev-only settings (which port to listen on) |

Open `src/FriendDex.Web/FriendDex.Web.csproj`. Notice `<Nullable>enable</Nullable>` and
`<ImplicitUsings>enable</ImplicitUsings>`; both are explained below.

Commands you'll use constantly:
```bash
dotnet build            # compile
dotnet run --project src/FriendDex.Web
dotnet watch --project src/FriendDex.Web   # rebuild + reload when you save (great while learning)
dotnet test             # run xUnit tests
dotnet add <proj> package <Name>
```

## 3. C# essentials (all of these appear in this project)

**Types and variables**
```csharp
int count = 3;              // value type, stored directly
string name = "Ana";        // reference type, variable points at an object
var age = 25;               // 'var' = compiler infers int. Still statically typed!
DateOnly birthday = new(1998, 4, 12);
```
- **Value types** (`int`, `bool`, `DateOnly`, `struct`) are copied on assignment.
- **Reference types** (`string`, `class`, `List<T>`) are shared: two variables can point at the same object.

**Classes, properties, records**
```csharp
public class Friend
{
    public int Id { get; set; }                 // property: field + get/set, with a clean syntax
    public string Name { get; set; } = "";
}
var f = new Friend { Name = "Ana" };            // object initializer
```
An **interface** (`IProfileExtractor`, Phase 2) is a contract: "anything with these methods".
Code depends on the interface, so you can swap implementations (Claude -> OpenAI, real -> fake in tests).

**Nullability** (`<Nullable>enable</Nullable>`)
```csharp
string a = "x";      // can never be null; compiler warns if you try
string? b = null;    // explicitly allowed to be null
DateOnly? birthday;  // Nullable<DateOnly>: "a date, or no date"
if (birthday is not null) { ... birthday.Value ... }
```
This kills the classic `NullReferenceException`. Treat nullable warnings as real bugs.

**Collections and LINQ**
```csharp
List<string> likes = new() { "climbing", "tea" };
var long = friends.Where(f => f.Name.Length > 5)   // LINQ: query collections declaratively
                  .OrderBy(f => f.Name)
                  .ToList();
```
`f => f.Name.Length > 5` is a **lambda**, a tiny inline function. EF Core translates LINQ
like this into SQL (`WHERE LENGTH(Name) > 5`), which is the magic of Phase 1.

**async / await** (the most important concept for web apps)
```csharp
var friend = await db.Friends.FindAsync(id);   // thread is FREE while the database works
```
A web server handles many users at once with few threads. Blocking a thread while waiting on the
database wastes it. `await` says "pause this method here, give the thread back, resume when the
result arrives". Rules of thumb: methods that await return `Task` / `Task<T>` and are named
`...Async`; async all the way up, never `.Result` or `.Wait()`.

**Misc you'll meet:** `using` (import a namespace, or dispose a resource), `await using`
(dispose when scope ends, e.g. a DbContext), string interpolation `$"Hi {name}"`,
`namespace` (folders of code), `static` (belongs to the type, not an instance; `AgeCalculator`).

## 4. How `Program.cs` works (the heart of ASP.NET Core)

Open `src/FriendDex.Web/Program.cs`.

**Step 1: Builder: register services (dependency injection).**
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
```
`builder.Services` is the **DI container**, a registry of "when someone asks for X, build or give them Y".
You never write `new AppDbContext(...)` in your pages. You *ask* for it and the framework provides it.
Why: testable (swap in fakes), configurable in one place, and objects get a managed lifetime:

| Lifetime | Meaning | Example |
|---|---|---|
| Singleton | one instance for the whole app | Redis connection, Claude client |
| Scoped | one per web request (per Blazor circuit) | normally a DbContext |
| Transient | new every time | lightweight helpers |

**Step 2: Build, then the middleware pipeline.**
```csharp
var app = builder.Build();
app.UseExceptionHandler(...); app.UseAntiforgery(); ...
```
Each `Use...` is **middleware**: a step every HTTP request passes through, in order, and the
response passes back out in reverse. Auth, error pages, and static files are all middleware.
Order matters.

**Step 3: Endpoints and `app.Run()`.**
`MapRazorComponents<App>()` maps URLs to your `.razor` pages. `app.Run()` starts the web server
(Kestrel) and blocks while it serves requests. Phase 2 adds `app.MapGet("/avatars/{id}", ...)`,
a **minimal API** endpoint.

**Configuration** comes from layers, later ones override earlier ones:
`appsettings.json` < `appsettings.Development.json` < **user-secrets** < **environment variables**
< command-line args. That layering is how the same code gets different settings locally, in
Docker (Phase 3), and in the cloud (Phase 5), and why API keys never go in `appsettings.json`.
Read it in code with `builder.Configuration["Key:Sub"]` or `GetConnectionString("Default")`.

## 5. How Blazor works

A `.razor` file mixes HTML and C#:
```razor
@page "/friends"
@rendermode InteractiveServer
@inject IDbContextFactory<AppDbContext> Factory

<h1>Friends (@friends.Count)</h1>
@foreach (var f in friends) { <p>@f.Name</p> }

@code {
    private List<Friend> friends = new();
    protected override async Task OnInitializedAsync() { ... load from db ... }
}
```
- `@page` gives it a URL. `@code { }` holds the C# for the component. `@name` prints a C# value.
- A component compiles to a C# class; lifecycle methods like `OnInitializedAsync` run at set moments.
- **Interactive Server mode:** your C# runs **on the server**. The browser keeps a live
  connection (SignalR/WebSocket); clicks are sent to the server, your code runs, and Blazor
  sends back only the HTML *differences*. Consequences you should remember:
  1. Secrets (API keys) and database access stay safely server-side.
  2. Every open browser tab holds server memory and a connection.
  3. If the connection drops, the page's in-memory state is lost.

## 6. Mini exercises (10 minutes, optional)

1. Run `dotnet watch --project src/FriendDex.Web`. Edit text in `Components/Pages/Home.razor`
   and save; watch the browser update without restarting.
2. In `Counter.razor`, change the button so each click adds 2. Find where the `@code` block is.
3. In `Program.cs`, add `app.MapGet("/ping", () => "pong");` before `app.Run()`, then open `/ping`.
   You just wrote a minimal API endpoint.
4. In the csproj, change `<Nullable>enable</Nullable>` to `disable`. Rebuild. What changes? Change it back.

## Check yourself
1. What is the difference between C#, .NET, and ASP.NET Core?
2. What does `await` do to the thread?
3. Why do pages *ask* for services instead of calling `new`?
4. Where does your Blazor C# code actually run in Interactive Server mode?
