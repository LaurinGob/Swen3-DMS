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
            // get connection async
            var connection = await RabbitMqConnectionProvider.Instance.GetConnectionAsync();

            // make channel async
            using var channel = await connection.CreateChannelAsync();

            // declare Q async
            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var body = Encoding.UTF8.GetBytes(message);

            //send massegs async
            await channel.BasicPublishAsync(
                exchange: string.Empty, // Default Exchange
                routingKey: queueName,
                body: body
            );
        }
    }
}
