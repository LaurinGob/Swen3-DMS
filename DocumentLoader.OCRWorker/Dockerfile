# ----------------------------
# Stage 1: Base runtime image
# ----------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS base
WORKDIR /app

# Install Tesseract OCR, Poppler (PDF → image), ImageMagick, and dependencies
RUN apt-get update && \
    apt-get install -y --allow-unauthenticated \
        imagemagick \
        poppler-utils \
        libleptonica-dev \
        libtesseract-dev \
        tesseract-ocr \
        tesseract-ocr-eng \
        wget \
        ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Create folder for Tesseract.NET symlinks
RUN mkdir -p /app/x64 \
    && ln -sf /usr/lib/x86_64-linux-gnu/liblept.so.5 /app/x64/libleptonica-1.82.0.so \
    && ln -sf /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /app/x64/libtesseract50.so

# Set environment variables for Tesseract.NET
ENV TESSDATA_PREFIX=/usr/share/tesseract-ocr/5/tessdata/
ENV LD_LIBRARY_PATH=/app/x64:/usr/lib/x86_64-linux-gnu:$LD_LIBRARY_PATH

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

# Copy published app from previous stage
COPY --from=publish /app/publish .

# Entry point
ENTRYPOINT ["dotnet", "DocumentLoader.OCRWorker.dll"]
