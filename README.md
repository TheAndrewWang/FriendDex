# FriendDex

A personal "dex" for the people in your life. FriendDex keeps the details you tend to forget about your friends (birthdays, favorite colors, what they love and what they can't stand) and shows each friend as a pixel-art sprite in a chibi game-menu style.

## Features

- **Friend list as a game menu.** Every friend gets a slot with their sprite, name and level (their age). A badge appears when a birthday is less than two weeks away.
- **Friend passport.** One page per friend with their details, a countdown stamp to their next birthday, and their likes and dislikes. Click any like or dislike to read or edit its notes.
- **Tell FriendDex in plain English.** Type a note like *"Got into bouldering this spring, but hates crowded gyms"*. Claude reads it and:
  - adds new likes and dislikes straight away;
  - lists edits, removals, birthday and color changes so you can approve them first.
- **AI pixel sprites.** One click asks Google's Gemini (Nano Banana 2) to draw the friend as a full-body chibi sprite. It uses their uploaded photo, favorite color and a few of their likes. FriendDex then trims, centers and snaps the image to a 48×48 pixel grid so every sprite matches.
- **Default sprites.** Until you generate one, each friend gets a hand-drawn pixel sprite (boy, girl or neutral, chosen from the gender field), wearing their favorite color.
- **Stored in MongoDB Atlas.** Each friend is one document, with their likes and dislikes embedded inside it.

## Tech stack

| Part | Technology |
|---|---|
| Web app | ASP.NET Core Blazor (interactive server), .NET 10 |
| Database | MongoDB Atlas through the MongoDB EF Core provider |
| Note understanding | Claude API (`claude-opus-5-5`) with structured JSON output |
| Sprite generation | Gemini API, `gemini-3.1-flash-image` (Nano Banana 2) |
| Image processing | SkiaSharp |
| Tests | xUnit |

## Getting started

### 1. Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A free [MongoDB Atlas](https://www.mongodb.com/cloud/atlas) cluster (the M0 tier is enough)
- An [Anthropic API key](https://console.anthropic.com/), used to turn your notes into likes and dislikes
- A [Gemini API key](https://aistudio.google.com/), used to generate sprites

The app runs without the two AI keys. Only the note box and sprite generation need them.

### 2. Set up MongoDB Atlas

1. Create a free cluster.
2. Under **Database Access**, add a database user with a password.
3. Under **Network Access**, add your IP address.
4. Click **Connect → Drivers** and copy the connection string.

### 3. Add your keys

Copy the example file and fill it in:

```bash
cp .env.example .env
```

```
ANTHROPIC_API_KEY=your-anthropic-key
GEMINI_API_KEY=your-gemini-key
MONGODB_URI=mongodb+srv://<user>:<password>@<cluster>.mongodb.net/
MONGODB_DATABASE=frienddex
```

`.env` is git-ignored, so your keys never get committed. The app loads it when it starts; restart the app after changing a key.

### 4. Run it

```bash
dotnet run --project src/FriendDex.Web
```

Then open http://localhost:5044.

### 5. Run the tests

```bash
dotnet test
```

The tests don't need MongoDB or any API keys. Database tests run the same data model on in-memory SQLite, and a separate test checks that the model is valid for the MongoDB provider.

## Project structure

```
src/FriendDex.Web/
  Domain/          Friend and Interest (likes and dislikes) classes
  Data/            AppDbContext (MongoDB mapping) and the random ID generator
  Services/
    FriendNoteAgent.cs   Claude: turns a typed note into proposed changes
    ChibiGenerator.cs    Gemini: builds the sprite prompt and calls the API
    Pixelator.cs         Trims, centers and snaps images to a pixel grid
    AgeCalculator.cs     Age and next-birthday math
    DotEnv.cs            Loads .env at startup
  Components/
    Pages/         Friend list, friend passport, add/edit form
    Shared/        Default pixel sprites
  wwwroot/app.css  Game-menu styles
tests/FriendDex.Tests/   xUnit tests
```

## How the data is stored

Each friend is one document in the `friends` collection, roughly like this:

```json
{
  "_id": 1840395721,
  "Name": "Sam",
  "Gender": "she/her",
  "Birthday": "1999-03-04",
  "FavoriteColor": "teal",
  "Interests": [
    { "Id": 552019384, "Label": "bouldering", "Notes": "Started last spring", "Kind": "Like" },
    { "Id": 90377125, "Label": "crowds", "Notes": "Hates crowded gyms", "Kind": "Dislike" }
  ],
  "Photo": "<binary>",
  "Avatar": "<binary>"
}
```

MongoDB doesn't number IDs automatically, so FriendDex picks a random positive number for each new friend and interest.

## Privacy

- Notes you type and the friend's current details are sent to Anthropic to work out the changes.
- Photos you generate sprites from are sent to Google.
- Everything else stays in your own MongoDB Atlas database.

Ask your friends before uploading their photos.

## Branches

| Branch | Database |
|---|---|
| `mongodb` | MongoDB Atlas (this README) |
| `main` | SQLite, stored in a local file |
