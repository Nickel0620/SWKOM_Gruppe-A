using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using DAL.Entities;
using DAL.Repositories;
using AutoMapper;
using REST_API.Controllers;
using REST_API.DTOs;
using Microsoft.Extensions.Logging;

namespace DMS.Tests.ControllerTests
{
    public class DocumentControllerTests
    {
        private readonly DocumentController _controller;
        private readonly Mock<IDocumentRepository> _mockRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ILogger<DocumentController>> _mockLogger;

        public DocumentControllerTests()
        {
            _mockRepository = new Mock<IDocumentRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockLogger = new Mock<ILogger<DocumentController>>();
            _controller = new DocumentController(_mockRepository.Object, _mockMapper.Object, _mockLogger.Object);
        }

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
    }
}
