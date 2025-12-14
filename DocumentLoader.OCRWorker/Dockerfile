# ----------------------------
# Stage 1: Runtime base image
# ----------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS base
WORKDIR /app

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        poppler-utils \
        tesseract-ocr \
        tesseract-ocr-eng \
        liblept5 \
        libtesseract5 \
        ca-certificates \
        libc6-dev \
    && rm -rf /var/lib/apt/lists/*

RUN ln -sf /lib/x86_64-linux-gnu/libdl.so.2 /lib/x86_64-linux-gnu/libdl.so


ENV TESSDATA_PREFIX=/usr/share/tesseract-ocr/5/tessdata/

# ----------------------------
# Stage 2: Build
# ----------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["DocumentLoader.OCRWorker/DocumentLoader.OCRWorker.csproj", "DocumentLoader.OCRWorker/"]
COPY ["DocumentLoader.Models/DocumentLoader.Models.csproj", "DocumentLoader.Models/"]
COPY ["DocumentLoader.RabbitMQ/DocumentLoader.RabbitMQ.csproj", "DocumentLoader.RabbitMQ/"]
RUN dotnet restore "DocumentLoader.OCRWorker/DocumentLoader.OCRWorker.csproj"

COPY . .
WORKDIR /src/DocumentLoader.OCRWorker
RUN dotnet build "DocumentLoader.OCRWorker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR /src/DocumentLoader.OCRWorker
RUN dotnet publish "DocumentLoader.OCRWorker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ----------------------------
# Stage 3: Final runtime image
# ----------------------------
FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .
COPY DocumentLoader.OCRWorker/entrypoint.sh /entrypoint.sh
RUN ls -la /entrypoint.sh && chmod +x /entrypoint.sh


ENTRYPOINT ["/entrypoint.sh"]
