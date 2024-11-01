using RabbitMQ.Client;
using System.Text;

namespace REST_API.Services
{
    public class RabbitMQPublisher
    {
        private readonly string _hostname = "localhost";
        private readonly string _queueName = "documentQueue";
        private IConnection _connection;

        public RabbitMQPublisher()
        {
            var factory = new ConnectionFactory() { HostName = _hostname };
            _connection = factory.CreateConnection();
        }

        public void PublishMessage(string message)
        {
            using (var channel = _connection.CreateModel())
            {
                channel.QueueDeclare(queue: _queueName,
                                     durable: false,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);

                var body = Encoding.UTF8.GetBytes(message);

                channel.BasicPublish(exchange: "",
                                     routingKey: _queueName,
                                     basicProperties: null,
                                     body: body);
            }
        }
    }
}
