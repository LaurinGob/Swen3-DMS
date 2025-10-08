using DocumentLoader.API.Messaging;
using DocumentLoader.Models;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace DocumentLoader.API.Messaging
{
    public class RabbitMqPublisher : IRabbitMqPublisher
    {
        private readonly ConnectionFactory _factory;

        public RabbitMqPublisher(IConfiguration configuration)
        {
            _factory = new ConnectionFactory()
            {
                HostName = configuration["RabbitMq:Host"] ?? "rabbitmq",
                UserName = configuration["RabbitMq:User"] ?? "guest",
                Password = configuration["RabbitMq:Pass"] ?? "guest",
                Port = 5672
            };
        }

        public async Task PublishDocumentUploadedAsync(Document document)
        {
            using var connection = await _factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: "document_uploaded",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var message = JsonSerializer.Serialize(new
            {
                DocumentId = document.Id,
                FileName = document.FileName,
                FilePath = document.FilePath,
                UploadedAt = document.UploadedAt
            });

            var body = Encoding.UTF8.GetBytes(message);

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: "document_uploaded",
                body: body);
        }
    }
}
