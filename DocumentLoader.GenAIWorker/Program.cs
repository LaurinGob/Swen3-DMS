using DocumentLoader.GenAIWorker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        // Register GeminiService with logger injection
        services.AddSingleton<GeminiService>(sp =>
            new GeminiService(sp.GetRequiredService<ILogger<GeminiService>>()));

        services.AddHostedService<GenAIWorkerService>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .Build()
    .Run();
