using DocumentLoader.GenAIWorker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddSingleton<GeminiService>();          // the API wrapper
        services.AddHostedService<GenAIWorkerService>(); // the worker
    })
    .Build()
    .Run();
