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
        private readonly ILogger<DocumentController> _logger; 

        public DocumentController(
            IHttpClientFactory httpClientFactory,
            IMapper mapper,
            IMessageQueueService messageQueueService,
            FileController fileController,
            ILogger<DocumentController> logger) 
        {
            _httpClientFactory = httpClientFactory;
            _mapper = mapper;
            _messageQueueService = messageQueueService;
            _fileController = fileController;
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

            return CreateErrorResponse("Error retrieving document from DAL", (int)response.StatusCode);
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

            if (response.IsSuccessStatusCode)
            {
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

            return NoContent();
        }
    }
}
