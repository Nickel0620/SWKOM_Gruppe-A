using Microsoft.AspNetCore.Mvc;
using DAL.Repositories;
using DAL.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentRepository _repository;

        public DocumentController(IDocumentRepository repository)
        {
            _repository = repository;
        }

        [HttpGet]
        public async Task<IEnumerable<Document>> GetAsync()
        {
            return await _repository.GetAllDocumentsAsync();
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var document = await _repository.GetDocumentByIdAsync(id);
            if (document == null)
            {
                return NotFound();
            }
            return Ok(document);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync(Document document)
        {
            await _repository.AddDocumentAsync(document);
            return Ok(document);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAsync(int id, Document document)
        {
            var existingDocument = await _repository.GetDocumentByIdAsync(id);
            if (existingDocument == null)
            {
                return NotFound();
            }

            existingDocument.Title = document.Title;
            existingDocument.OcrText = document.OcrText;
            existingDocument.UpdatedAt = document.UpdatedAt;

            await _repository.UpdateDocumentAsync(existingDocument);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAsync(int id)
        {
            var document = await _repository.GetDocumentByIdAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            await _repository.DeleteDocumentAsync(id);
            return NoContent();
        }
    }
}
