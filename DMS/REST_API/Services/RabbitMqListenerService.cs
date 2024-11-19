using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DAL.Entities;

namespace REST_API.Services
{
    public class RabbitMqListenerService : IHostedService
    {
        private readonly ILogger<RabbitMqListenerService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private IConnection _connection;
        private IModel _channel;

        public RabbitMqListenerService(IHttpClientFactory httpClientFactory, ILogger<RabbitMqListenerService> logger, IConnection connection = null)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _connection = connection;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting RabbitMQ Listener Service...");
            ConnectToRabbitMQ();
            StartListening();
            return Task.CompletedTask;
        }

        public void ConnectToRabbitMQ()
        {
            if (_connection == null)
            {
                int retries = 5;
                while (retries > 0)
                {
                    try
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

                        _channel.QueueDeclare(queue: "ocr_result_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);
                        _logger.LogInformation("Successfully connected to RabbitMQ and declared the queue.");
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
                    _logger.LogCritical("Failed to connect to RabbitMQ after multiple attempts.");
                    throw new Exception("Failed to connect to RabbitMQ after multiple attempts.");
                }
            }
            else
            {
                _channel = _connection.CreateModel();
                _channel.QueueDeclare(queue: "ocr_result_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);
                _logger.LogInformation("Reused existing RabbitMQ connection and declared the queue.");
            }
        }

        public void StartListening()
        {
            try
            {
                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += async (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    _logger.LogInformation("Received message: {Message}", message);

                    var parts = message.Split('|', 2);

                    if (parts.Length == 2)
                    {
                        var id = parts[0];
                        var extractedText = parts[1];

                        if (string.IsNullOrEmpty(extractedText))
                        {
                            _logger.LogWarning("Message for Task {Id} contains empty OCR text. Ignoring message.", id);
                            return;
                        }

                        var client = _httpClientFactory.CreateClient("DAL");
                        bool documentUpdated = false;

                        await Task.Delay(500); // Initial delay before retrying

                        for (int attempt = 1; attempt <= 3; attempt++)
                        {
                            try
                            {
                                var response = await client.GetAsync($"/api/document/{id}");
                                _logger.LogInformation("Attempt {Attempt}: Response Status Code for document {Id}: {StatusCode}", attempt, id, response.StatusCode);

                                if (response.IsSuccessStatusCode)
                                {
                                    var document = await response.Content.ReadFromJsonAsync<Document>();
                                    if (document != null)
                                    {
                                        _logger.LogInformation("Document {Id} retrieved successfully on attempt {Attempt}.", id, attempt);
                                        document.OcrText = extractedText;

                                        var updateResponse = await client.PutAsJsonAsync($"/api/document/{id}", document);
                                        if (updateResponse.IsSuccessStatusCode)
                                        {
                                            _logger.LogInformation("OCR text for Document {Id} updated successfully.", id);
                                            documentUpdated = true;
                                            break;
                                        }
                                        else
                                        {
                                            _logger.LogError("Error updating document {Id}. Response: {StatusCode}", id, updateResponse.StatusCode);
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Document {Id} not found on attempt {Attempt}.", id, attempt);
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("Failed to retrieve document {Id} on attempt {Attempt}. Response: {StatusCode}", id, attempt, response.StatusCode);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Exception while processing document {Id} on attempt {Attempt}.", id, attempt);
                            }

                            // Wait before retrying
                            await Task.Delay(1000);
                        }

                        if (!documentUpdated)
                        {
                            _logger.LogWarning("Failed to update document {Id} after multiple attempts.", id);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Invalid message received: {Message}", message);
                    }
                };

                _channel.BasicConsume(queue: "ocr_result_queue", autoAck: true, consumer: consumer);
                _logger.LogInformation("Started listening on queue: ocr_result_queue");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting listener for OCR results.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping RabbitMQ Listener Service...");

            if (_channel?.IsOpen == true)
            {
                _logger.LogInformation("Closing RabbitMQ channel...");
                _channel.Close();
            }

            if (_connection?.IsOpen == true)
            {
                _logger.LogInformation("Closing RabbitMQ connection...");
                _connection.Close();
            }

            _logger.LogInformation("RabbitMQ Listener Service stopped.");
            return Task.CompletedTask;
        }
    }
}
