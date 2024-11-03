using Microsoft.AspNetCore.Mvc;
using DAL.Entities;
using DAL.Repositories;
using AutoMapper;
using REST_API.DTOs;
using REST_API.Services;

namespace REST_API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<DocumentController> _logger;
        private readonly RabbitMQPublisher _rabbitMQPublisher;

        private const string DocumentEventTemplate = "Document {EventType}: {Id}";
        private const string DocumentErrorTemplate = "Error occurred while {Action} document {Id}";
        private const string DocumentActionTemplate = "Attempting to {Action} document {Id}";
        private const string ValidationErrorTemplate = "Validation failed for {Action} document: {Errors}";

        public DocumentController(IDocumentRepository documentRepository, IMapper mapper, ILogger<DocumentController> logger, RabbitMQPublisher rabbitMQPublisher)
        {
            _documentRepository = documentRepository;
            _mapper = mapper;
            _logger = logger;
            _rabbitMQPublisher = rabbitMQPublisher;
        }

        // GET: /document
        [HttpGet(Name = "GetDocuments")]
        public async Task<ActionResult<IEnumerable<Document>>> Get()
        {
            try
            {
                _logger.LogInformation("Fetching all documents");
                var documents = await _documentRepository.GetAllDocumentsAsync();
                _logger.LogInformation("Retrieved {Count} documents", documents?.Count() ?? 0);
                
                var documentDTOs = _mapper.Map<IEnumerable<DocumentDTO>>(documents);
                return Ok(documentDTOs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch documents");
                return StatusCode(500, new { error = "Failed to retrieve documents" });
            }
        }

        // GET: /document/{id}
        [HttpGet("{id}", Name = "GetDocumentById")]
        public async Task<ActionResult<Document>> GetById(int id)
        {
            _logger.LogInformation(DocumentActionTemplate, "get", id);

            var document = await _documentRepository.GetDocumentByIdAsync(id);
            if (document == null)
            {
                _logger.LogWarning("Document {Id} not found", id);
                return NotFound();
            }

            _logger.LogInformation("Document retrieved successfully: {Id}", id);
            var documentDTO = _mapper.Map<DocumentDTO>(document);
            return Ok(documentDTO);
        }

        // POST: /document
        [HttpPost(Name = "CreateDocument")]
        public async Task<ActionResult<DocumentDTO>> Post([FromBody] DocumentDTO documentDTO)
        {
            try
            {
                _logger.LogInformation("Attempting to create new document");

                if (!ModelState.IsValid)
                {
                    var errors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                    _logger.LogWarning(ValidationErrorTemplate, "create", errors);
                    return BadRequest(ModelState);
                }

                var document = _mapper.Map<Document>(documentDTO);
                document.CreatedAt = DateTime.UtcNow;
                document.UpdatedAt = DateTime.UtcNow;

                _logger.LogDebug("Saving document to database");
                await _documentRepository.AddDocumentAsync(document);

                var createdDocumentDTO = _mapper.Map<DocumentDTO>(document); // Map the created Document back to DocumentDTO
                _logger.LogInformation("Document created successfully with ID: {Id}", document.Id);

                // publish message to RabbitMQ
                _logger.LogDebug("Publishing document created event to RabbitMQ");
                _rabbitMQPublisher.PublishDocumentCreated(createdDocumentDTO);
                _logger.LogInformation(DocumentEventTemplate, "created", document.Id);

                return CreatedAtAction(nameof(GetById), new { id = document.Id }, createdDocumentDTO);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, DocumentErrorTemplate, "creating", documentDTO.Id);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // DELETE: /document/{id}
        [HttpDelete("{id}", Name = "DeleteDocument")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                _logger.LogInformation(DocumentActionTemplate, "delete", id);

                if (id <= 0) // Simple validation
                {
                    _logger.LogWarning("Invalid document ID provided for deletion: {Id}", id);
                    return BadRequest("Invalid document ID.");
                }

                var document = await _documentRepository.GetDocumentByIdAsync(id);
                if (document == null)
                {
                    _logger.LogWarning("Document not found for deletion: {Id}", id);
                    return NotFound(); // Return 404
                }

                _logger.LogDebug("Deleting document from database: {Id}", id);
                await _documentRepository.DeleteDocumentAsync(id);

                // publish message to RabbitMQ
                _logger.LogDebug("Publishing document deleted event to RabbitMQ");
                _rabbitMQPublisher.PublishDocumentDeleted(id);
                _logger.LogInformation(DocumentEventTemplate, "deleted", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, DocumentErrorTemplate, "deleting", id);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // PUT: /document/{id}
        [HttpPut("{id}", Name = "UpdateDocument")]
        public async Task<IActionResult> Put(int id, [FromBody] DocumentDTO documentDTO)
        {
            try
            {
                _logger.LogInformation(DocumentActionTemplate, "update", id);

                if (id <= 0) // Simple validation
                {
                    _logger.LogWarning("Invalid document ID provided for update: {Id}", id);
                    return BadRequest("Invalid document ID.");
                }

                if (!ModelState.IsValid)
                {
                    var errors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                    _logger.LogWarning(ValidationErrorTemplate, "update", errors);
                    return BadRequest(ModelState);
                }

                var existingDocument = await _documentRepository.GetDocumentByIdAsync(id);
                if (existingDocument == null)
                {
                    _logger.LogWarning("Document not found for update: {Id}", id);
                    return NotFound(); // Return 404 if document does not exist
                }

                // Map updated properties
                _logger.LogDebug("Updating document properties: {Id}", id);
                existingDocument.Title = documentDTO.Title;
                existingDocument.Content = documentDTO.Content;
                existingDocument.UpdatedAt = DateTime.UtcNow; // Update the last modified date

                await _documentRepository.UpdateDocumentAsync(existingDocument);
                _logger.LogInformation("Document updated successfully: {Id}", id);

                // publish message to RabbitMQ
                _logger.LogDebug("Publishing document updated event to RabbitMQ");
                _rabbitMQPublisher.PublishDocumentUpdated(_mapper.Map<DocumentDTO>(existingDocument));
                _logger.LogInformation(DocumentEventTemplate, "updated", id);

                return NoContent(); // Return 204 No Content on successful update
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, DocumentErrorTemplate, "updating", id);
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
