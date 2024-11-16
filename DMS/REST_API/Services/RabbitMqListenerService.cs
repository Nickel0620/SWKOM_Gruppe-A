using Microsoft.Extensions.Hosting;
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
        private IConnection _connection;
        private IModel _channel;
        private readonly IHttpClientFactory _httpClientFactory;

        public RabbitMqListenerService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            ConnectToRabbitMQ();
            StartListening();
            return Task.CompletedTask;
        }

        private void ConnectToRabbitMQ()
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
                    Console.WriteLine("Successfully connected to RabbitMQ and queue declared.");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error connecting to RabbitMQ: {ex.Message}. Retrying in 5 seconds...");
                    Thread.Sleep(5000);
                    retries--;
                }
            }

            if (_connection == null || !_connection.IsOpen)
            {
                throw new Exception("Failed to connect to RabbitMQ after multiple attempts.");
            }
        }

        private void StartListening()
        {
            try
            {
                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += async (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var parts = message.Split('|', 2);

                    Console.WriteLine($"[Listener] Received message: {message}");

                    if (parts.Length == 2)
                    {
                        var id = parts[0];
                        var extractedText = parts[1];

                        if (string.IsNullOrEmpty(extractedText))
                        {
                            Console.WriteLine($"Error: Empty OCR text for Task {id}. Message will be ignored.");
                            return;
                        }

                        var client = _httpClientFactory.CreateClient("DAL");
                        bool documentUpdated = false;

                        await Task.Delay(500); // Initial delay before retrying
                        // Retry mechanism for fetching and updating the document
                        for (int attempt = 1; attempt <= 3; attempt++)
                        {
                            try
                            {
                                var response = await client.GetAsync($"/api/document/{id}");

                                Console.WriteLine($"Response Status Code: {response.StatusCode}");

                                if (response.IsSuccessStatusCode)
                                {
                                    var document = await response.Content.ReadFromJsonAsync<Document>();
                                    if (document != null)
                                    {
                                        Console.WriteLine($"[Listener] Document {id} retrieved successfully on attempt {attempt}.");
                                        document.OcrText = extractedText;

                                        var updateResponse = await client.PutAsJsonAsync($"/api/document/{id}", document);
                                        if (updateResponse.IsSuccessStatusCode)
                                        {
                                            Console.WriteLine($"OCR text for Document {id} updated successfully.");
                                            documentUpdated = true;
                                            break;
                                        }
                                        else
                                        {
                                            Console.WriteLine($"Error updating document with ID {id}: {updateResponse.StatusCode}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"[Listener] Document {id} not found on attempt {attempt}.");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Error retrieving document with ID {id} on attempt {attempt}: {response.StatusCode}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Exception while processing document {id} on attempt {attempt}: {ex.Message}");
                            }

                            // Wait before retrying
                            await Task.Delay(1000);
                        }

                        if (!documentUpdated)
                        {
                            Console.WriteLine($"Failed to update document {id} after multiple attempts.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Error: Invalid message received.");
                    }
                };

                _channel.BasicConsume(queue: "ocr_result_queue", autoAck: true, consumer: consumer);
                Console.WriteLine("Started listening on queue: ocr_result_queue");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting listener for OCR results: {ex.Message}");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _channel?.Close();
            _connection?.Close();
            Console.WriteLine("RabbitMQ Listener Service stopped.");
            return Task.CompletedTask;
        }
    }
}
