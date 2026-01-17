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

        // Wir entfernen die feste _connection im Konstruktor, 
        // da GetConnectionAsync nun await erfordert.
        public RabbitMqSubscriber() { }

        public static RabbitMqSubscriber Instance => _instance.Value;

        /// <summary>
        /// Subscribe to a queue with a native async handler (RabbitMQ v7)
        /// </summary>
        public async Task SubscribeAsync(string queueName, Func<string, Task> onMessageReceived)
        {
            // 1. Connection asynchron holen
            var connection = await RabbitMqConnectionProvider.Instance.GetConnectionAsync();

            // 2. Kanal erstellen (IChannel in v7)
            var channel = await connection.CreateChannelAsync();

            // 3. Queue deklarieren
            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            // 4. Den asynchronen Consumer nutzen
            var consumer = new AsyncEventingBasicConsumer(channel);

            // In v7 nutzen wir das ReceivedAsync Event
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                try
                {
                    // Business Logic ausführen
                    await onMessageReceived(message);

                    // Bestätigung senden
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception)
                {
                    // Bei Fehler zurück in die Warteschlange (requeue: true)
                    await channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            // 5. Konsumierung starten
            await channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumer: consumer
            );
        }
    }
}
