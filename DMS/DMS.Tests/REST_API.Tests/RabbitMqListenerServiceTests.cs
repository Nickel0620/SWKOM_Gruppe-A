using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using REST_API.Services;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DMS.Tests.REST_API.Tests
{
    public class RabbitMqListenerServiceTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IConnection> _connectionMock;
        private readonly Mock<IModel> _channelMock;
        private readonly Mock<ILogger<RabbitMqListenerService>> _loggerMock;
        private readonly RabbitMqListenerService _listenerService;

        public RabbitMqListenerServiceTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _connectionMock = new Mock<IConnection>();
            _channelMock = new Mock<IModel>();
            _loggerMock = new Mock<ILogger<RabbitMqListenerService>>();

            // Mock the connection to return the mocked channel
            _connectionMock.Setup(c => c.CreateModel()).Returns(_channelMock.Object);

            // Inject mocks into the service
            _listenerService = new RabbitMqListenerService(_httpClientFactoryMock.Object, _loggerMock.Object, _connectionMock.Object);
        }

        [Fact]
        public async Task StartAsync_ConnectsToRabbitMQAndStartsListening()
        {
            // Arrange
            _channelMock.Setup(c => c.QueueDeclare("ocr_result_queue", false, false, false, null));

            // Act
            await _listenerService.StartAsync(CancellationToken.None);

            // Assert
            _channelMock.Verify(c => c.QueueDeclare("ocr_result_queue", false, false, false, null), Times.Once);
        }

        [Fact]
        public void Received_ValidMessage_ProcessesSuccessfully()
        {
            // Arrange
            var consumer = new EventingBasicConsumer(_channelMock.Object);
            var messageBody = Encoding.UTF8.GetBytes("1|Test OCR Text");

            var clientMock = new Mock<HttpMessageHandler>();
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{ \"Id\": 1, \"OcrText\": null }")
            };

            clientMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            var client = new HttpClient(clientMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient("DAL")).Returns(client);

            // Simulate message received
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                Assert.Equal("1|Test OCR Text", message);
            };

            _channelMock.Setup(c => c.BasicConsume("ocr_result_queue", true, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), consumer));
        }

        [Fact]
        public void ConnectToRabbitMQ_SuccessfullyConnectsAndDeclaresQueue()
        {
            // Arrange
            _channelMock.Setup(c => c.QueueDeclare("ocr_result_queue", false, false, false, null));

            // Act
            _listenerService.ConnectToRabbitMQ();

            // Assert
            _channelMock.Verify(c => c.QueueDeclare("ocr_result_queue", false, false, false, null), Times.Once, "Queue was not declared.");
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Reused existing RabbitMQ connection and declared the queue")),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once, "Expected log message for successful connection was not written."
            );

        }

        [Fact]
        public void ConnectToRabbitMQ_FailsAfterRetries_ThrowsException()
        {
            // Arrange
            _channelMock.Setup(c => c.QueueDeclare("ocr_result_queue", false, false, false, null))
                .Throws(new Exception("Failed to connect to RabbitMQ"));

            // Act and Assert
            Assert.Throws<Exception>(() => _listenerService.ConnectToRabbitMQ());
        }

        [Fact]
        public void StartListening_BasicConsumeCalledAndLogsReceivedMessage()
        {
            // Arrange
            _connectionMock.Setup(c => c.CreateModel()).Returns(_channelMock.Object);
            _channelMock.Setup(c => c.BasicConsume(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<EventingBasicConsumer>()
            ));

            // Act
            _listenerService.ConnectToRabbitMQ(); // Ensure _channel is initialized
            _listenerService.StartListening();   // Start consuming messages

            // Assert
            _channelMock.Verify(c => c.BasicConsume(
                It.Is<string>(queue => queue == "ocr_result_queue"),
                It.Is<bool>(autoAck => autoAck),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<EventingBasicConsumer>()),
                Times.Once, "BasicConsume was not called."
            );
        }

        [Fact]
        public async Task StopAsync_ClosesConnectionAndChannel()
        {
            // Arrange: Set up mocks to simulate open connection and channel
            _connectionMock.Setup(c => c.CreateModel()).Returns(_channelMock.Object);
            _channelMock.Setup(c => c.IsOpen).Returns(true);
            _connectionMock.Setup(c => c.IsOpen).Returns(true);

            // Act: Start and then stop the service
            await _listenerService.StartAsync(CancellationToken.None);
            await _listenerService.StopAsync(CancellationToken.None);

            // Assert: Verify that Close() is called once on both mocks
            _channelMock.Verify(c => c.Close(), Times.Once, "Channel.Close() was not called.");
            _connectionMock.Verify(c => c.Close(), Times.Once, "Connection.Close() was not called.");
        }
    }

}
