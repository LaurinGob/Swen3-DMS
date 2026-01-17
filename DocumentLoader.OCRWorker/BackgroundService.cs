using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class OcrWorkerService : BackgroundService
{
    private readonly ILogger<OcrWorkerService> _logger;

    public OcrWorkerService(ILogger<OcrWorkerService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OCR Worker started. Waiting for messages...");

        var factory = new ConnectionFactory()
        {
            HostName = "rabbitmq",
            UserName = "myuser",
            Password = "mypassword"
        };

        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            queue: "ocr_queue",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (sender, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                _logger.LogInformation($"Processing document ID: {message}");

                // Simulate OCR processing
                await Task.Delay(2000, stoppingToken);

                _logger.LogInformation($"OCR processed document {message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing message");
            }
        };

        channel.BasicConsumeAsync(
            queue: "ocr_queue",
            autoAck: true,
            consumer: consumer);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }
}
