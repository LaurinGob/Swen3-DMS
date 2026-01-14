using DocumentLoader.BatchProcessing;
using DocumentLoader.DAL;
using DocumentLoader.Models;
using DocumentLoader.DAL.Repositories;
using DocumentLoader.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Batch Processing Demo ===");

        // --- 1) Setup DI ---
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(config =>
        {
            config.AddConsole();
            config.SetMinimumLevel(LogLevel.Debug);
        });

        // DbContext
        services.AddDbContext<DocumentDbContext>(options =>
            options.UseNpgsql("Your_Postgres_Connection_String"));

        // Repository
        services.AddScoped<IAccessLogRepository, AccessLogRepository>();

        // Batch processor
        services.AddScoped<AccessLogBatchProcessor>();

        services.AddScoped<IAccessLogService, AccessLogService>();


        // Build service provider
        var serviceProvider = services.BuildServiceProvider();

        // --- 2) Resolve processor ---
        var processor = serviceProvider.GetRequiredService<AccessLogBatchProcessor>();

        // --- 3) Determine mode ---
        var mode = args.FirstOrDefault()?.ToLowerInvariant() ?? "run-once";

        // Configure folders and file pattern
        var basePath = AppContext.BaseDirectory;
        var inputFolder = Path.Combine(basePath, "input");
        var archiveFolder = Path.Combine(basePath, "archive");
        var errorFolder = Path.Combine(basePath, "error");
        var filePattern = "access-*.xml";

        // Batch processor
        services.AddScoped(sp =>
        {
            var sink = sp.GetRequiredService<IAccessLogSink>();
            return new AccessLogBatchProcessor(
                inputFolder,
                archiveFolder,
                errorFolder,
                filePattern,
                sink);
        });


        switch (mode)
        {
            case "run-once":
                await processor.RunOnceAsync();
                break;

            case "quartz":
                var cron = "0/10 * * * * ?"; // every 10 seconds
                await RunQuartzAsync(processor, cron);
                break;

            default:
                Console.WriteLine("Unknown mode. Use: run-once | quartz");
                break;
        }

        Console.WriteLine("Batch processing finished.");
    }

    private static async Task RunQuartzAsync(AccessLogBatchProcessor processor, string cronExpression)
    {
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
