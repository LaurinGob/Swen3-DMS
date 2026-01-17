using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentLoader.RabbitMQ
{
    public interface IRabbitMqSubscriber
    {
        Task SubscribeAsync(string queueName, Func<string, Task> onMessageReceived);
    }
}
