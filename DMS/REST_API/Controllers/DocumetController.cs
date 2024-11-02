using Microsoft.AspNetCore.Mvc;
using DAL.Entities;
using DAL.Repositories;
using AutoMapper;
using REST_API.DTOs;

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

        private const string DocumentEventTemplate = "Document {EventType}: {DocumentId}";
        private const string DocumentErrorTemplate = "Error occurred while {Action} document {DocumentId}";

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
            var documents = await _documentRepository.GetAllDocumentsAsync();
            var documentDTOs = _mapper.Map<IEnumerable<DocumentDTO>>(documents);
            return Ok(documentDTOs);
        }

        // GET: /document/{id}
        [HttpGet("{id}", Name = "GetDocumentById")]
        public async Task<ActionResult<Document>> GetById(int id)
        {
            var document = await _documentRepository.GetDocumentByIdAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            var documentDTO = _mapper.Map<DocumentDTO>(document);
            return Ok(documentDTO);
        }

        // POST: /document
        [HttpPost(Name = "CreateDocument")]
        public async Task<ActionResult<DocumentDTO>> Post([FromBody] DocumentDTO documentDTO)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState); // Return validation errors
                }

                var document = _mapper.Map<Document>(documentDTO);
                document.CreatedAt = DateTime.UtcNow;
                document.UpdatedAt = DateTime.UtcNow;

                await _documentRepository.AddDocumentAsync(document);
                var createdDocumentDTO = _mapper.Map<DocumentDTO>(document); // Map the created Document back to DocumentDTO

                // publish message to RabbitMQ
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
                if (id <= 0) // Simple validation
                {
                    return BadRequest("Invalid document ID.");
                }

                var document = await _documentRepository.GetDocumentByIdAsync(id);
                if (document == null)
                {
                    return NotFound(); // Return 404
                }

                await _documentRepository.DeleteDocumentAsync(id);

                // publish message to RabbitMQ
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
                if (id <= 0) // Simple validation
                {
                    return BadRequest("Invalid document ID.");
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState); // Return validation errors
                }

                var existingDocument = await _documentRepository.GetDocumentByIdAsync(id);
                if (existingDocument == null)
                {
                    return NotFound(); // Return 404 if document does not exist
                }

                // Map updated properties
                existingDocument.Title = documentDTO.Title;
                existingDocument.Content = documentDTO.Content;
                existingDocument.UpdatedAt = DateTime.UtcNow; // Update the last modified date

                await _documentRepository.UpdateDocumentAsync(existingDocument);

                // publish message to RabbitMQ
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
