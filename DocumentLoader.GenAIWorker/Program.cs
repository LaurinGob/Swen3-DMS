using DocumentLoader.GenAIWorker;
using DocumentLoader.RabbitMQ; // Wichtig für die Interfaces
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Logging konfigurieren
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// 1. RabbitMQ Komponenten registrieren (Interface zu Klasse)
// Ohne diese Zeilen kann der GenAIWorkerService nicht starten!
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
builder.Services.AddSingleton<IRabbitMqSubscriber, RabbitMqSubscriber>();

// 2. GeminiService registrieren
builder.Services.AddSingleton<GeminiService>();

// 3. Den Worker registrieren
builder.Services.AddHostedService<GenAIWorkerService>();

var app = builder.Build();
await app.RunAsync();