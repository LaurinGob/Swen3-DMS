using RabbitMQ.Client;
using System;

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
                DispatchConsumersAsync = true
            };

            _connection = _factory.CreateConnection();
        }

        public static RabbitMqConnectionProvider Instance => _instance.Value;

        public IConnection GetConnection()
        {
            if (_connection == null || !_connection.IsOpen)
            {
                _connection = _factory.CreateConnection();
            }
            return _connection;
        }

        public void Dispose()
        {
            if (_connection != null)
            {
                if (_connection.IsOpen) _connection.Close();
                _connection.Dispose();
            }
        }
    }
}
