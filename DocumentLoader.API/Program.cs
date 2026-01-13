using DocumentLoader.API.Services;
using DocumentLoader.DAL;
using DocumentLoader.DAL.Repositories;
using Minio;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// -------------------------------
// Register DbContext with PostgreSQL
// -------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 104_857_600; // 100 MB
});

builder.Services.AddDbContext<DocumentDbContext>(options =>
    options.UseNpgsql(connectionString));

// -------------------------------
// Register repository
// -------------------------------
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

// -------------------------------
// Register MinIO client singleton
// -------------------------------
builder.Services.AddSingleton<MinioClient>(sp =>
{
    var endpoint = builder.Configuration["Minio:Endpoint"] ?? "localhost:9000";
    var accessKey = builder.Configuration["Minio:AccessKey"] ?? "minioadmin";
    var secretKey = builder.Configuration["Minio:SecretKey"] ?? "minioadmin";

    return (MinioClient)new MinioClient()
        .WithEndpoint(endpoint)
        .WithCredentials(accessKey, secretKey)
        .Build();
});

// -------------------------------
// Add controllers and Swagger
// -------------------------------
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// -------------------------------
// Background service for Result_Queue
// -------------------------------
builder.Services.AddHostedService<SummaryQueueSubscriber>();

// -------------------------------
// CORS
// -------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

// -------------------------------
// Apply pending migrations automatically
// -------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();
    db.Database.Migrate();
}

// -------------------------------
// Swagger (development only)
// -------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// -------------------------------
// HTTPS & Authorization
// -------------------------------
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.UseCors("AllowFrontend");

app.Run();
