using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using DAL.Context;
using DAL.Entities;
using DAL.Repositories;

namespace DMS.Tests.DAL.Tests
{
    public class DocumentRepositoryTests
    {
        private readonly DocumentRepository _repository;
        private readonly Mock<ILogger<DocumentRepository>> _loggerMock;
        private readonly DocumentContext _context;

        public DocumentRepositoryTests()
        {
            _loggerMock = new Mock<ILogger<DocumentRepository>>();

            // Use an in-memory database for testing
            var options = new DbContextOptionsBuilder<DocumentContext>()
                .UseInMemoryDatabase(databaseName: "DocumentRepositoryTests")
                .Options;

            _context = new DocumentContext(options);
            _repository = new DocumentRepository(_context, _loggerMock.Object);
        }

        [Fact]
        public async Task GetAllDocumentsAsync_ReturnsAllDocuments()
        {
            // Arrange: Clear existing data and seed
            _context.Documents.RemoveRange(_context.Documents);
            await _context.SaveChangesAsync();

            _context.Documents.AddRange(
                new Document { Id = 1, Title = "Document 1" },
                new Document { Id = 2, Title = "Document 2" }
            );
            await _context.SaveChangesAsync();

            // Act
            var documents = await _repository.GetAllDocumentsAsync();

            // Assert
            Assert.NotNull(documents);
            Assert.Equal(2, documents.Count());
        }

        [Fact]
        public async Task GetDocumentByIdAsync_ExistingId_ReturnsDocument()
        {
            // Arrange: Clear existing data and seed
            _context.Documents.RemoveRange(_context.Documents);
            await _context.SaveChangesAsync();

            _context.Documents.Add(new Document { Id = 1, Title = "Document 1" });
            await _context.SaveChangesAsync();

            // Act
            var document = await _repository.GetDocumentByIdAsync(1);

            // Assert
            Assert.NotNull(document);
            Assert.Equal(1, document.Id);
            Assert.Equal("Document 1", document.Title);
        }

        [Fact]
        public async Task GetDocumentByIdAsync_NonExistingId_ReturnsNull()
        {
            // Arrange: Clear existing data
            _context.Documents.RemoveRange(_context.Documents);
            await _context.SaveChangesAsync();

            // Act
            var document = await _repository.GetDocumentByIdAsync(99);

            // Assert
            Assert.Null(document);
        }

        [Fact]
        public async Task AddDocumentAsync_ValidDocument_AddsDocument()
        {
            // Arrange: Clear existing data
            _context.Documents.RemoveRange(_context.Documents);
            await _context.SaveChangesAsync();

            var newDocument = new Document { Id = 3, Title = "Document 3" };

            // Act
            await _repository.AddDocumentAsync(newDocument);
            var documents = await _repository.GetAllDocumentsAsync();

            // Assert
            Assert.Single(documents);
            Assert.Contains(documents, d => d.Id == 3 && d.Title == "Document 3");
        }

        [Fact]
        public async Task UpdateDocumentAsync_ValidDocument_UpdatesDocument()
        {
            // Arrange: Clear existing data and seed
            _context.Documents.RemoveRange(_context.Documents);
            await _context.SaveChangesAsync();

            _context.Documents.Add(new Document { Id = 1, Title = "Document 1" });
            await _context.SaveChangesAsync();

            var documentToUpdate = await _repository.GetDocumentByIdAsync(1);
            documentToUpdate.Title = "Updated Document 1";

            // Act
            await _repository.UpdateDocumentAsync(documentToUpdate);
            var updatedDocument = await _repository.GetDocumentByIdAsync(1);

            // Assert
            Assert.NotNull(updatedDocument);
            Assert.Equal("Updated Document 1", updatedDocument.Title);
        }

        [Fact]
        public async Task DeleteDocumentAsync_ExistingId_DeletesDocument()
        {
            // Arrange: Clear existing data and seed
            _context.Documents.RemoveRange(_context.Documents);
            await _context.SaveChangesAsync();

            _context.Documents.Add(new Document { Id = 1, Title = "Document 1" });
            await _context.SaveChangesAsync();

            // Act
            await _repository.DeleteDocumentAsync(1);
            var documents = await _repository.GetAllDocumentsAsync();

            // Assert
            Assert.Empty(documents);
            Assert.DoesNotContain(documents, d => d.Id == 1);
        }

        [Fact]
        public async Task DeleteDocumentAsync_NonExistingId_LogsWarning()
        {
            // Arrange: Clear existing data
            _context.Documents.RemoveRange(_context.Documents);
            await _context.SaveChangesAsync();

            // Act
            await _repository.DeleteDocumentAsync(99);

            // Assert
            _loggerMock.Verify(
                log => log.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Attempted to delete non-existent document")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once
            );
        }
    }
}
