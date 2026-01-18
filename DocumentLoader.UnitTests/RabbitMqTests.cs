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
            // Wir biegen die Verbindung für den lokalen Test auf localhost um
            Environment.SetEnvironmentVariable("RabbitMQ__Host", "localhost");
            Environment.SetEnvironmentVariable("RabbitMQ__User", "myuser"); // Dein User aus docker-compose
            Environment.SetEnvironmentVariable("RabbitMQ__Password", "mypassword"); // Dein Passwort
        }
        [Test]
        public async Task RabbitMq_FullRoundtrip_WorksCorrectly()
        {
            // Arrange
            var publisher = new RabbitMqPublisher();
            var subscriber = new RabbitMqSubscriber();
            var testQueue = "integration-test-queue";
            var expectedMessage = "Test-Message-" + Guid.NewGuid();

            // TaskCompletionSource ist wie ein Versprechen: Wir warten, bis die Nachricht ankommt
            var messageReceivedTask = new TaskCompletionSource<string>();

            // Act
            // 1. Subscriber starten
            await subscriber.SubscribeAsync(testQueue, async (msg) =>
            {
                messageReceivedTask.SetResult(msg); // Nachricht erhalten -> Versprechen einlösen
                await Task.CompletedTask;
            });

            // 2. Nachricht senden
            await publisher.PublishAsync(testQueue, expectedMessage);

            // 3. Warten (mit Timeout, falls RabbitMQ nicht antwortet)
            var completedTask = await Task.WhenAny(messageReceivedTask.Task, Task.Delay(5000));

            // Assert
            Assert.That(completedTask, Is.EqualTo(messageReceivedTask.Task), "Timeout: Nachricht kam nicht an!");
            Assert.That(await messageReceivedTask.Task, Is.EqualTo(expectedMessage));
        }

    }
}
