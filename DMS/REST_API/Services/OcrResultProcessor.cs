//using DAL.Repositories;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Hosting;
//using RabbitMQ.Client;
//using RabbitMQ.Client.Events;
//using System.Text;
//using System.Text.Json;

//namespace REST_API.Services
//{
//    public class OcrResultProcessor : BackgroundService
//    {
//        private readonly IConnection _connection;
//        private readonly IModel _channel;
//        private readonly IServiceProvider _serviceProvider;
//        private readonly ILogger<OcrResultProcessor> _logger;

//        private const string QueueName = "ocr_result_queue";

//        public OcrResultProcessor(IServiceProvider serviceProvider, ILogger<OcrResultProcessor> logger)
//        {
//            _serviceProvider = serviceProvider;
//            _logger = logger;

//            var factory = new ConnectionFactory
//            {
//                HostName = "rabbitmq",
//                UserName = "guest",
//                Password = "guest"
//            };

//            _connection = factory.CreateConnection();
//            _channel = _connection.CreateModel();

//            _channel.QueueDeclare(queue: QueueName, durable: true, exclusive: false, autoDelete: false);
//        }

//        protected override Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            var consumer = new EventingBasicConsumer(_channel);
//            consumer.Received += async (model, ea) =>
//            {
//                var body = ea.Body.ToArray();
//                var message = Encoding.UTF8.GetString(body);
//                var ocrResult = JsonSerializer.Deserialize<OcrResultMessage>(message);

//                if (ocrResult != null)
//                {
//                    await ProcessOcrResultAsync(ocrResult);
//                }

//                _channel.BasicAck(ea.DeliveryTag, false);
//            };

//            _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);

//            return Task.CompletedTask;
//        }

//        private async Task ProcessOcrResultAsync(OcrResultMessage ocrResult)
//        {
//            using var scope = _serviceProvider.CreateScope();
//            var documentRepository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();

//            var document = await documentRepository.GetDocumentByIdAsync(ocrResult.Id);
//            if (document == null)
//            {
//                _logger.LogWarning("Document with ID {Id} not found for OCR update.", ocrResult.Id);
//                return;
//            }

//            document.OcrText = ocrResult.OcrText;
//            await documentRepository.UpdateDocumentAsync(document);

//            _logger.LogInformation("Document with ID {Id} updated with OCR text.", ocrResult.Id);
//        }

//        public override void Dispose()
//        {
//            _channel?.Close();
//            _connection?.Close();
//            base.Dispose();
//        }
//    }

//    public class OcrResultMessage
//    {
//        public int Id { get; set; }
//        public string OcrText { get; set; }
//    }
//}
