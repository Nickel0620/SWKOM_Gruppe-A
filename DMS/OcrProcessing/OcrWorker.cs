using ImageMagick;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Diagnostics;
using System.Text;

namespace OcrProcessing
{
    public class OcrWorker : IHostedService
    {
        public IConnection _connection;
        public IModel _channel;
        private readonly ILogger<OcrWorker> _logger;
        private const string FileQueueName = "file_queue";
        private const string OcrResultQueueName = "ocr_result_queue";
        private const string BucketName = "uploads"; // MinIO bucket name
        private readonly IMinioClient _minioClient;

        public OcrWorker(ILogger<OcrWorker> logger)
        {
            _logger = logger;
            _minioClient = new MinioClient()
                .WithEndpoint("minio", 9000)
                .WithCredentials("minioadmin", "minioadmin")
                .WithSSL(false)
                .Build();

            ConnectToRabbitMQ();
        }

        public OcrWorker(ILogger<OcrWorker> logger, IConnection connection, IModel channel)
        {
            _logger = logger;
            _connection = connection;
            _channel = channel;
            _minioClient = new MinioClient()
                .WithEndpoint("minio", 9000)
                .WithCredentials("minioadmin", "minioadmin")
                .WithSSL(false)
                .Build();

            SetupRabbitMQ();
        }

        private void SetupRabbitMQ()
        {
            _channel.QueueDeclare(queue: FileQueueName, durable: false, exclusive: false, autoDelete: false);
        }

        private void ConnectToRabbitMQ()
        {
            int retries = 5;
            while (retries > 0)
            {
                try
                {
                    var factory = new ConnectionFactory()
                    {
                        HostName = "rabbitmq",
                        UserName = "guest",
                        Password = "guest"
                    };
                    _connection = factory.CreateConnection();
                    _channel = _connection.CreateModel();

                    // Declare the queues
                    _channel.QueueDeclare(queue: FileQueueName, durable: false, exclusive: false, autoDelete: false);

                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error connecting to RabbitMQ. Retrying in 5 seconds...");
                    Thread.Sleep(5000);
                    retries--;
                }
            }

            if (_connection == null || !_connection.IsOpen)
            {
                throw new Exception("Could not establish a connection to RabbitMQ after multiple attempts.");
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Set up consumer for the file_queue
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var parts = message.Split('|');

                if (parts.Length == 2)
                {
                    var id = parts[0];
                    var fileName = parts[1];

                    _logger.LogInformation("Received ID: {Id}, FileName: {FileName}", id, fileName);

                    try
                    {
                        // Download the file from MinIO
                        var localFilePath = await DownloadFileFromMinIOAsync(fileName);

                        // Start OCR processing
                        var extractedText = await PerformOcrAsync(localFilePath);

                        if (!string.IsNullOrEmpty(extractedText))
                        {
                            // Send result back to ocr_result_queue
                            var resultMessage = $"{id}|{extractedText.Replace("\n", " ").Replace("\r", "")}";
                            var resultBody = Encoding.UTF8.GetBytes(resultMessage);
                            _channel.BasicPublish(exchange: "", routingKey: OcrResultQueueName, basicProperties: null, body: resultBody);

                            _logger.LogInformation("Sent OCR result for ID: {Id} to queue: {QueueName}", id, OcrResultQueueName);
                        }

                        // Clean up local file
                        if (File.Exists(localFilePath))
                        {
                            File.Delete(localFilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing file: {FileName}", fileName);
                    }

                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                else
                {
                    _logger.LogError("Invalid message received, split into less than 2 parts.");
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
            };

            _channel.BasicConsume(queue: FileQueueName, autoAck: false, consumer: consumer);
            _logger.LogInformation("OCR Worker started listening for messages on queue: {QueueName}", FileQueueName);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _channel.Close();
            _connection.Close();
            return Task.CompletedTask;
        }

        private async Task<string> DownloadFileFromMinIOAsync(string fileName)
        {
            var localFilePath = Path.Combine(Path.GetTempPath(), fileName);

            try
            {
                using var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write);
                await _minioClient.GetObjectAsync(new GetObjectArgs()
                    .WithBucket(BucketName)
                    .WithObject(fileName)
                    .WithCallbackStream(stream => stream.CopyTo(fileStream)));

                _logger.LogInformation("Downloaded file {FileName} to {LocalFilePath}", fileName, localFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file from MinIO: {FileName}", fileName);
                throw;
            }

            return localFilePath;
        }

        public async Task<string> PerformOcrAsync(string filePath)
        {
            var stringBuilder = new StringBuilder();

            try
            {
                using (var images = new MagickImageCollection(filePath)) // For multi-page documents
                {
                    foreach (var image in images)
                    {
                        var tempPngFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");

                        image.Density = new Density(300, 300); // Set resolution
                        image.Format = MagickFormat.Png;
                        image.Write(tempPngFile);

                        // Attempt to use Tesseract CLI for each page
                        var psi = new ProcessStartInfo
                        {
                            FileName = "tesseract", // Default to "tesseract" assuming it's in PATH
                            Arguments = $"{tempPngFile} stdout -l eng",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        try
                        {
                            using (var process = Process.Start(psi))
                            {
                                if (process == null)
                                {
                                    throw new InvalidOperationException("Failed to start Tesseract process.");
                                }

                                string result = await process.StandardOutput.ReadToEndAsync();
                                stringBuilder.Append(result);
                            }
                        }
                        finally
                        {
                            if (File.Exists(tempPngFile))
                            {
                                File.Delete(tempPngFile); // Delete temp PNG file after processing
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during OCR processing");
            }

            return stringBuilder.ToString();
        }
    }
}
