using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading.Tasks;

namespace DocumentLoader.RabbitMQ
{
    public sealed class RabbitMqSubscriber
    {
        private static readonly Lazy<RabbitMqSubscriber> _instance =
            new Lazy<RabbitMqSubscriber>(() => new RabbitMqSubscriber());

        private readonly IConnection _connection;

        private RabbitMqSubscriber()
        {
            _connection = RabbitMqConnectionProvider.Instance.GetConnection();
        }

        public static RabbitMqSubscriber Instance => _instance.Value;

        /// <summary>
        /// Subscribe to a queue with an async handler
        /// </summary>
        public void Subscribe(string queueName, Func<string, Task> onMessageReceived)
        {
            var channel = _connection.CreateModel();

            // Ensure queue exists
            channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var consumer = new EventingBasicConsumer(channel);

            consumer.Received += (model, ea) =>
            {
                Task.Run(async () =>
                {
                    var message = Encoding.UTF8.GetString(ea.Body.ToArray());
    
                    try
                    {
                        await onMessageReceived(message);
                        channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch
                    {
                        // Requeue message on failure
                        channel.BasicNack(ea.DeliveryTag, false, true);
                    }
                });
            };

            channel.BasicConsume(
                queue: queueName,
                autoAck: false,
                consumer: consumer
            );
        }
    }
}
