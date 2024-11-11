using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using DAL.Entities;
using DAL.Repositories;
using AutoMapper;
using REST_API.Controllers;
using REST_API.DTOs;
using Microsoft.Extensions.Logging;
using REST_API.Services;

namespace DMS.Tests.ControllerTests
{
    public class DocumentControllerTests
    {
        private readonly DocumentController _controller;
        private readonly Mock<IDocumentRepository> _mockRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ILogger<DocumentController>> _mockLogger;
        private readonly Mock<RabbitMQPublisher> _rabbitMQPublisher;

        public DocumentControllerTests()
        {
            _mockRepository = new Mock<IDocumentRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockLogger = new Mock<ILogger<DocumentController>>();
            _controller = new DocumentController(_mockRepository.Object, _mockMapper.Object, _mockLogger.Object, _rabbitMQPublisher.Object);
        }

        #region GET Tests

        [Fact]
        public async Task Get_ReturnsOkResult_WithDocuments()
        {
            // Arrange
            var documents = new List<Document>
            {
                new Document { Id = 1, Title = "Doc1", Content = "Content1" },
                new Document { Id = 2, Title = "Doc2", Content = "Content2" }
            };

            var documentDTOs = new List<DocumentDTO>
            {
                new DocumentDTO { Title = "Doc1", Content = "Content1" },
                new DocumentDTO { Title = "Doc2", Content = "Content2" }
            };

            _mockRepository.Setup(repo => repo.GetAllDocumentsAsync()).ReturnsAsync(documents);
            _mockMapper.Setup(m => m.Map<IEnumerable<DocumentDTO>>(It.IsAny<IEnumerable<Document>>())).Returns(documentDTOs);

            // Act
            var result = await _controller.Get();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsAssignableFrom<IEnumerable<DocumentDTO>>(okResult.Value);
            Assert.Equal(2, returnValue.Count());
        }

        [Fact]
        public async Task GetById_ReturnsNotFound_WhenDocumentDoesNotExist()
        {
            // Arrange
            _mockRepository.Setup(repo => repo.GetDocumentByIdAsync(1)).ReturnsAsync((Document)null);

            // Act
            var result = await _controller.GetById(1);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetById_ReturnsOkResult_WithDocument()
        {
            // Arrange
            var document = new Document { Id = 1, Title = "Doc1", Content = "Content1" };
            var documentDTO = new DocumentDTO { Title = "Doc1", Content = "Content1" };

            _mockRepository.Setup(repo => repo.GetDocumentByIdAsync(1)).ReturnsAsync(document);
            _mockMapper.Setup(m => m.Map<DocumentDTO>(It.IsAny<Document>())).Returns(documentDTO);

            // Act
            var result = await _controller.GetById(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<DocumentDTO>(okResult.Value);
            Assert.Equal("Doc1", returnValue.Title);
        }

        #endregion

        #region POST Tests

        [Fact]
        public async Task Post_ReturnsCreatedAtAction_WhenDocumentIsValid()
        {
            // Arrange
            var documentDTO = new DocumentDTO { Title = "Doc1", Content = "Content1" };
            var document = new Document { Id = 1, Title = "Doc1", Content = "Content1" };

            _mockMapper.Setup(m => m.Map<Document>(documentDTO)).Returns(document);
            _mockRepository.Setup(repo => repo.AddDocumentAsync(document)).Returns(Task.CompletedTask);
            _mockMapper.Setup(m => m.Map<DocumentDTO>(document)).Returns(documentDTO);

            // Act
            var result = await _controller.Post(documentDTO);

            // Assert
            var createdResult = Assert.IsType<ActionResult<DocumentDTO>>(result);
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(createdResult.Result);
            Assert.Equal("GetById", createdAtActionResult.ActionName);
            Assert.Equal(1, createdAtActionResult.RouteValues["id"]);

            var returnedDTO = Assert.IsType<DocumentDTO>(createdAtActionResult.Value);
            Assert.Equal(documentDTO.Title, returnedDTO.Title);
            Assert.Equal(documentDTO.Content, returnedDTO.Content);
        }

        #endregion

        #region DELETE Tests

        [Fact]
        public async Task Delete_ReturnsNotFound_WhenDocumentDoesNotExist()
        {
            // Arrange
            _mockRepository.Setup(repo => repo.GetDocumentByIdAsync(1)).ReturnsAsync((Document)null);

            // Act
            var result = await _controller.Delete(1);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Delete_ReturnsNoContent_WhenDocumentIsDeleted()
        {
            // Arrange
            var document = new Document { Id = 1, Title = "Doc1", Content = "Content1" };
            _mockRepository.Setup(repo => repo.GetDocumentByIdAsync(1)).ReturnsAsync(document);

            // Act
            var result = await _controller.Delete(1);

            // Assert
            Assert.IsType<NoContentResult>(result);
            _mockRepository.Verify(repo => repo.DeleteDocumentAsync(1), Times.Once);
        }

        #endregion

        #region PUT Tests

        [Fact]
        public async Task Put_ReturnsNoContent_WhenDocumentIsValidAndExists()
        {
            // Arrange
            int documentId = 1;
            var existingDocument = new Document { Id = documentId, Title = "Old Title", Content = "Old Content" };
            var updatedDocumentDTO = new DocumentDTO { Title = "New Title", Content = "New Content" };

            _mockRepository.Setup(repo => repo.GetDocumentByIdAsync(documentId)).ReturnsAsync(existingDocument);
            _mockMapper.Setup(m => m.Map<Document>(updatedDocumentDTO)).Returns(existingDocument);
            _mockRepository.Setup(repo => repo.UpdateDocumentAsync(existingDocument)).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Put(documentId, updatedDocumentDTO);

            // Assert
            Assert.IsType<NoContentResult>(result);
            Assert.Equal("New Title", existingDocument.Title);
            Assert.Equal("New Content", existingDocument.Content);
            _mockRepository.Verify(repo => repo.UpdateDocumentAsync(existingDocument), Times.Once);
        }

        [Fact]
        public async Task Put_ReturnsBadRequest_WhenIdIsInvalid()
        {
            // Arrange
            var documentDTO = new DocumentDTO { Title = "Doc1", Content = "Content1" };

            // Act
            var result = await _controller.Put(0, documentDTO); // Invalid ID

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            var badRequestResult = result as BadRequestObjectResult;
            Assert.Equal("Invalid document ID.", badRequestResult.Value);
        }

        [Fact]
        public async Task Put_ReturnsNotFound_WhenDocumentDoesNotExist()
        {
            // Arrange
            int documentId = 1;
            var documentDTO = new DocumentDTO { Title = "New Title", Content = "New Content" };

            _mockRepository.Setup(repo => repo.GetDocumentByIdAsync(documentId)).ReturnsAsync((Document)null); // No document found

            // Act
            var result = await _controller.Put(documentId, documentDTO);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Put_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            // Arrange
            var documentDTO = new DocumentDTO(); // Invalid state (assuming Title and Content are required)

            _controller.ModelState.AddModelError("Title", "The Title field is required."); // Simulating validation error

            // Act
            var result = await _controller.Put(1, documentDTO);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        #endregion
    }
}
