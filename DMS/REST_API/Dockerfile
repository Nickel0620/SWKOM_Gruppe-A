# Use the ASP.NET runtime image as the base
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Use the .NET SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy the rest of the project files
COPY . .

# Restore dependencies for the solution
WORKDIR /src/REST_API
RUN dotnet restore

# Build the REST_API project
RUN dotnet build -c $BUILD_CONFIGURATION -o /app/build

# Publish the project to /app/publish
RUN dotnet publish -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Use the base image to run the application
FROM base AS final
WORKDIR /app

# Copy the published files from the build stage
COPY --from=build /app/publish .

# Entry point to run the application
ENTRYPOINT ["dotnet", "REST_API.dll"]