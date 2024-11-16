using Microsoft.AspNetCore.Mvc;
using REST_API.DTOs;
using AutoMapper;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DAL.Entities;
using REST_API.Services;
using System.Text.Json;

namespace REST_API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DocumentController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMapper _mapper;
        private readonly IMessageQueueService _messageQueueService;

        public DocumentController(IHttpClientFactory httpClientFactory, IMapper mapper, IMessageQueueService messageQueueService)
        {
            _httpClientFactory = httpClientFactory;
            _mapper = mapper;
            _messageQueueService = messageQueueService;
        }

        private IActionResult CreateErrorResponse(string message, int statusCode)
        {
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
        [HttpPost]
        public async Task<IActionResult> Create(DocumentDTO documentDto)
        {
            Console.WriteLine($"Received DocumentDTO: {JsonSerializer.Serialize(documentDto)}");

            if (!ModelState.IsValid)
            {
                return BadRequest(new { errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)) });
            }

            var client = _httpClientFactory.CreateClient("DAL");
            var document = _mapper.Map<Document>(documentDto);

            var response = await client.PostAsJsonAsync("/api/document", document);

            if (response.IsSuccessStatusCode)
            {
                // Parse the created document from the DAL response
                var createdDocument = await response.Content.ReadFromJsonAsync<Document>();

                if (createdDocument == null || createdDocument.Id <= 0)
                {
                    return StatusCode(500, new { error = "Error retrieving created document from DAL." });
                }

                // Return the created document with the generated ID
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

        [HttpPut("{id}/upload")]
        public async Task<IActionResult> UploadFile(int id, IFormFile? documentFile)
        {
            if (documentFile == null || documentFile.Length == 0)
            {
                return BadRequest(new { error = "No file uploaded." });
            }
            if (!documentFile.FileName.EndsWith(".pdf"))
            {
                return BadRequest(new { error = "Only PDF files are allowed." });
            }

            var client = _httpClientFactory.CreateClient("DAL");
            var response = await client.GetAsync($"/api/document/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return NotFound(new { error = $"Error fetching document with ID {id}." });
            }

            var document = await response.Content.ReadFromJsonAsync<Document>();
            if (document == null)
            {
                return NotFound(new { error = $"Document with ID {id} not found." });
            }

            var documentDto = _mapper.Map<DocumentDTO>(document);
            var validator = new DocumentDTOValidator();
            var validationResult = await validator.ValidateAsync(documentDto);

            if (!validationResult.IsValid)
            {
                return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });
            }

            var updatedDocument = _mapper.Map<Document>(documentDto);
            var updateResponse = await client.PutAsJsonAsync($"/api/document/{id}", updatedDocument);
            if (!updateResponse.IsSuccessStatusCode)
            {
                return CreateErrorResponse("Error updating document in DAL", (int)updateResponse.StatusCode);
            }

            var filePath = Path.Combine("/app/uploads", documentFile.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await documentFile.CopyToAsync(stream);
            }

            try
            {
                _messageQueueService.SendToQueue($"{id}|{filePath}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Error sending the message to RabbitMQ: {ex.Message}" });
            }

            return Ok(new { message = $"File {documentFile.FileName} for task {id} saved successfully." });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var client = _httpClientFactory.CreateClient("DAL");
            var response = await client.DeleteAsync($"/api/document/{id}");

            if (response.IsSuccessStatusCode)
            {
                return NoContent();
            }

            return CreateErrorResponse("Error deleting document from DAL", (int)response.StatusCode);
        }
    }
}
