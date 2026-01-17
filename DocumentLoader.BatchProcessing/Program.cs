using DocumentLoader.BatchProcessing;
using DocumentLoader.Core;
using DocumentLoader.Core.Services;
using DocumentLoader.DAL;
using DocumentLoader.DAL.Repositories;
using DocumentLoader.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using System.Xml.Linq;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Batch Processing Demo ===");
        

        var basePath = AppContext.BaseDirectory;


        var inputFolder = Environment.GetEnvironmentVariable("BATCH_INPUT_PATH") ?? Path.Combine(AppContext.BaseDirectory, "BatchInput");
        var archiveFolder = Environment.GetEnvironmentVariable("BATCH_ARCHIVE_PATH") ?? Path.Combine(AppContext.BaseDirectory, "BatchArchive");
        var errorFolder = Path.Combine(Environment.GetEnvironmentVariable("BATCH_ERROR_PATH") ?? Path.Combine(AppContext.BaseDirectory, "BatchError"));
        
        Directory.CreateDirectory(inputFolder);
        Directory.CreateDirectory(archiveFolder);
        Directory.CreateDirectory(errorFolder);

        Console.WriteLine($"Input Folder: {inputFolder}");
        Console.WriteLine($"Archive Folder: {archiveFolder}");
        Console.WriteLine($"Error Folder: {errorFolder}");

        //var projectRoot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
        //var inputFolder = Path.Combine(projectRoot, "BatchInput");
        //var archiveFolder = Path.Combine(projectRoot, "BatchArchive");
        //var errorFolder = Path.Combine(projectRoot, "BatchError");

        // --- 1) Setup DI ---
        var services = new ServiceCollection();

        services.AddCoreServices();

        // Logging
        services.AddLogging(config =>
        {
            config.AddConsole();
            config.SetMinimumLevel(LogLevel.Debug);
        });

        // DbContext
        services.AddDbContext<DocumentDbContext>(options =>
            options.UseNpgsql(
                Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"))
             );


        // Repository
        services.AddTransient<IAccessLogRepository, AccessLogRepository>();
        services.AddTransient<IDocumentRepository, DocumentRepository>();


        // Batch processor

        var apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:5000/";

        services.AddHttpClient<IAccessLogSink, AccessLogApiSink>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
        });


        services.AddTransient<IAccessLogService, AccessLogService>();

        services.AddTransient<AccessLogBatchProcessor>(sp =>
        {
            return new AccessLogBatchProcessor(
                inputFolder,
                archiveFolder,
                errorFolder,
                filePattern: "access-*.xml",
                sink: sp.GetRequiredService<IAccessLogSink>(),
                logger: sp.GetRequiredService<ILogger<AccessLogBatchProcessor>>()
                );
        });

        // Build service provider
        var serviceProvider = services.BuildServiceProvider();

        // --- 2) Resolve processor ---
        var processor = serviceProvider.GetRequiredService<AccessLogBatchProcessor>();

        // --- 3) Determine mode ---
        var mode = args.FirstOrDefault()?.ToLowerInvariant() ?? "run-once";

        switch (mode)
        {
            case "run-once":
                Console.WriteLine("Running batch processor once...");
                await processor.RunOnceAsync();
                break;

            case "quartz":
                Console.WriteLine("Running batch processor with Quartz scheduler...");
                var cron = "0/10 * * * * ?"; // every 2 minutes
                await RunQuartzAsync(processor, cron);
                break;

            case "gen":
                GenerateSampleFiles(inputFolder);
                break;

            default:
                Console.WriteLine("Unknown mode. Use: run-once | quartz");
                break;
        }

        Console.WriteLine("Batch processing finished.");
    }

    private static void GenerateSampleFiles(string inputFolder)
    {
        Random rnd = new Random();

        // create definitely valid file -> archive
        Directory.CreateDirectory(inputFolder);
        var validIds = new[] { 5, 13, 14, 23, 29, 30 };
        var goldenName = $"access-SUCCESS-{DateTime.Today:yyyy-MM-dd}.xml";

        var today = DateOnly.FromDateTime(DateTime.Today);

        var goldenEntries = Enumerable.Range(1, 6).Select(i => new
        {
            Id = validIds[i % validIds.Length], // Use exactly your valid IDs
            Count = rnd.Next(10, 50)
        }).ToList();

        var goldenDoc = new XDocument(
        new XElement("accessLogBatch",
            new XAttribute("batchDate", today.ToString("yyyy-MM-dd")),
            goldenEntries.Select(e => new XElement("entry",
                new XAttribute("documentId", e.Id),
                new XAttribute("accessCount", e.Count)))
        )
    );
        goldenDoc.Save(Path.Combine(inputFolder, goldenName));
        Console.WriteLine($"Created GOLDEN file (Should Archive): {goldenName}");

        // 1) create 3 random (maybe partly valid) batch files
        for (int i = 0; i < 3; i++)
        {
            var batchDate = today.AddDays(-i); // today, yesterday, the day before

            // Use a timestamp to make each file name unique, so files in the archive
            // are not overwritten when we generate again.
            var fileName = $"access-{batchDate:yyyy-MM-dd}.xml";
            var fullPath = Path.Combine(inputFolder, fileName);

            // Create some fake entries
            var fakeEntries = Enumerable.Range(1, 5).Select(n => new
            {
                // Use integers because your Processor uses int.TryParse
                DocumentId = rnd.Next(1, 40),
                AccessCount = rnd.Next(1, 50)   
            }).ToList();
            Console.WriteLine($"{fakeEntries}");

            // Build the XML document (valid format)
            var xdoc = new XDocument(
               new XElement("accessLogBatch",
                   new XAttribute("batchDate", batchDate.ToString("yyyy-MM-dd")),
                   fakeEntries.Select(e =>
                       new XElement("entry",
                           new XAttribute("documentId", e.DocumentId),
                           new XAttribute("accessCount", e.AccessCount)
                       )
                   )
               )
           );

            xdoc.Save(fullPath);
            Console.WriteLine($"Created random file: {fileName}");
        }

        // 2) create ONE INVALID file on purpose to demonstrate error handling
        var invalidStamp = DateTime.Today.ToString("HHmmssfff");
        var invalidName = $"demo-invalid-{invalidStamp}.xml";
        var invalidPath = Path.Combine(inputFolder, invalidName);

        // This XML violates multiple validation rules:
        // - batchDate is not a valid date
        // - documentId is not a GUID
        // - accessCount is not an integer
        var invalidXml = new XDocument(
            new XElement("accessLogBatch",
                new XAttribute("batchDate", "NOT-A-DATE"),
                new XElement("entry",
                    new XAttribute("documentId", "NOT-A-GUID"),
                    new XAttribute("accessCount", "abc")
                )
            )
        );

        invalidXml.Save(invalidPath);
        Console.WriteLine($"Created INVALID file (should go to error): {invalidName}");

        Console.WriteLine();
        Console.WriteLine("Sample files generated.");
    }

    private static async Task RunQuartzAsync(AccessLogBatchProcessor processor, string cronExpression)
    {

        Console.WriteLine("Starting Quartz scheduler...");
        StdSchedulerFactory factory = new StdSchedulerFactory();
        var scheduler = await factory.GetScheduler();

        var jobData = new JobDataMap { { "processor", processor } };

        var job = JobBuilder.Create<AccessBatchJob>()
            .WithIdentity("accessJob")
            .UsingJobData(jobData)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity("accessTrigger")
            .WithCronSchedule(cronExpression)
            .Build();

        await scheduler.ScheduleJob(job, trigger);
        await scheduler.Start();

        Console.WriteLine($"Quartz scheduler started. Cron: {cronExpression}");
        await Task.Delay(-1); // Keep the app running
    }
}
