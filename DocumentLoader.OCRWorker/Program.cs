using DocumentLoader.OCRWorker.Services;
using Minio;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Register MinIO client as a singleton
builder.Services.AddSingleton<IMinioClient>(sp =>
    new MinioClient()
        .WithEndpoint("minio:9000")       // Docker service name + port
        .WithCredentials("minioadmin", "minioadmin")
        .WithSSL(false)                   // set true if using https
        .Build());

// Register OCR worker background service
builder.Services.AddHostedService<OcrWorkerService>();

var app = builder.Build();
await app.RunAsync();
