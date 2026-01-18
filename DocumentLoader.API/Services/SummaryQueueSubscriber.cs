using DocumentLoader.DAL.Repositories;
using DocumentLoader.Models;
using DocumentLoader.RabbitMQ;
using Elastic.Clients.Elasticsearch;
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
        private readonly ElasticsearchClient _elasticsearchClient;

        public SummaryQueueSubscriber(ILogger<SummaryQueueSubscriber> logger, IServiceProvider serviceProvider, ElasticsearchClient elasticsearchClient)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _elasticsearchClient = elasticsearchClient;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RabbitMqSubscriber.Instance.SubscribeAsync(RabbitMqQueues.SUMMARY_QUEUE, async message =>
            {
                _logger.LogInformation($"[API] Received summary message: {message}");
                try
                {
                    var result = JsonSerializer.Deserialize<SummaryResult>(message);
                    if (result == null)
                    {
                        _logger.LogWarning("[API] Received invalid GenAI summary job message.");
                        return;
                    }

                    //Create a scope for each message
                    using var scope = _serviceProvider.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();

                    await repository.UpdateSummaryAsync(result);

                    var fullDoc = await repository.GetByIdAsync(result.DocumentId);

                    if (fullDoc != null)
                    {
                        var searchData = new
                        {
                            Id = result.DocumentId,
                            FileName = result.ObjectName,
                            Summary = result.SummaryText,
                            Content = result.RawOcrText,
                            IndexedAt = DateTime.UtcNow,
                            User = new
                            {
                                Username = fullDoc.User?.Username ?? "System"
                            }
                        };
                        await _elasticsearchClient.IndexAsync(searchData, i => i.Index("documents"));
                        _logger.LogInformation("[API] Summary update successful for DocumentId: {DocumentId}", result.DocumentId);
                    }

                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, "[API] Error processing summary message.");
                }

                await Task.CompletedTask;
            });

            try
            {
                //keeps worker alive
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[API] Service wird beendet...");
            }
        }
    }
}
