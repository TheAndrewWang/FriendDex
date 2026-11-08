# FriendDex on Android

The Android app is a .NET MAUI app that shows the same pages as the website. It doesn't hold anyone's
friends or your API keys. It asks the FriendDex **server** for everything:

```
Phone (FriendDex app) ──HTTPS + login token──▶ FriendDex server ──▶ MongoDB Atlas, Claude, Gemini
Browser (website)     ──HTTPS + login cookie─▶ FriendDex server
```

Everyone has their own account (email and password) and sees only their own friends. Creating an account
needs the invite code you set on the server, so only people you share it with can sign up and use your AI credits.

So you need the server running somewhere the phone can reach. Hosting it online is the most reliable option.

## 1. Host the server online

Vercel can't run this server: it only hosts static sites and short JavaScript functions, and FriendDex needs a
.NET server that stays connected to the browser. Use a host that runs Docker containers. The repo has a
`Dockerfile`, so any of these work: **Render** (simplest, free tier), Railway, Fly.io or Azure App Service.

### Render (free)

1. Push this branch to GitHub.
2. On [render.com](https://render.com), choose **New → Web Service**, connect the repo and pick this branch.
   Render detects the `Dockerfile`.
3. Choose the **Free** instance type.
4. Under **Environment**, add the same values as your `.env`:

   | Key | Value |
   |---|---|
   | `MONGODB_URI` | your Atlas connection string |
   | `MONGODB_DATABASE` | `frienddex` |
   | `ANTHROPIC_API_KEY` | your Anthropic key |
   | `GEMINI_API_KEY` | your Gemini key |
   | `FRIENDDEX_INVITE_CODE` | the code people need to create an account |

5. Deploy. Your server will be at an address like `https://frienddex.onrender.com`.
6. In MongoDB Atlas, open **Network Access** and allow `0.0.0.0/0`. Render's addresses change, so Atlas
   has to accept connections from anywhere. Your database password still protects it.

Pick an invite code that's hard to guess (for example, run `openssl rand -base64 12`) and share it only with
people you want to let in. Anyone with the code can create an account and use the note box and sprite drawing,
which spend your Claude and Gemini credits. Change the code on Render to stop new sign-ups; existing accounts keep working.

The first account created gets any friends you saved before accounts existed, so sign up first yourself.

Free Render servers go to sleep after 15 minutes without visitors. The first request after that takes about
a minute. Open the website a minute before you demo.

The `Dockerfile` hasn't been built on this machine (Docker isn't installed here), but the same release build was
tested locally. If the host's build fails, send me its log.

## 2. Build and install the app

Build the app (your machine already has the Android tools):

```bash
dotnet build src/FriendDex.App -f net10.0-android
```

The installable file is:

```
src/FriendDex.App/bin/Debug/net10.0-android/com.frienddex.app-Signed.apk
```

Install it on your phone, either way:

- **With a USB cable:** turn on *Developer options → USB debugging* on the phone, plug it in, then run
  `~/Android/Sdk/platform-tools/adb install -r src/FriendDex.App/bin/Debug/net10.0-android/com.frienddex.app-Signed.apk`
- **Without a cable:** copy the `.apk` to the phone (Google Drive, email to yourself, etc.), open it, and allow
  installing from that source when Android asks.

## 3. Log in on the app

On first launch the app opens the login screen:

- **Server address:** your hosted address, e.g. `https://frienddex.onrender.com`
- **Email and password:** the same account as the website. Tap **Create an account** to sign up from the
  phone instead (you'll need the invite code).

The app keeps a personal login token in Android's secure storage, never your password. Use the ⚙ button to see
which account is logged in, or to log out (that also signs the token out on the server).

## Backup: run the server on your laptop

If you can't host it, run the server on your laptop so devices on the same network can reach it:

```bash
dotnet run --project src/FriendDex.Web --launch-profile lan
```

Then in the app, use `http://<your-laptop-ip>:5044` as the server address (find the IP with `hostname -I`).
Set `FRIENDDEX_INVITE_CODE` in your laptop's `.env` so you can create an account.

Venue and school Wi-Fi often blocks devices from talking to each other. If the app can't connect, turn on your
phone's hotspot and connect the laptop to it.

## How it's built

| Project | What it is |
|---|---|
| `src/FriendDex.UI` | Shared pages, sprites, styles and the `IFriendDexService` interface |
| `src/FriendDex.Web` | The server: website, accounts (`AccountService`), `/api` for the app, MongoDB, Claude and Gemini (`ServerFriendDexService`) |
| `src/FriendDex.App` | The Android app: shows the shared pages and calls the API (`HttpFriendDexService`) |

`tests/FriendDex.Tests/ApiEndToEndTests.cs` runs the real server in memory and drives it with the app's
`HttpAccountClient` and `HttpFriendDexService`, so logins, the app-to-server path and the rule that accounts
can't see each other's friends are tested without a phone.

Security details: passwords are stored with ASP.NET Core's salted password hasher; app login tokens are random
and only their SHA-256 hash is stored; login and sign-up are limited to 10 attempts per minute per address.
