# Use the official .NET SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY *.sln ./
COPY *.csproj ./
COPY . ./

# Restore dependencies
RUN dotnet restore

# Build and publish the application
RUN dotnet publish -c Release -o /app/publish

# Use the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "DatabaseAPI.dll"]
