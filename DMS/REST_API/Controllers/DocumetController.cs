using Microsoft.AspNetCore.Mvc;
using DAL.Entities;
using DAL.Repositories;


namespace REST_API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly ILogger<DocumentController> _logger;

        public DocumentController(IDocumentRepository documentRepository, ILogger<DocumentController> logger)
        {
            _documentRepository = documentRepository;
            _logger = logger;
        }

        // GET: /document
        [HttpGet(Name = "GetDocuments")]
        public async Task<ActionResult<IEnumerable<Document>>> Get()
        {
            var documents = await _documentRepository.GetAllDocumentsAsync();
            return Ok(documents);
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

            return Ok(document);
        }

        // POST: /document
        [HttpPost(Name = "CreateDocument")]
        public async Task<ActionResult<Document>> Post([FromBody] Document newDocument)
        {
            await _documentRepository.AddDocumentAsync(newDocument);
            return CreatedAtAction(nameof(GetById), new { id = newDocument.Id }, newDocument);
        }

        // DELETE: /document/{id}
        [HttpDelete("{id}", Name = "DeleteDocument")]
        public async Task<IActionResult> Delete(int id)
        {
            var document = await _documentRepository.GetDocumentByIdAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            await _documentRepository.DeleteDocumentAsync(id);
            return NoContent();
        }
    }
}
