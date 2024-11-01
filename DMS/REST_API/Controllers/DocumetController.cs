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

        public DocumentController(IDocumentRepository documentRepository, IMapper mapper, ILogger<DocumentController> logger, RabbitMQPublisher rabbitMQPublisher)
        {
            _documentRepository = documentRepository;
            _mapper = mapper;
            _logger = logger;
            _rabbitMQPublisher = rabbitMQPublisher;
        }

        // GET: /document
        [HttpGet(Name = "GetDocuments")]
        public async Task<ActionResult<IEnumerable<DocumentDTO>>> Get()
        {
            var documents = await _documentRepository.GetAllDocumentsAsync();
            var documentDTOs = _mapper.Map<IEnumerable<DocumentDTO>>(documents);
            return Ok(documentDTOs);
        }

        // GET: /document/{id}
        [HttpGet("{id}", Name = "GetDocumentById")]
        public async Task<ActionResult<DocumentDTO>> GetById(int id)
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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var document = _mapper.Map<Document>(documentDTO);
            document.CreatedAt = DateTime.UtcNow;
            document.UpdatedAt = DateTime.UtcNow;

            await _documentRepository.AddDocumentAsync(document);
            var createdDocumentDTO = _mapper.Map<DocumentDTO>(document);

            // Publish a message to RabbitMQ
            _rabbitMQPublisher.PublishMessage($"New document created: {document.Title}");

            return CreatedAtAction(nameof(GetById), new { id = document.Id }, createdDocumentDTO);
        }

        // DELETE: /document/{id}
        [HttpDelete("{id}", Name = "DeleteDocument")]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0)
            {
                return BadRequest("Invalid document ID.");
            }

            var document = await _documentRepository.GetDocumentByIdAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            await _documentRepository.DeleteDocumentAsync(id);
            return NoContent();
        }

        // PUT: /document/{id}
        [HttpPut("{id}", Name = "UpdateDocument")]
        public async Task<IActionResult> Put(int id, [FromBody] DocumentDTO documentDTO)
        {
            if (id <= 0)
            {
                return BadRequest("Invalid document ID.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingDocument = await _documentRepository.GetDocumentByIdAsync(id);
            if (existingDocument == null)
            {
                return NotFound();
            }

            // Map updated properties
            existingDocument.Title = documentDTO.Title;
            existingDocument.Content = documentDTO.Content;
            existingDocument.UpdatedAt = DateTime.UtcNow;

            await _documentRepository.UpdateDocumentAsync(existingDocument);

            // Publish a message to RabbitMQ
            _rabbitMQPublisher.PublishMessage($"Document updated: {existingDocument.Title}");

            return NoContent();
        }
    }
}
