using DocumentLoader.RabbitMQ;
using DocumentLoader.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;



namespace DocumentLoader.GenAIWorker
{
    public class GenAIWorkerService : BackgroundService
    {
        private readonly ILogger<GenAIWorkerService> _logger;
        private readonly GeminiService _gemini;

        public GenAIWorkerService(ILogger<GenAIWorkerService> logger, GeminiService gemini)
        {
            _logger = logger;
            _gemini = gemini;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            RabbitMqSubscriber.Instance.Subscribe(
                RabbitMqQueues.RESULT_QUEUE,
                async messageJson =>
                {
                    var ocrResult = JsonSerializer.Deserialize<OcrResult>(messageJson);
                    if (ocrResult == null) return;

                    string prompt = $@"Summarize the following document in a concise way:

                    {ocrResult.OcrText}";

                    string summary = await _gemini.SendPromptAsync(prompt);
                    _logger.LogInformation("Generated summary: {summary}", summary);

                    var summaryResult = new SummaryResult
                    {
                        DocumentId = ocrResult.DocumentId,
                        ObjectName = ocrResult.ObjectName,
                        SummaryText = summary
                    };

                    // TODO: save summary to DB or publish another queue
                    RabbitMqPublisher.Instance.Publish(
                        RabbitMqQueues.SUMMARY_QUEUE,
                        JsonSerializer.Serialize(summaryResult)
                    );

                    _logger.LogInformation("Published summary to SUMMARY_QUEUE for {doc}", ocrResult.ObjectName);
                });

            return Task.CompletedTask;
        }


    }
}
