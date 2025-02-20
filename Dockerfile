# Use the official .NET 8 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Use the .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY DatabaseAPI.csproj ./
RUN dotnet restore "DatabaseAPI.csproj"

# Copy the rest of the application source code and build
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Final runtime image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Set the entrypoint to run the API
ENTRYPOINT ["dotnet", "DatabaseAPI.dll"]
