# Swen3-DMS (Document Management System)

Ein modernes Dokumenten-Management-System mit automatischer OCR-Verarbeitung, KI-Zusammenfassungen und Volltextsuche.

## Features

* **Document Upload:** Speicherung in MinIO (S3-kompatibler Speicher).
* **User Management:** Automatische Zuordnung von Dokumenten zu Erstellern.
* **AI Summary:** Automatisierte Zusammenfassungen via RabbitMQ-Worker.
* **Volltextsuche:** Indizierung und Suche über Elasticsearch.
* **Analytics:** Batch-Verarbeitung von Zugriffszahlen mittels Quartz.NET.

## Tech Stack

* **Backend:** .NET 8 (ASP.NET Core Web API)
* **Datenbank:** PostgreSQL (via Entity Framework Core)
* **Messaging:** RabbitMQ
* **Search Engine:** Elasticsearch
* **Object Storage:** MinIO
* **Frontend:** HTML5, CSS3, Vanilla JavaScript (Modernes Responsive Design)

##  Installation & Setup

### Voraussetzungen
* Docker & Docker Desktop
* .NET 8 SDK
* Visual Studio 2022 (optional)

### Batch Processing starten
Zuerst Batch Files generieren
```bash
docker compose build batch-gen
```
### Schritt 1: Infrastruktur starten
Starte die benötigten Dienste (Datenbank, RabbitMQ, Elastic, MinIO) über Docker Compose:
```bash
docker compose build --no-cache
docker-compose up -d --force-recreate 




