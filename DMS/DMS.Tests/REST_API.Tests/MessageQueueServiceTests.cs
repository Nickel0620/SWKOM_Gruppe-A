//using Moq;
//using RabbitMQ.Client;
//using REST_API.Services;
//using System.Text;
//using Xunit;

//namespace DMS.Tests.REST_API.Tests
//{
//    public class MessageQueueServiceTests
//    {
//        private readonly Mock<IConnection> _connectionMock;
//        private readonly Mock<IModel> _channelMock;
//        private readonly MessageQueueService _service;

//        public MessageQueueServiceTests()
//        {
//            _connectionMock = new Mock<IConnection>();
//            _channelMock = new Mock<IModel>();
//            _connectionMock.Setup(c => c.CreateModel()).Returns(_channelMock.Object);

//            _service = new MessageQueueService();
//        }

//        [Fact]
//        public void SendToQueue_ValidMessage_PublishesMessage()
//        {
//            // Arrange
//            var message = "Test Message";
//            var queueName = "file_queue";

//            _channelMock.Setup(c => c.QueueDeclare(queueName, false, false, false, null));
//            _channelMock.Setup(c => c.BasicPublish("", queueName, null, It.IsAny<byte[]>()));

//            // Act
//            _service.SendToQueue(message);

//            // Assert
//            _channelMock.Verify(c => c.BasicPublish("", queueName, null, It.Is<byte[]>(b => Encoding.UTF8.GetString(b) == message)), Times.Once);
//        }

//        [Fact]
//        public void Dispose_ClosesChannelAndConnection()
//        {
//            // Arrange
//            _channelMock.Setup(c => c.IsOpen).Returns(true);
//            _connectionMock.Setup(c => c.IsOpen).Returns(true);

//            // Act
//            _service.Dispose();

//            // Assert
//            _channelMock.Verify(c => c.Close(), Times.Once);
//            _connectionMock.Verify(c => c.Close(), Times.Once);
//        }
//    }

//}
