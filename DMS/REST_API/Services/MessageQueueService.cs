using RabbitMQ.Client;
using System;
using System.Text;

namespace REST_API.Services
{
    public class MessageQueueService : IMessageQueueService, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public MessageQueueService()
        {
            var factory = new ConnectionFactory
            {
                HostName = "rabbitmq",
                Port = 5672,
                UserName = "guest",
                Password = "guest"
            };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.QueueDeclare(queue: "file_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);
        }

        public void SendToQueue(string message)
        {
            var body = Encoding.UTF8.GetBytes(message);
            _channel.BasicPublish(exchange: "", routingKey: "file_queue", basicProperties: null, body: body);
            Console.WriteLine($"[x] Sent: {message}");
        }

        public void Dispose()
        {
            if (_channel.IsOpen)
            {
                _channel.Close();
            }

            if (_connection.IsOpen)
            {
                _connection.Close();
            }
        }
    }
}
