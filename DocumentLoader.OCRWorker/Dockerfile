# Use the SDK image to build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies first
COPY ["DocumentLoader.OCRWorker.csproj", "./"]
RUN dotnet restore "./DocumentLoader.OCRWorker.csproj"

# Copy the rest of the code
COPY . .

# Build the project
RUN dotnet publish "./DocumentLoader.OCRWorker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Use runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "DocumentLoader.OCRWorker.dll"]
