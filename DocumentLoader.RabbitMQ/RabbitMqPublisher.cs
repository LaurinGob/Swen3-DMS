using RabbitMQ.Client;
using System.Text;

namespace DocumentLoader.RabbitMQ
{
    public sealed class RabbitMqPublisher : IRabbitMqPublisher
    {
        private static readonly Lazy<RabbitMqPublisher> _instance =
            new Lazy<RabbitMqPublisher>(() => new RabbitMqPublisher());

        public RabbitMqPublisher() { }

        public static RabbitMqPublisher Instance => _instance.Value;

        public async Task PublishAsync(string queueName, string message)
        {
            // 1. Connection asynchron holen
            var connection = await RabbitMqConnectionProvider.Instance.GetConnectionAsync();

            // 2. Channel asynchron erstellen (IChannel statt IModel in v7)
            using var channel = await connection.CreateChannelAsync();

            // 3. Queue asynchron deklarieren
            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var body = Encoding.UTF8.GetBytes(message);

            // 4. Nachricht asynchron senden
            // In v7 sind die Parameter für BasicPublishAsync etwas direkter
            await channel.BasicPublishAsync(
                exchange: string.Empty, // Default Exchange
                routingKey: queueName,
                body: body
            );
        }
    }
}
