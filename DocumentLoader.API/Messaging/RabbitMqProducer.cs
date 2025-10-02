using RabbitMQ.Client;
using System.Text;

public class RabbitMqProducer
{
    private readonly ConnectionFactory _factory;

    public RabbitMqProducer(IConfiguration config)
    {
        _factory = new ConnectionFactory
        {
            HostName = config["RabbitMQ:Host"] ?? "localhost",
            Port = int.Parse(config["RabbitMQ:Port"] ?? "5672"),
            UserName = config["RabbitMQ:User"] ?? "guest",
            Password = config["RabbitMQ:Password"] ?? "guest"
        };
    }

    /*public void Publish(string queue, string message)
    {
        using var connection = _factory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        var body = Encoding.UTF8.GetBytes(message);
        channel.BasicPublish("", queue, null, body);
    }*/
}
