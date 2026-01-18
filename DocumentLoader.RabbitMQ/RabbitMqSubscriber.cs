using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading.Tasks;

namespace DocumentLoader.RabbitMQ
{
    public sealed class RabbitMqSubscriber : IRabbitMqSubscriber
    {
        private static readonly Lazy<RabbitMqSubscriber> _instance =
            new Lazy<RabbitMqSubscriber>(() => new RabbitMqSubscriber());

        public RabbitMqSubscriber() { }

        public static RabbitMqSubscriber Instance => _instance.Value;

        public async Task SubscribeAsync(string queueName, Func<string, Task> onMessageReceived)
        {
            // get conn async
            var connection = await RabbitMqConnectionProvider.Instance.GetConnectionAsync();

            // create channel async
            var channel = await connection.CreateChannelAsync();

            //declare queue async
            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                try
                {
                    await onMessageReceived(message);

                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception)
                {
                    await channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            await channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumer: consumer
            );
        }
    }
}
