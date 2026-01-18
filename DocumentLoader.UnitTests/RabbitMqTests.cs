using DocumentLoader.RabbitMQ;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentLoader.UnitTests
{
    internal class RabbitMqTests
    {
        [SetUp]
        public void SetUp()
        {
            //connection changed to localhost for tests
            Environment.SetEnvironmentVariable("RabbitMQ__Host", "localhost");
            Environment.SetEnvironmentVariable("RabbitMQ__User", "myuser");
            Environment.SetEnvironmentVariable("RabbitMQ__Password", "mypassword");
        }
        [Test]
        public async Task RabbitMq_FullRoundtrip_WorksCorrectly()
        {
            // Arrange
            var publisher = new RabbitMqPublisher();
            var subscriber = new RabbitMqSubscriber();
            var testQueue = "integration-test-queue";
            var expectedMessage = "Test-Message-" + Guid.NewGuid();

            // wait until message arrives
            var messageReceivedTask = new TaskCompletionSource<string>();

            // Act
            //start subscriber
            await subscriber.SubscribeAsync(testQueue, async (msg) =>
            {
                messageReceivedTask.SetResult(msg);
                await Task.CompletedTask;
            });

            //send message
            await publisher.PublishAsync(testQueue, expectedMessage);

            // wait and timeout if rabbitmq fails
            var completedTask = await Task.WhenAny(messageReceivedTask.Task, Task.Delay(5000));

            // Assert
            Assert.That(completedTask, Is.EqualTo(messageReceivedTask.Task), "Timeout: Nachricht kam nicht an!");
            Assert.That(await messageReceivedTask.Task, Is.EqualTo(expectedMessage));
        }

    }
}
