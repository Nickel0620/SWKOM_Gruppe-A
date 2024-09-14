using Microsoft.AspNetCore.Mvc;
using DMS.Models;

namespace DMS.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DocumentController : ControllerBase
    {
        private static readonly List<Document> Documents = new List<Document>
        {
            new Document { Id = 1, Title = "Sample Document 1", Content = "This is the first sample document." },
            new Document { Id = 2, Title = "Sample Document 2", Content = "This is the second sample document." }
        };

        private readonly ILogger<DocumentController> _logger;

        public DocumentController(ILogger<DocumentController> logger)
        {
            _logger = logger;
        }

        [HttpGet(Name = "GetDocuments")]
        public IEnumerable<Document> Get()
        {
            return Documents;
        }

        [HttpGet("{id}", Name = "GetDocumentById")]
        public ActionResult<Document> GetById(int id)
        {
            var document = Documents.FirstOrDefault(d => d.Id == id);
            if (document == null)
            {
                return NotFound();
            }

            return document;
        }

        [HttpPost(Name = "CreateDocument")]
        public ActionResult<Document> Post([FromBody] Document newDocument)
        {
            newDocument.Id = Documents.Count + 1;
            Documents.Add(newDocument);

            return CreatedAtAction(nameof(GetById), new { id = newDocument.Id }, newDocument);
        }

        [HttpDelete("{id}", Name = "DeleteDocument")]
        public IActionResult Delete(int id)
        {
            var document = Documents.FirstOrDefault(d => d.Id == id);
            if (document == null)
            {
                return NotFound();
            }

            Documents.Remove(document);
            return NoContent();
        }
    }
}

