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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RabbitMqSubscriber.Instance.SubscribeAsync(
                RabbitMqQueues.RESULT_QUEUE,
                async messageJson =>
                {
                    var ocrResult = JsonSerializer.Deserialize<OcrResult>(messageJson);
                    if (ocrResult == null) return;

                    string prompt = $@"Summarize the following document in a concise way:

                    {ocrResult.OcrText}";

                    string summary = "";

                    try
                    {
                        summary = await _gemini.SendPromptAsync(prompt, maxRetries: 5);
                        if (string.IsNullOrWhiteSpace(summary))
                        {
                            _logger.LogWarning("[GenAIWorker] Gemini returned empty summary for {doc}", ocrResult.ObjectName);
                        }
                        else
                        {
                            _logger.LogInformation("[GenAIWorker] Generated summary for {doc}: {summary}", ocrResult.ObjectName, summary);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[GenAIWorker] Summary generation failed for {doc}", ocrResult.ObjectName);
                        return;
                    }

                    var summaryResult = new SummaryResult
                    {
                        DocumentId = ocrResult.DocumentId,
                        ObjectName = ocrResult.ObjectName,
                        SummaryText = summary
                    };

                    // Publish to summary queue
                    await RabbitMqPublisher.Instance.PublishAsync(
                        RabbitMqQueues.SUMMARY_QUEUE,
                        JsonSerializer.Serialize(summaryResult)
                    );

                    _logger.LogInformation("[GenAIWorker] Published summary to SUMMARY_QUEUE for {doc}", ocrResult.ObjectName);
                });

            try
            {
                // Hält den Worker am Leben, bis das System ihn stoppt
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[GenAIWorker] Service wird beendet...");
            }
        }
    }
}
