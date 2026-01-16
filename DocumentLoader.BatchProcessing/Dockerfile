# Stage 1: Build
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Stage 2: Build (Use the correct SDK image)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src


# Copy the project files for restore (paths relative to build context)
COPY ["DocumentLoader.BatchProcessing/DocumentLoader.BatchProcessing.csproj", "DocumentLoader.BatchProcessing/"]
COPY ["DocumentLoader.Models/DocumentLoader.Models.csproj", "DocumentLoader.Models/"]
COPY ["DocumentLoader.DAL/DocumentLoader.DAL.csproj", "DocumentLoader.DAL/"]
COPY ["DocumentLoader.Core/DocumentLoader.Core.csproj", "DocumentLoader.Core/"]
COPY ["DocumentLoader.API/DocumentLoader.API.csproj", "DocumentLoader.API/"]
# Restore only dependencies
RUN dotnet restore "./DocumentLoader.BatchProcessing/DocumentLoader.BatchProcessing.csproj"

# Copy everything else
COPY . .

# Build & publish the batch processor
WORKDIR "/src/DocumentLoader.BatchProcessing"
RUN dotnet build "./DocumentLoader.BatchProcessing.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "./DocumentLoader.BatchProcessing.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# Copy the published app
COPY --from=publish /app/publish .

# Create folders for batch processing
RUN mkdir -p /app/BatchInput /app/BatchArchive /app/BatchError

# Environment variables
ENV BATCH_INPUT_PATH=/app/BatchInput
ENV BATCH_ARCHIVE_PATH=/app/BatchArchive
ENV BATCH_ERROR_PATH=/app/BatchError
ENV ConnectionStrings__DefaultConnection="Host=db;Database=mydatabase;Username=myuser;Password=mypassword"

# Run batch processor once on startup
ENTRYPOINT ["dotnet", "DocumentLoader.BatchProcessing.dll"]
CMD ["run-once"]
