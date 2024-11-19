//using Moq;
//using Moq.Protected;
//using RabbitMQ.Client;
//using RabbitMQ.Client.Events;
//using REST_API.Services;
//using System.Net;
//using System.Net.Http;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using Xunit;

//namespace DMS.Tests.REST_API.Tests
//{
//    public class RabbitMqListenerServiceTests
//    {
//        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
//        private readonly Mock<IConnection> _connectionMock;
//        private readonly Mock<IModel> _channelMock;
//        private readonly RabbitMqListenerService _listenerService;

//        public RabbitMqListenerServiceTests()
//        {
//            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
//            _connectionMock = new Mock<IConnection>();
//            _channelMock = new Mock<IModel>();

//            _listenerService = new RabbitMqListenerService(_httpClientFactoryMock.Object);

//            _connectionMock.Setup(c => c.CreateModel()).Returns(_channelMock.Object);
//        }

//        [Fact]
//        public async Task StartAsync_ConnectsToRabbitMQAndStartsListening()
//        {
//            // Arrange
//            _channelMock.Setup(c => c.QueueDeclare("ocr_result_queue", false, false, false, null));

//            // Act
//            await _listenerService.StartAsync(CancellationToken.None);

//            // Assert
//            _channelMock.Verify(c => c.QueueDeclare("ocr_result_queue", false, false, false, null), Times.Once);
//        }

//        [Fact]
//        public void Received_ValidMessage_ProcessesSuccessfully()
//        {
//            // Arrange
//            var consumer = new EventingBasicConsumer(_channelMock.Object);
//            var messageBody = Encoding.UTF8.GetBytes("1|Test OCR Text");

//            var clientMock = new Mock<HttpMessageHandler>();
//            var response = new HttpResponseMessage(HttpStatusCode.OK)
//            {
//                Content = new StringContent("{ \"Id\": 1, \"OcrText\": null }")
//            };

//            clientMock
//                .Protected()
//                .Setup<Task<HttpResponseMessage>>(
//                    "SendAsync",
//                    ItExpr.IsAny<HttpRequestMessage>(),
//                    ItExpr.IsAny<CancellationToken>())
//                .ReturnsAsync(response);

//            var client = new HttpClient(clientMock.Object);
//            _httpClientFactoryMock.Setup(f => f.CreateClient("DAL")).Returns(client);

//            // Simulate message received
//            consumer.Received += async (model, ea) =>
//            {
//                var body = ea.Body.ToArray();
//                var message = Encoding.UTF8.GetString(body);

//                Assert.Equal("1|Test OCR Text", message);
//            };

//            _channelMock.Setup(c => c.BasicConsume("ocr_result_queue", true, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), consumer));
//        }

//        [Fact]
//        public async Task StopAsync_ClosesConnectionAndChannel()
//        {
//            // Arrange
//            _channelMock.Setup(c => c.IsOpen).Returns(true);
//            _connectionMock.Setup(c => c.IsOpen).Returns(true);

//            // Act
//            await _listenerService.StopAsync(CancellationToken.None);

//            // Assert
//            _channelMock.Verify(c => c.Close(), Times.Once);
//            _connectionMock.Verify(c => c.Close(), Times.Once);
//        }
//    }

//}
