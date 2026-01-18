using DocumentLoader.GenAIWorker;
using DocumentLoader.RabbitMQ; // Wichtig für die Interfaces
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
builder.Services.AddSingleton<IRabbitMqSubscriber, RabbitMqSubscriber>();

// register geminiservice
builder.Services.AddSingleton<GeminiService>();

// register worker service
builder.Services.AddHostedService<GenAIWorkerService>();

var app = builder.Build();
await app.RunAsync();