using DocumentLoader.DAL;
using DocumentLoader.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Register DbContext with PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

builder.Services.AddDbContext<DocumentDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register repository
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

// Add controllers and Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

// Apply pending migrations automatically
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();
    db.Database.Migrate(); // Ensures the Documents table exists
}

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Serve wwwroot files (upload.html)
app.UseStaticFiles();

// Serve uploaded files from wwwroot/uploads
var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
if (!Directory.Exists(uploadsDir))
    Directory.CreateDirectory(uploadsDir);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsDir),
    RequestPath = "/uploads"
});


app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.UseCors("AllowFrontend");

app.Run();
