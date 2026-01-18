using DocumentLoader.OCRWorker.Services;
using DocumentLoader.RabbitMQ;
using Minio;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();


builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
builder.Services.AddSingleton<IRabbitMqSubscriber, RabbitMqSubscriber>();

// register MinIO client
builder.Services.AddSingleton<IMinioClient>(sp =>
    new MinioClient()
        .WithEndpoint("minio", 9000)       
        .WithCredentials("minioadmin", "minioadmin")
        .WithSSL(false)                   
        .Build());




// register OCR worker 
builder.Services.AddHostedService<OcrWorkerService>();

var app = builder.Build();
await app.RunAsync();
