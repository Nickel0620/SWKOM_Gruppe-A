using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Diagnostics;
using ImageMagick;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OcrProcessing
{
    public class OcrWorker : IHostedService
    {
        public IConnection _connection;
        public IModel _channel;
        private readonly ILogger<OcrWorker> _logger;
        private const string FileQueueName = "file_queue";
        private const string OcrResultQueueName = "ocr_result_queue";

        public OcrWorker(ILogger<OcrWorker> logger)
        {
            _logger = logger;
            ConnectToRabbitMQ();
        }

        public OcrWorker(ILogger<OcrWorker> logger, IConnection connection, IModel channel)
        {
            _logger = logger;
            _connection = connection;
            _channel = channel;

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
                    var filePath = parts[1];

                    _logger.LogInformation("Received ID: {Id}, FilePath: {FilePath}", id, filePath);

                    // Ensure the file exists
                    if (!File.Exists(filePath))
                    {
                        _logger.LogError("File not found: {FilePath}", filePath);
                        _channel.BasicAck(ea.DeliveryTag, false);
                        return;
                    }

                    // Start OCR processing
                    var extractedText = await PerformOcrAsync(filePath);

                    if (!string.IsNullOrEmpty(extractedText))
                    {
                        // Send result back to ocr_result_queue
                        var resultMessage = $"{id}|{extractedText.Replace("\n", " ").Replace("\r", "")}";
                        var resultBody = Encoding.UTF8.GetBytes(resultMessage);
                        _channel.BasicPublish(exchange: "", routingKey: OcrResultQueueName, basicProperties: null, body: resultBody);


                        _logger.LogInformation("Sent OCR result for ID: {Id} to queue: {QueueName}", id, OcrResultQueueName);
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
                        catch (Exception ex)
                        {
                            // If the process fails, log the error and retry with a hardcoded path to tesseract.exe
                            // for unit testing purposes
                            _logger.LogError(ex, "Failed to start Tesseract using default path. Trying hardcoded path.");

                            var hardcodedPsi = new ProcessStartInfo
                            {
                                FileName = @"C:\Program Files\Tesseract-OCR\tesseract.exe", // Hardcoded fallback path
                                Arguments = $"{tempPngFile} stdout -l eng",
                                RedirectStandardOutput = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };

                            try
                            {
                                using (var process = Process.Start(hardcodedPsi))
                                {
                                    if (process == null)
                                    {
                                        throw new InvalidOperationException("Failed to start Tesseract process with hardcoded path.");
                                    }

                                    string result = await process.StandardOutput.ReadToEndAsync();
                                    stringBuilder.Append(result);
                                }
                            }
                            catch (Exception fallbackEx)
                            {
                                _logger.LogError(fallbackEx, "Failed to start Tesseract using hardcoded path.");
                                throw; // Re-throw the exception to signal failure
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
