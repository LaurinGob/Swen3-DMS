using RabbitMQ.Client;
using System.Text;

namespace DocumentLoader.RabbitMQ
{
    public sealed class RabbitMqPublisher
    {
        private static readonly Lazy<RabbitMqPublisher> _instance =
            new Lazy<RabbitMqPublisher>(() => new RabbitMqPublisher());

        private readonly IConnection _connection;

        private RabbitMqPublisher()
        {
            _connection = RabbitMqConnectionProvider.Instance.GetConnection();
        }

        public static RabbitMqPublisher Instance => _instance.Value;

        public void Publish(string queueName, string message)
        {
            using var channel = _connection.CreateModel();

            // Ensure the queue exists
            channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var body = Encoding.UTF8.GetBytes(message);

            channel.BasicPublish(
                exchange: "",
                routingKey: queueName,
                basicProperties: null,
                body: body
            );
        }
    }
}
