using DAL.Context;
using DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories
{
    public class DocumentRepository : IDocumentRepository
    {
        private readonly DocumentContext _context;
        private readonly ILogger<DocumentRepository> _logger;

        public DocumentRepository(DocumentContext context, ILogger<DocumentRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<Document>> GetAllDocumentsAsync()
        {
            try
            {
                _logger.LogDebug("Fetching all documents from database");
                var documents = await _context.Documents.ToListAsync();
                _logger.LogInformation("Retrieved {Count} documents from database", documents.Count);
                return documents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching all documents");
                throw;
            }
        }

        public async Task<Document?> GetDocumentByIdAsync(int id)
        {
            try
            {
                _logger.LogDebug("Fetching document {DocumentId} from database", id);
                var document = await _context.Documents.FindAsync(id);

                if (document == null)
                {
                    _logger.LogWarning("Document {DocumentId} not found in database", id);
                }
                else
                {
                    _logger.LogDebug("Successfully retrieved document {DocumentId}", id);
                }

                return document;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching document {DocumentId}", id);
                throw;
            }
        }

        public async Task AddDocumentAsync(Document document)
        {
            try
            {
                _logger.LogDebug("Adding new document to database");
                await _context.Documents.AddAsync(document);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully added document {DocumentId} to database", document.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while adding document to database");
                throw;
            }
        }

        public async Task UpdateDocumentAsync(Document document)
        {
            try
            {
                _logger.LogDebug("Updating document {DocumentId} in database", document.Id);
                _context.Entry(document).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully updated document {DocumentId} in database", document.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating document {DocumentId}", document.Id);
                throw;
            }
        }

        public async Task DeleteDocumentAsync(int id)
        {
            try
            {
                _logger.LogDebug("Deleting document {DocumentId} from database", id);
                var document = await _context.Documents.FindAsync(id);
                if (document != null)
                {
                    _context.Documents.Remove(document);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Successfully deleted document {DocumentId} from database", id);
                }
                else
                {
                    _logger.LogWarning("Attempted to delete non-existent document {DocumentId}", id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting document {DocumentId}", id);
                throw;
            }
        }
    }
}
