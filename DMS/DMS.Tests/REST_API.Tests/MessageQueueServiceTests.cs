using Moq;
using RabbitMQ.Client;
using REST_API.Services;
using System.Text;
using Xunit;

namespace DMS.Tests.REST_API.Tests
{
    public class MessageQueueServiceTests
    {
        private readonly Mock<IConnection> _connectionMock;
        private readonly Mock<IModel> _channelMock;
        private readonly MessageQueueService _service;

        public MessageQueueServiceTests()
        {
            _connectionMock = new Mock<IConnection>();
            _channelMock = new Mock<IModel>();
            _connectionMock.Setup(c => c.CreateModel()).Returns(_channelMock.Object);

            _service = new MessageQueueService(_connectionMock.Object, _channelMock.Object);
        }


        // Moq does not support mocking or verifying calls to extension methods like BasicPublish
        //[Fact]
        //public void SendToQueue_ValidMessage_PublishesMessage()
        //{
        //    // Arrange
        //    var message = "Test Message";
        //    var body = Encoding.UTF8.GetBytes(message);

        //    // Act
        //    _service.SendToQueue(message);

        //    // Assert
        //    _channelMock.Verify(c => c.BasicPublish("", "file_queue", null, body), Times.Once);
        //}

        [Fact]
        public void Dispose_ClosesChannelAndConnection()
        {
            // Arrange
            _channelMock.Setup(c => c.IsOpen).Returns(true);
            _connectionMock.Setup(c => c.IsOpen).Returns(true);

            // Act
            _service.Dispose();

            // Assert
            _channelMock.Verify(c => c.Close(), Times.Once);
            _connectionMock.Verify(c => c.Close(), Times.Once);
        }
    }
}
