using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using REST_API.DTOs;
using System.Text;
using System.Text.Json;

namespace REST_API.Services
{
    public class OcrWorker : IHostedService
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<OcrWorker> _logger;

        private readonly string[] _queueNames = { "document.created", "document.updated", "document.deleted" };

        public OcrWorker(ILogger<OcrWorker> logger, RabbitMQPublisher rabbitMQPublisher)
        {
            _logger = logger;
            _connection = rabbitMQPublisher.GetConnection();
            _channel = _connection.CreateModel();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var queueName in _queueNames)
            {
                _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
            }

            // create a consumer for each queue
            foreach (var queueName in _queueNames)
            {
                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var document = JsonSerializer.Deserialize<DocumentDTO>(message);

                    ProcessDocument(document, queueName);

                    _channel.BasicAck(ea.DeliveryTag, false);
                };

                _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
                _logger.LogInformation("OCR Worker started listening for messages on queue: {QueueName}", queueName);
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _channel.Close();
            _connection.Close();
            return Task.CompletedTask;
        }

        private void ProcessDocument(DocumentDTO document, string queueName)
        {
            // placeholder for OCR processing logic
            _logger.LogInformation("Processing document: {Id} from queue: {QueueName}", document.Id, queueName);
        }
    }
}
