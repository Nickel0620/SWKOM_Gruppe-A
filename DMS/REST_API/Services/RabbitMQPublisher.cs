//using RabbitMQ.Client;
//using REST_API.DTOs;
//using System.Text.Json;
//using System.Text;

//namespace REST_API.Services
//{
//    public class RabbitMQPublisher
//    {
//        private readonly IConnection _connection; // RabbitMQ connection
//        private readonly IModel _channel; // channel for communicating with RabbitMQ
//        private readonly ILogger<RabbitMQPublisher> _logger;
//        private const string FileQueueName = "file_queue";

//        public RabbitMQPublisher(ILogger<RabbitMQPublisher> logger)
//        {
//            _logger = logger;
//            var factory = new ConnectionFactory
//            {
//                HostName = "rabbitmq", // RabbitMQ host in the Docker network
//                Port = 5672,
//                UserName = "guest",
//                Password = "guest"
//            };

//            try
//            {
//                _logger.LogInformation("Initializing RabbitMQPublisher. Attempting to connect to RabbitMQ at {Host}:{Port}", factory.HostName, factory.Port);

//                // Create the connection and channel
//                _connection = factory.CreateConnection();
//                _channel = _connection.CreateModel();

//                _logger.LogInformation("Successfully connected to RabbitMQ at {Host}:{Port}. Declaring queue {QueueName}.", factory.HostName, factory.Port, FileQueueName);

//                // Declare the queue
//                _channel.QueueDeclare(queue: FileQueueName, durable: true, exclusive: false, autoDelete: false);

//                _logger.LogInformation("Queue {QueueName} declared successfully.", FileQueueName);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Failed to initialize RabbitMQPublisher while connecting to RabbitMQ at {Host}:{Port}.", factory.HostName, factory.Port);
//                throw;
//            }
//        }

//        public void PublishFileForProcessing(string documentId, string filePath)
//        {
//            try
//            {
//                _logger.LogInformation("Preparing to publish message to RabbitMQ. DocumentId: {DocumentId}, FilePath: {FilePath}", documentId, filePath);

//                // Construct the message as "id|filepath"
//                var message = $"{documentId}|{filePath}";
//                var body = Encoding.UTF8.GetBytes(message);

//                _logger.LogDebug("Message body prepared for queue {QueueName}: {Message}", FileQueueName, message);

//                // Publish to the file queue
//                _channel.BasicPublish(
//                    exchange: "",
//                    routingKey: FileQueueName,
//                    basicProperties: null,
//                    body: body);

//                _logger.LogInformation("Message published to queue {QueueName}: {Message}", FileQueueName, message);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Failed to publish message to queue {QueueName}. DocumentId: {DocumentId}, FilePath: {FilePath}", FileQueueName, documentId, filePath);
//                throw;
//            }
//        }

//        public void Dispose()
//        {
//            _logger.LogInformation("Disposing RabbitMQPublisher resources.");

//            try
//            {
//                _channel?.Close();
//                _logger.LogInformation("RabbitMQ channel closed.");

//                _channel?.Dispose();
//                _logger.LogInformation("RabbitMQ channel disposed.");

//                _connection?.Close();
//                _logger.LogInformation("RabbitMQ connection closed.");

//                _connection?.Dispose();
//                _logger.LogInformation("RabbitMQ connection disposed.");
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error while disposing RabbitMQPublisher resources.");
//            }
//        }
//    }

//}