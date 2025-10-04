# ---------- build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
# Restore first (layer cache friendly)
RUN dotnet restore

# Publish (self-contained optional; here framework-dependent)
RUN dotnet publish src/Deck.Api/Deck.Api.csproj -c Release -o /app/publish

# ---------- runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create a writable data folder for SQLite
# (For dev simplicity we chmod 777; tighten for production as needed)
RUN mkdir -p /app/data && chmod -R 777 /app/data

# Copy published files
COPY --from=build /app/publish ./

# ASP.NET Core will listen on this port inside the container
ENV ASPNETCORE_URLS=http://+:5202
EXPOSE 5202

# Optional: set DOTNET_ENV if you want (we'll use compose/env for this)
# ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "Deck.Api.dll"]
