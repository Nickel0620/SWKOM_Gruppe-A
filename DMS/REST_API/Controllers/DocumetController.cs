using Microsoft.AspNetCore.Mvc;
using REST_API.DTOs;
using AutoMapper;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DAL.Entities;
using REST_API.Services;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Elastic.Clients.Elasticsearch;

namespace REST_API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DocumentController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMapper _mapper;
        private readonly IMessageQueueService _messageQueueService;
        private readonly FileController _fileController;
        private readonly ElasticsearchClient _elasticClient;
        private readonly ILogger<DocumentController> _logger; 

        public DocumentController(
            IHttpClientFactory httpClientFactory,
            IMapper mapper,
            IMessageQueueService messageQueueService,
            FileController fileController,
            ElasticsearchClient elasticClient,
            ILogger<DocumentController> logger) 
        {
            _httpClientFactory = httpClientFactory;
            _mapper = mapper;
            _messageQueueService = messageQueueService;
            _fileController = fileController;
            _elasticClient = elasticClient;
            _logger = logger; 
        }

        private IActionResult CreateErrorResponse(string message, int statusCode)
        {
            _logger.LogError("Error response: {Message}, StatusCode: {StatusCode}", message, statusCode); 
            return StatusCode(statusCode, new { error = message });
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var client = _httpClientFactory.CreateClient("DAL");
            var response = await client.GetAsync("/api/document");

            if (response.IsSuccessStatusCode)
            {
                var documents = await response.Content.ReadFromJsonAsync<IEnumerable<Document>>();
                var sortedDocuments = documents.OrderBy(d => d.Id);
                var dtoDocuments = _mapper.Map<IEnumerable<DocumentDTO>>(sortedDocuments);
                return Ok(dtoDocuments);
            }

            return CreateErrorResponse("Error retrieving documents from DAL", (int)response.StatusCode);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            // Validate input
            if (id <= 0)
            {
                return BadRequest(new { error = "Invalid document ID." });
            }

            try
            {
                var client = _httpClientFactory.CreateClient("DAL");
                var response = await client.GetAsync($"/api/document/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var document = await response.Content.ReadFromJsonAsync<Document>();
                    if (document != null)
                    {
                        var dtoDocument = _mapper.Map<DocumentDTO>(document);
                        return Ok(dtoDocument);
                    }

                    return NotFound(new { error = "Document not found" });
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Handle not found response from DAL explicitly
                    return NotFound(new { error = "Document not found" });
                }
                else
                {
                    // Handle other non-successful responses
                    _logger.LogError("Error retrieving document from DAL. Status code: {StatusCode}, Reason: {ReasonPhrase}",
                                     (int)response.StatusCode, response.ReasonPhrase);
                    return CreateErrorResponse("Error retrieving document from DAL", (int)response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving the document with ID: {Id}", id);
                return StatusCode(500, new { error = "An unexpected error occurred while retrieving the document." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DocumentDTO documentDto)
        {
            _logger.LogInformation("Received DocumentDTO: {DocumentDTO}", JsonSerializer.Serialize(documentDto));

            if (!ModelState.IsValid)
            {
                return BadRequest(new { errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)) });
            }

            var client = _httpClientFactory.CreateClient("DAL");
            var document = _mapper.Map<Document>(documentDto);

            var response = await client.PostAsJsonAsync("/api/document", document);

            if (response.IsSuccessStatusCode)
            {
                var createdDocument = await response.Content.ReadFromJsonAsync<Document>();
                if (createdDocument == null || createdDocument.Id <= 0)
                {
                    return StatusCode(500, new { error = "Error retrieving created document from DAL." });
                }

                // Send the FilePath to RabbitMQ for processing
                if (!string.IsNullOrEmpty(documentDto.FilePath))
                {
                    try
                    {
                        _messageQueueService.SendToQueue($"{createdDocument.Id}|{documentDto.FilePath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending message to RabbitMQ for FilePath: {FilePath}", documentDto.FilePath);
                        return StatusCode(500, new { error = $"Error sending the message to RabbitMQ: {ex.Message}" });
                    }
                }

                _logger.LogInformation("OCR Text to index: {OcrText}", documentDto.OcrText);
                // Index the document in Elasticsearch
                try
                {
                    var elasticDocument = new
                    {
                        createdDocument.Id,
                        createdDocument.Title,
                        createdDocument.FilePath,
                        createdDocument.CreatedAt,
                        createdDocument.UpdatedAt,
                        OcrText = documentDto.OcrText ?? ""
                    };

                    var indexResponse = await _elasticClient.IndexAsync(elasticDocument, i => i.Index("documents"));

                    if (!indexResponse.IsValidResponse)
                    {
                        _logger.LogError("Failed to index document in Elasticsearch. Debug Info: {DebugInfo}", indexResponse.DebugInformation);
                        return StatusCode(500, new { error = "Error indexing document in Elasticsearch." });
                    }

                    _logger.LogInformation("Document indexed successfully in Elasticsearch with ID: {Id}", createdDocument.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error indexing document in Elasticsearch.");
                    return StatusCode(500, new { error = $"Error indexing document in Elasticsearch: {ex.Message}" });
                }

                return CreatedAtAction(nameof(GetById), new { id = createdDocument.Id }, createdDocument);
            }

            return CreateErrorResponse("Error creating document in DAL", (int)response.StatusCode);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, DocumentDTO documentDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)) });
            }

            if (id != documentDto.Id)
            {
                return BadRequest(new { error = "ID Mismatch" });
            }

            var client = _httpClientFactory.CreateClient("DAL");
            var document = _mapper.Map<Document>(documentDto);
            var response = await client.PutAsJsonAsync($"/api/document/{id}", document);

            _logger.LogInformation("OCR Text to index: {OcrText}", document.OcrText);
            if (response.IsSuccessStatusCode)
            {
                // Re-index the updated document in Elasticsearch
                try
                {
                    var elasticDocument = new
                    {
                        document.Id,
                        document.Title,
                        document.FilePath,
                        document.CreatedAt,
                        document.UpdatedAt,
                        OcrText = document.OcrText
                    };

                    var indexResponse = await _elasticClient.IndexAsync(elasticDocument, i => i.Index("documents"));

                    if (!indexResponse.IsValidResponse)
                    {
                        _logger.LogError("Failed to update document in Elasticsearch. Debug Info: {DebugInfo}", indexResponse.DebugInformation);
                        return StatusCode(500, new { error = "Error updating document in Elasticsearch." });
                    }

                    _logger.LogInformation("Document updated successfully in Elasticsearch with ID: {Id}", document.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating document in Elasticsearch.");
                    return StatusCode(500, new { error = $"Error updating document in Elasticsearch: {ex.Message}" });
                }

                return NoContent();
            }

            return CreateErrorResponse("Error updating document in DAL", (int)response.StatusCode);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var client = _httpClientFactory.CreateClient("DAL");

            // Fetch the document to get its filePath before deletion
            var response = await client.GetAsync($"/api/document/{id}");

            if (!response.IsSuccessStatusCode)
            {
                return CreateErrorResponse("Error retrieving document from DAL", (int)response.StatusCode);
            }

            var document = await response.Content.ReadFromJsonAsync<Document>();
            if (document == null || string.IsNullOrEmpty(document.FilePath))
            {
                return NotFound(new { error = "Document not found or no associated file path." });
            }

            // Delete the document in DAL
            response = await client.DeleteAsync($"/api/document/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return CreateErrorResponse("Error deleting document from DAL", (int)response.StatusCode);
            }

            // Delete the file from MinIO
            try
            {
                await _fileController.DeleteFile(document.FilePath);
                _logger.LogInformation("Successfully deleted file {FilePath} from MinIO.", document.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {FilePath} from MinIO.", document.FilePath);
            }

            // Delete the document from Elasticsearch
            try
            {
                var elasticResponse = await _elasticClient.DeleteAsync(new DeleteRequest("documents", id.ToString()));

                if (elasticResponse.IsValidResponse)
                {
                    _logger.LogInformation("Document with ID {Id} deleted successfully from Elasticsearch.", id);
                }
                else
                {
                    _logger.LogWarning("Failed to delete document with ID {Id} from Elasticsearch. Debug Info: {DebugInfo}", id, elasticResponse.DebugInformation);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document with ID {Id} from Elasticsearch.", id);
            }

            return NoContent();
        }

        [HttpPost("search/querystring")]
        public async Task<IActionResult> SearchByQueryString([FromBody] string searchTerm)
        {
            _logger.LogInformation("SearchByQueryString called with term: {SearchTerm}", searchTerm);

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                _logger.LogWarning("Search term is empty or null.");
                return BadRequest(new { message = "Search term cannot be empty" });
            }

            try
            {
                var response = await _elasticClient.SearchAsync<Document>(s => s
                    .Index("documents")
                    .Query(q => q.QueryString(qs => qs.Query($"*{searchTerm}*"))));

                if (response.IsValidResponse)
                {
                    _logger.LogInformation("Elasticsearch response valid. Found {Count} documents.", response.Documents.Count);

                    if (response.Documents.Any())
                    {
                        return Ok(response.Documents);
                    }

                    _logger.LogInformation("No documents found for search term: {SearchTerm}", searchTerm);
                    return Ok(new List<Document>()); // Return an empty list with 200 OK
                }

                _logger.LogError("Invalid response from Elasticsearch for term: {SearchTerm}. Debug Info: {DebugInfo}", searchTerm, response.DebugInformation);
                return StatusCode(500, new { message = "Elasticsearch query failed", details = response.DebugInformation });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while searching with term: {SearchTerm}", searchTerm);
                return StatusCode(500, new { message = "An error occurred while performing the search", details = ex.Message });
            }
        }

        [HttpPost("search/fuzzy")]
        public async Task<IActionResult> SearchByFuzzy([FromBody] string searchTerm)
        {
            _logger.LogInformation("SearchByFuzzy called with term: {SearchTerm}", searchTerm);

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                _logger.LogWarning("Search term is empty or null.");
                return BadRequest(new { message = "Search term cannot be empty" });
            }

            try
            {
                var response = await _elasticClient.SearchAsync<Document>(s => s
                    .Index("documents")
                    .Query(q => q.Match(m => m
                        .Field(f => f.OcrText)
                        .Query(searchTerm)
                        .Fuzziness(new Fuzziness(2))
                    )));

                if (response.IsValidResponse)
                {
                    _logger.LogInformation("Elasticsearch response valid. Found {Count} documents.", response.Documents.Count);

                    if (response.Documents.Any())
                    {
                        return Ok(response.Documents);
                    }

                    _logger.LogInformation("No documents found for fuzzy search term: {SearchTerm}", searchTerm);
                    return Ok(new List<Document>()); // Return an empty list with 200 OK
                }

                _logger.LogError("Invalid response from Elasticsearch for fuzzy term: {SearchTerm}. Debug Info: {DebugInfo}", searchTerm, response.DebugInformation);
                return StatusCode(500, new { message = "Failed to perform fuzzy search", details = response.DebugInformation });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while performing fuzzy search with term: {SearchTerm}", searchTerm);
                return StatusCode(500, new { message = "An error occurred while performing the search", details = ex.Message });
            }
        }

        [HttpGet("download/{id}")]
        public async Task<IActionResult> DownloadDocument(int id)
        {
            // Fetch document details from DAL
            var client = _httpClientFactory.CreateClient("DAL");
            var response = await client.GetAsync($"/api/document/{id}");

            if (!response.IsSuccessStatusCode)
            {
                return CreateErrorResponse("Error retrieving document from DAL", (int)response.StatusCode);
            }

            var document = await response.Content.ReadFromJsonAsync<Document>();
            if (document == null || string.IsNullOrEmpty(document.FilePath))
            {
                return NotFound(new { error = "Document not found or no associated file path." });
            }

            // Use FileController to download the file from MinIO
            return await _fileController.DownloadFile(document.FilePath);
        }

    }
}
