namespace DocumentLoader.RabbitMQ
{
    public interface IRabbitMqPublisher
    {
        Task PublishAsync(string queueName, string message);
    }
}