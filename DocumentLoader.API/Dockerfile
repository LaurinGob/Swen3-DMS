# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base


# Install curl for Docker healthchecks
USER root
RUN apt-get update \
    && apt-get install -y curl \
    && rm -rf /var/lib/apt/lists/*

USER $APP_UID
WORKDIR /app


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Copy ALL project files that are referenced
COPY ["DocumentLoader.API/DocumentLoader.API.csproj", "DocumentLoader.API/"]
COPY ["DocumentLoader.DAL/DocumentLoader.DAL.csproj", "DocumentLoader.DAL/"]
COPY ["DocumentLoader.Models/DocumentLoader.Models.csproj", "DocumentLoader.Models/"]
COPY ["DocumentLoader.RabbitMQ/DocumentLoader.RabbitMQ.csproj", "DocumentLoader.RabbitMQ/"]
RUN dotnet restore "./DocumentLoader.API/DocumentLoader.API.csproj"
COPY . .
WORKDIR "/src/DocumentLoader.API"
RUN dotnet build "./DocumentLoader.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
RUN dotnet publish "./DocumentLoader.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

EXPOSE 80

ENTRYPOINT ["dotnet", "DocumentLoader.API.dll"]