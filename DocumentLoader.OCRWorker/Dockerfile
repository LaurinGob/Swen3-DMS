# ----------------------------
# Stage 1: Base runtime image
# ----------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS base
WORKDIR /app

# Install Tesseract OCR and dependencies
RUN apt-get update && \
    apt-get install -y --allow-unauthenticated \
        ghostscript \
        libleptonica-dev \
        libtesseract-dev \
    && rm -rf /var/lib/apt/lists/*

# Set Tesseract data path
ENV TESSDATA_PREFIX=/usr/share/tesseract-ocr/5/tessdata/

EXPOSE 80

# ----------------------------
# Stage 2: Build the application
# ----------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["DocumentLoader.OCRWorker/DocumentLoader.OCRWorker.csproj", "DocumentLoader.OCRWorker/"]
RUN dotnet restore "DocumentLoader.OCRWorker/DocumentLoader.OCRWorker.csproj"

# Copy the rest of the source code and build
COPY . .
WORKDIR "/src/DocumentLoader.OCRWorker"
RUN dotnet build "DocumentLoader.OCRWorker.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ----------------------------
# Stage 3: Publish the application
# ----------------------------
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "DocumentLoader.OCRWorker.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# ----------------------------
# Stage 4: Final runtime image
# ----------------------------
FROM base AS final
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Entry point
ENTRYPOINT ["dotnet", "DocumentLoader.OCRWorker.dll"]