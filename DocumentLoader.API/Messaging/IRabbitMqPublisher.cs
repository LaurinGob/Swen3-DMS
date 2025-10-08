using DocumentLoader.Models;

namespace DocumentLoader.API.Messaging
{
    public interface IRabbitMqPublisher
    {
        Task PublishDocumentUploadedAsync(Document document);
    }
}
