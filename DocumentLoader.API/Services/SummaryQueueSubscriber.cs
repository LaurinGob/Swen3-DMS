using DocumentLoader.DAL.Repositories;
using DocumentLoader.Models;
using DocumentLoader.RabbitMQ;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentLoader.API.Services
{
    public class SummaryQueueSubscriber : BackgroundService
    {
        private readonly ILogger<SummaryQueueSubscriber> _logger;
        private readonly IServiceProvider _serviceProvider;

        public SummaryQueueSubscriber(ILogger<SummaryQueueSubscriber> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            RabbitMqSubscriber.Instance.Subscribe(RabbitMqQueues.SUMMARY_QUEUE, async message =>
            {
                _logger.LogInformation($"[API] Received summary message: {message}");
                try
                {
                    var job = JsonSerializer.Deserialize<SummaryResult>(message);
                    if (job == null)
                    {
                        _logger.LogWarning("[API] Received invalid GenAI summary job message.");
                        return;
                    }

                    //Create a scope for each message
                    using var scope = _serviceProvider.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();

                    await repository.UpdateSummaryAsync(job);
                    _logger.LogInformation("[API] Summary updated for DocumentId {docId}", job.DocumentId);
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, "[API] Error processing summary message.");
                }

                await Task.CompletedTask;
            });

            return Task.CompletedTask;
        }
    }
}
