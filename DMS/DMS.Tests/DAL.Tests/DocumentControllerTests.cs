using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using DAL.Controllers;
using DAL.Repositories;
using DAL.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DMS.Tests.DAL.Tests
{
    public class DocumentControllerTests
    {
        private readonly Mock<IDocumentRepository> _repositoryMock;
        private readonly DocumentController _controller;

        public DocumentControllerTests()
        {
            _repositoryMock = new Mock<IDocumentRepository>();
            _controller = new DocumentController(_repositoryMock.Object);
        }

        [Fact]
        public async Task GetAsync_ReturnsAllDocuments()
        {
            // Arrange
            var documents = new List<Document>
    {
        new Document { Id = 1, Title = "Doc 1" },
        new Document { Id = 2, Title = "Doc 2" }
    };
            _repositoryMock.Setup(repo => repo.GetAllDocumentsAsync())
                .ReturnsAsync(documents);

            // Act
            var result = await _controller.GetAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task GetByIdAsync_ExistingId_ReturnsDocument()
        {
            // Arrange
            var document = new Document { Id = 1, Title = "Doc 1" };
            _repositoryMock.Setup(repo => repo.GetDocumentByIdAsync(1))
                .ReturnsAsync(document);

            // Act
            var result = await _controller.GetByIdAsync(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedDocument = Assert.IsType<Document>(okResult.Value);
            Assert.Equal(1, returnedDocument.Id);
            Assert.Equal("Doc 1", returnedDocument.Title);
        }

        [Fact]
        public async Task GetByIdAsync_NonExistingId_ReturnsNotFound()
        {
            // Arrange
            _repositoryMock.Setup(repo => repo.GetDocumentByIdAsync(99))
                .ReturnsAsync((Document)null);

            // Act
            var result = await _controller.GetByIdAsync(99);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task CreateAsync_ValidDocument_ReturnsOk()
        {
            // Arrange
            var document = new Document { Id = 1, Title = "Doc 1" };

            _repositoryMock.Setup(repo => repo.AddDocumentAsync(document))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.CreateAsync(document);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedDocument = Assert.IsType<Document>(okResult.Value);
            Assert.Equal(1, returnedDocument.Id);
            Assert.Equal("Doc 1", returnedDocument.Title);
        }

        [Fact]
        public async Task UpdateAsync_ExistingId_UpdatesAndReturnsNoContent()
        {
            // Arrange
            var existingDocument = new Document { Id = 1, Title = "Old Title" };
            var updatedDocument = new Document { Id = 1, Title = "New Title", OcrText = "Updated Text" };

            _repositoryMock.Setup(repo => repo.GetDocumentByIdAsync(1))
                .ReturnsAsync(existingDocument);

            _repositoryMock.Setup(repo => repo.UpdateDocumentAsync(It.IsAny<Document>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.UpdateAsync(1, updatedDocument);

            // Assert
            Assert.IsType<NoContentResult>(result);
            Assert.Equal("New Title", existingDocument.Title);
            Assert.Equal("Updated Text", existingDocument.OcrText);
        }

        [Fact]
        public async Task UpdateAsync_NonExistingId_ReturnsNotFound()
        {
            // Arrange
            var updatedDocument = new Document { Id = 99, Title = "Updated Title" };

            _repositoryMock.Setup(repo => repo.GetDocumentByIdAsync(99))
                .ReturnsAsync((Document)null);

            // Act
            var result = await _controller.UpdateAsync(99, updatedDocument);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteAsync_ExistingId_DeletesAndReturnsNoContent()
        {
            // Arrange
            var document = new Document { Id = 1, Title = "Doc 1" };

            _repositoryMock.Setup(repo => repo.GetDocumentByIdAsync(1))
                .ReturnsAsync(document);

            _repositoryMock.Setup(repo => repo.DeleteDocumentAsync(1))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.DeleteAsync(1);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteAsync_NonExistingId_ReturnsNotFound()
        {
            // Arrange
            _repositoryMock.Setup(repo => repo.GetDocumentByIdAsync(99))
                .ReturnsAsync((Document)null);

            // Act
            var result = await _controller.DeleteAsync(99);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
    }
}
