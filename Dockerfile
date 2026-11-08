# FriendDex server (website + API for the Android app).
# Build:  docker build -t frienddex .
# Run:    docker run -p 8080:8080 --env-file .env frienddex

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/FriendDex.UI/FriendDex.UI.csproj src/FriendDex.UI/
COPY src/FriendDex.Web/FriendDex.Web.csproj src/FriendDex.Web/
RUN dotnet restore src/FriendDex.Web/FriendDex.Web.csproj
COPY src/FriendDex.UI/ src/FriendDex.UI/
COPY src/FriendDex.Web/ src/FriendDex.Web/
RUN dotnet publish src/FriendDex.Web/FriendDex.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Render, Railway and Fly tell the app which port to use through $PORT; default to 8080.
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet FriendDex.Web.dll"]
