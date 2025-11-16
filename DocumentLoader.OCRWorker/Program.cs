using DocumentLoader.OCRWorker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<OcrWorkerService>();

var app = builder.Build();
app.Run();