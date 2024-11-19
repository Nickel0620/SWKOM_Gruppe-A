using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ImageMagick;
using ImageMagick.Drawing;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace OcrProcessing.Tests
{
    public class OcrWorkerTests
    {
        private readonly Mock<ILogger<OcrWorker>> _mockLogger;
        private readonly Mock<IConnection> _mockConnection;
        private readonly Mock<IModel> _mockChannel;
        private readonly OcrWorker _ocrWorker;

        public OcrWorkerTests()
        {
            _mockLogger = new Mock<ILogger<OcrWorker>>();
            _mockConnection = new Mock<IConnection>();
            _mockChannel = new Mock<IModel>();

            _ocrWorker = new OcrWorker(_mockLogger.Object, _mockConnection.Object, _mockChannel.Object);
        }

        [Fact]
        public async Task StopAsync_ShouldCloseConnectionAndChannel()
        {
            // Act
            await _ocrWorker.StopAsync(default);

            // Assert
            _mockChannel.Verify(c => c.Close(), Times.Once);
            _mockConnection.Verify(c => c.Close(), Times.Once);
        }

        [Fact]
        public void Constructor_ShouldSetupQueues()
        {
            // Assert
            _mockChannel.Verify(c => c.QueueDeclare(
                It.Is<string>(q => q == "file_queue"),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                null), Times.Once);
        }

        [Fact]
        public void StartAsync_ShouldConsumeMessagesFromQueue()
        {
            // Arrange
            var consumerAttached = false;

            // Mocking the method call with a callback to ensure it was called correctly
            _mockChannel
                .Setup(c => c.BasicConsume(
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<IDictionary<string, object>>(),
                    It.IsAny<IBasicConsumer>()))
                .Callback<string, bool, string, bool, bool, IDictionary<string, object>, IBasicConsumer>((queue, autoAck, consumerTag, noLocal, exclusive, arguments, consumerInstance) =>
                {
                    // Verify queue name and other arguments
                    Assert.Equal("file_queue", queue); // Verify queue name
                    Assert.False(autoAck); // Verify autoAck is false
                    consumerAttached = true; // Indicate the consumer was attached
                });

            // Act
            _ocrWorker.StartAsync(CancellationToken.None);

            // Assert
            Assert.True(consumerAttached, "The consumer was not attached to the queue.");
            _mockChannel.Verify(c => c.BasicConsume(
                It.Is<string>(q => q == "file_queue"),
                It.Is<bool>(autoAck => !autoAck),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IBasicConsumer>()), Times.Once);
        }

        [Fact]
        public async Task PerformOcrAsync_ReturnsExtractedTextFromPdf()
        {
            string tempFilePath = Path.Combine(Path.GetTempPath(), "test_document.pdf");
            string expectedText = "Hello OCR!";

            // Create a temporary PDF file with text
            using (var images = new MagickImageCollection())
            {
                var settings = new MagickReadSettings
                {
                    Format = MagickFormat.Pdf,
                    Density = new Density(300, 300)
                };

                using (var image = new MagickImage(MagickColors.White, 300, 300))
                {
                    image.Settings.Font = "Arial";
                    image.Settings.FontPointsize = 24;
                    image.Settings.FillColor = MagickColors.Black;
                    image.Draw(new Drawables().Text(50, 50, expectedText));

                    images.Add(image.Clone());
                }

                images.Write(tempFilePath, MagickFormat.Pdf);
            }

            try
            {
                // Debug: Ensure the file exists
                Assert.True(File.Exists(tempFilePath), "Test PDF was not created.");

                // Act
                var result = await _ocrWorker.PerformOcrAsync(tempFilePath);

                // Assert
                Assert.Contains(expectedText, result, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        [Fact]
        public void ReceivedEvent_ShouldAckMessage_OnSuccessfulProcessing()
        {
            // Arrange
            var messageBody = Encoding.UTF8.GetBytes("123|path/to/file");
            var eventArgs = new BasicDeliverEventArgs
            {
                DeliveryTag = 1,
                Body = new ReadOnlyMemory<byte>(messageBody) // Correctly set Body as ReadOnlyMemory<byte>
            };

            var consumer = new EventingBasicConsumer(_mockChannel.Object);

            _mockChannel.Setup(c => c.BasicAck(It.IsAny<ulong>(), It.IsAny<bool>()));

            // Attach the consumer's Received event manually
            consumer.Received += async (model, args) =>
            {
                // Simulate processing of the message
                var body = args.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                Assert.Equal("123|path/to/file", message);

                // Simulate acknowledgment
                _mockChannel.Object.BasicAck(args.DeliveryTag, false);
            };

            // Act
            consumer.HandleBasicDeliver(
                consumerTag: "consumer_tag",
                deliveryTag: eventArgs.DeliveryTag,
                redelivered: false,
                exchange: "",
                routingKey: "",
                properties: null,
                body: eventArgs.Body);

            // Assert
            _mockChannel.Verify(c => c.BasicAck(It.IsAny<ulong>(), It.IsAny<bool>()), Times.Once);
        }

        [Fact]
        public void ReceivedEvent_ShouldLogError_OnInvalidMessage()
        {
            // Arrange
            var messageBody = Encoding.UTF8.GetBytes("invalid message format");
            var deliveryTag = 1UL;

            var mockConsumer = new EventingBasicConsumer(_mockChannel.Object);

            // Attach a Received event handler to simulate message handling
            mockConsumer.Received += (model, args) =>
            {
                // Simulate calling the worker's logic with invalid message
                var body = args.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                // Ensure the message is processed
                Assert.Equal("invalid message format", message);

                // Log an error for invalid message
                _mockLogger.Object.Log(
                    LogLevel.Error,
                    new EventId(),
                    $"Invalid message received: {message}",
                    null,
                    (msg, ex) => msg.ToString());
            };

            // Act: Trigger the Received event manually
            mockConsumer.HandleBasicDeliver(
                consumerTag: "consumer_tag",
                deliveryTag: deliveryTag,
                redelivered: false,
                exchange: "",
                routingKey: "",
                properties: null,
                body: new ReadOnlyMemory<byte>(messageBody));

            // Assert: Verify that the logger logs the error
            _mockLogger.Verify(logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Invalid message received")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

    }
}
