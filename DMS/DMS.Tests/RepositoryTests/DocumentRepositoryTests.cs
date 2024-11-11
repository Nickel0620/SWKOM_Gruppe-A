using DAL.Context;
using DAL.Entities;
using DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using REST_API.Controllers;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace DMS.Tests.RepositoryTests
{
    public class DocumentRepositoryTests : IDisposable
    {
        private readonly DocumentContext _context;
        private readonly DocumentRepository _repository;
        private readonly Mock<ILogger<DocumentRepository>> _mockLogger;

        public DocumentRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<DocumentContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique database name for each test
                .Options;

            _mockLogger = new Mock<ILogger<DocumentRepository>>();
            _context = new DocumentContext(options);
            _repository = new DocumentRepository(_context, _mockLogger.Object);
        }

        [Fact]
        public async Task GetAllDocumentsAsync_ReturnsAllDocuments()
        {
            // Arrange
            var documents = new List<Document>
            {
                new Document { Id = 1, Title = "Doc1", Content = "Content1" },
                new Document { Id = 2, Title = "Doc2", Content = "Content2" }
            };

            await _context.Documents.AddRangeAsync(documents);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetAllDocumentsAsync();

            // Assert
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task GetDocumentByIdAsync_ReturnsDocument_WhenExists()
        {
            // Arrange
            var document = new Document { Id = 1, Title = "Doc1", Content = "Content1" };
            await _context.Documents.AddAsync(document);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetDocumentByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Doc1", result.Title);
        }

        [Fact]
        public async Task GetDocumentByIdAsync_ReturnsNull_WhenDoesNotExist()
        {
            // Act
            var result = await _repository.GetDocumentByIdAsync(1);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task AddDocumentAsync_AddsDocument()
        {
            // Arrange
            var document = new Document { Title = "Doc1", Content = "Content1" };

            // Act
            await _repository.AddDocumentAsync(document);
            var result = await _context.Documents.FindAsync(document.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Doc1", result.Title);
        }

        [Fact]
        public async Task DeleteDocumentAsync_RemovesDocument()
        {
            // Arrange
            var document = new Document { Id = 1, Title = "Doc1", Content = "Content1" };
            await _context.Documents.AddAsync(document);
            await _context.SaveChangesAsync();

            // Act
            await _repository.DeleteDocumentAsync(1);

            // Assert
            var result = await _context.Documents.FindAsync(1);
            Assert.Null(result);
        }

        public void Dispose()
        {
            // Clean up the database after each test
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
