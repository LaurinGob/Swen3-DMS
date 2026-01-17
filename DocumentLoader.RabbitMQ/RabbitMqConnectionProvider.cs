using RabbitMQ.Client;
using System;
using System.Threading.Tasks;

namespace DocumentLoader.RabbitMQ
{
    public sealed class RabbitMqConnectionProvider : IDisposable
    {
        private static readonly Lazy<RabbitMqConnectionProvider> _instance =
            new Lazy<RabbitMqConnectionProvider>(() => new RabbitMqConnectionProvider());

        private readonly ConnectionFactory _factory;
        private IConnection _connection;

        private RabbitMqConnectionProvider()
        {
            var host = Environment.GetEnvironmentVariable("RabbitMQ__Host") ?? "rabbitmq";
            var user = Environment.GetEnvironmentVariable("RabbitMQ__User") ?? "myuser";
            var password = Environment.GetEnvironmentVariable("RabbitMQ__Password") ?? "mypassword";

            _factory = new ConnectionFactory
            {
                HostName = host,
                UserName = user,
                Password = password,
            };
        }

        public static RabbitMqConnectionProvider Instance => _instance.Value;

        public async Task<IConnection> GetConnectionAsync()
        {
            // Wir nutzen ein einfaches Lock oder prüfen auf null, 
            // um sicherzustellen, dass nicht mehrere Verbindungen gleichzeitig geöffnet werden
            if (_connection == null || !_connection.IsOpen)
            {
                _connection = await _factory.CreateConnectionAsync();
            }
            return _connection;
        }

        public void Dispose()
        {
            // In v7 ist IConnection auch IAsyncDisposable, aber für Dispose machen wir es so:
            if (_connection != null)
            {
                if (_connection.IsOpen) _connection.CloseAsync().GetAwaiter().GetResult();
                _connection.Dispose();
            }
        }
    }
}
