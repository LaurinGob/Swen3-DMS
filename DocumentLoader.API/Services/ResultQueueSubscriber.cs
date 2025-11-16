using DocumentLoader.RabbitMQ;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentLoader.API.Services
{
    public class ResultQueueSubscriber : BackgroundService
    {
        private readonly ILogger<ResultQueueSubscriber> _logger;

        public ResultQueueSubscriber(ILogger<ResultQueueSubscriber> logger)
        {
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            RabbitMqSubscriber.Instance.Subscribe(RabbitMqQueues.RESULT_QUEUE, async message =>
            {
                _logger.LogInformation($"[API] Received OCR result: {message}");
                await Task.CompletedTask;
            });

            return Task.CompletedTask;
        }
    }
}
