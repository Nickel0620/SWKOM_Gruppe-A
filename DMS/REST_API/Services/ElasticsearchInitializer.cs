using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;

namespace REST_API.Services
{
    public class ElasticsearchInitializer
    {
        private readonly ElasticsearchClient _elasticClient;
        private readonly ILogger<ElasticsearchInitializer> _logger;

        public ElasticsearchInitializer(ElasticsearchClient elasticClient, ILogger<ElasticsearchInitializer> logger)
        {
            _elasticClient = elasticClient;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            const string indexName = "documents";

            // Check if the index exists
            var existsResponse = await _elasticClient.Indices.ExistsAsync(indexName);
            if (existsResponse.Exists)
            {
                _logger.LogInformation("Elasticsearch index '{IndexName}' already exists.", indexName);
                return;
            }

            // Define the index mapping
            var mapping = new TypeMapping
            {
                Properties = new Properties
                {
                    { "createdAt", new DateProperty() },
                    { "filePath", new TextProperty
                        {
                            Fields = new Properties
                            {
                                { "keyword", new KeywordProperty { IgnoreAbove = 256 } }
                            }
                        }
                    },
                    { "id", new LongNumberProperty() }, 
                    { "title", new TextProperty
                        {
                            Fields = new Properties
                            {
                                { "keyword", new KeywordProperty { IgnoreAbove = 256 } }
                            }
                        }
                    },
                    { "OcrText", new TextProperty() }
                }
            };

            // Create the index with the defined mapping
            var createResponse = await _elasticClient.Indices.CreateAsync(indexName, c => c
                .Mappings(mapping)
            );

            if (createResponse.IsValidResponse)
            {
                _logger.LogInformation("Elasticsearch index '{IndexName}' created successfully.", indexName);
            }
            else
            {
                _logger.LogError("Failed to create Elasticsearch index '{IndexName}': {Error}", indexName, createResponse.DebugInformation);
                throw new Exception($"Failed to create Elasticsearch index: {createResponse.DebugInformation}");
            }
        }
    }
}
