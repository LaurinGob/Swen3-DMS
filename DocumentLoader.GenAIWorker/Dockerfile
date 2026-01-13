# Base image with .NET runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files
COPY ["DocumentLoader.GenAIWorker/DocumentLoader.GenAIWorker.csproj", "DocumentLoader.GenAIWorker/"]
COPY ["DocumentLoader.RabbitMQ/DocumentLoader.RabbitMQ.csproj", "DocumentLoader.RabbitMQ/"]
COPY ["DocumentLoader.Models/DocumentLoader.Models.csproj", "DocumentLoader.Models/"]

# Restore dependencies
RUN dotnet restore "./DocumentLoader.GenAIWorker/DocumentLoader.GenAIWorker.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/DocumentLoader.GenAIWorker"
RUN dotnet build "./DocumentLoader.GenAIWorker.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "./DocumentLoader.GenAIWorker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app

# Copy published app
COPY --from=publish /app/publish .

# Set environment variables if needed (Gemini API key)
# ENV GEMINI_API_KEY=your_key_here

# Run the worker
ENTRYPOINT ["dotnet", "DocumentLoader.GenAIWorker.dll"]
