# Use the official .NET SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY DatabaseAPI.sln ./
COPY DatabaseAPI.csproj ./

# Restore dependencies
RUN dotnet restore DatabaseAPI.csproj

# Copy the remaining files
COPY . ./

# Build and publish the application
RUN dotnet publish DatabaseAPI.csproj -c Release -o /app/publish

# Use the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "DatabaseAPI.dll"]
