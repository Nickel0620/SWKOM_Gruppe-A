# Use the official .NET runtime image for .NET 8.0
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the SDK image for building the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project files
COPY ["DAL/DAL.csproj", "DAL/"]

# Restore dependencies
RUN dotnet restore "DAL/DAL.csproj"

# Copy the entire project and build it
COPY . .
WORKDIR "/src/DAL"
RUN dotnet build "DAL.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "DAL.csproj" -c Release -o /app/publish

# Build the runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DAL.dll"]
