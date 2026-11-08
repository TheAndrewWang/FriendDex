# FriendDex server (website + API for the Android app).
# Build:  docker build -t frienddex .
# Run:    docker run -p 8080:8080 --env-file .env frienddex

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Copy the code BEFORE restoring: the .NET 10 Blazor build looks at the source files during restore to decide
# which framework files to include. Restoring with only the .csproj files leaves out blazor.web.js,
# and then no buttons on the website work.
COPY src/FriendDex.UI/ src/FriendDex.UI/
COPY src/FriendDex.Web/ src/FriendDex.Web/
RUN dotnet publish src/FriendDex.Web/FriendDex.Web.csproj -c Release -o /app
# Fail the build if the Blazor script is missing, instead of deploying a site with dead buttons.
RUN test -f /app/wwwroot/_framework/blazor.web.js

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Render, Railway and Fly tell the app which port to use through $PORT; default to 8080.
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet FriendDex.Web.dll"]
