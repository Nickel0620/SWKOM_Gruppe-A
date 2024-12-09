using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using REST_API.Controllers;
using REST_API.DTOs;
using AutoMapper;
using REST_API.Services;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using DAL.Entities;
using Moq.Protected;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Elastic.Clients.Elasticsearch;

namespace DMS.Tests.REST_API.Tests
{
    public class DocumentControllerTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IMessageQueueService> _messageQueueServiceMock;
        private readonly Mock<FileController> _fileControllerMock;
        private readonly Mock<ElasticsearchClient> _elasticClientMock;
        private readonly DocumentController _controller;

        public DocumentControllerTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _mapperMock = new Mock<IMapper>();
            _messageQueueServiceMock = new Mock<IMessageQueueService>();
            _fileControllerMock = new Mock<FileController>(MockBehavior.Strict, null);
            _elasticClientMock = new Mock<ElasticsearchClient>();

            _controller = new DocumentController(
                _httpClientFactoryMock.Object,
                _mapperMock.Object,
                _messageQueueServiceMock.Object,
                _fileControllerMock.Object,
                _elasticClientMock.Object,
                Mock.Of<ILogger<DocumentController>>()); 
        }

        [Fact]
        public async Task Get_ReturnsOkWithDocuments()
        {
            // Arrange
            var documents = new List<Document>
            {
                new Document { Id = 1, Title = "Test Document 1" },
                new Document { Id = 2, Title = "Test Document 2" }
            };
                    var dtoDocuments = new List<DocumentDTO>
            {
                new DocumentDTO { Id = 1, Title = "Test Document 1" },
                new DocumentDTO { Id = 2, Title = "Test Document 2" }
            };

            var jsonResponse = JsonSerializer.Serialize(documents);

            var mockClient = new Mock<HttpMessageHandler>();
            mockClient.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse)
                });

            var client = new HttpClient(mockClient.Object)
            {
                BaseAddress = new Uri("http://localhost") 
            };

            _httpClientFactoryMock.Setup(f => f.CreateClient("DAL")).Returns(client);
            _mapperMock.Setup(m => m.Map<IEnumerable<DocumentDTO>>(It.IsAny<IEnumerable<Document>>())).Returns(dtoDocuments);

            // Act
            var result = await _controller.Get();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedDocuments = Assert.IsType<List<DocumentDTO>>(okResult.Value);
            Assert.Equal(2, returnedDocuments.Count);
            Assert.Equal(1, returnedDocuments[0].Id);
            Assert.Equal("Test Document 1", returnedDocuments[0].Title);
            Assert.Equal(2, returnedDocuments[1].Id);
            Assert.Equal("Test Document 2", returnedDocuments[1].Title);
        }

        [Fact]
        public async Task Get_ReturnsErrorWhenDALFails()
        {
            // Arrange
            var mockClient = new Mock<HttpMessageHandler>();
            mockClient.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError
                });

            var client = new HttpClient(mockClient.Object)
            {
                BaseAddress = new Uri("http://localhost") 
            };
            _httpClientFactoryMock.Setup(f => f.CreateClient("DAL")).Returns(client);

            // Act
            var result = await _controller.Get();

            // Assert
            var errorResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, errorResult.StatusCode);
        }

        //[Fact]
        //public async Task Create_ValidDocument_ReturnsCreated()
        //{
        //    // Arrange
        //    var documentDto = new DocumentDTO { Id = 1, Title = "Test Title" };
        //    var document = new Document { Id = 1, Title = "Test Title" };
        //    var jsonResponse = JsonSerializer.Serialize(document);

        //    var mockClient = new Mock<HttpMessageHandler>();
        //    mockClient.Protected()
        //        .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
        //        .ReturnsAsync(new HttpResponseMessage
        //        {
        //            StatusCode = HttpStatusCode.Created,
        //            Content = new StringContent(jsonResponse)
        //        });

        //    var client = new HttpClient(mockClient.Object)
        //    {
        //        BaseAddress = new Uri("http://localhost") 
        //    };
        //    _httpClientFactoryMock.Setup(f => f.CreateClient("DAL")).Returns(client);
        //    _mapperMock.Setup(m => m.Map<Document>(It.IsAny<DocumentDTO>())).Returns(document);

        //    // Act
        //    var result = await _controller.Create(documentDto);

        //    // Assert
        //    var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        //    var returnedDocument = Assert.IsType<Document>(createdResult.Value);
        //    Assert.Equal(1, returnedDocument.Id);
        //    Assert.Equal("Test Title", returnedDocument.Title);
        //}

        [Fact]
        public async Task Create_ValidDocument_ReturnsOk()
        {
            // Arrange
            var documentDto = new DocumentDTO { Id = 1, Title = "Test Title" };
            var document = new Document { Id = 1, Title = "Test Title" };
            var jsonResponse = JsonSerializer.Serialize(document);

            var mockClient = new Mock<HttpMessageHandler>();
            mockClient.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Created,
                    Content = new StringContent(jsonResponse)
                });

            var client = new HttpClient(mockClient.Object)
            {
                BaseAddress = new Uri("http://localhost")
            };
            _httpClientFactoryMock.Setup(f => f.CreateClient("DAL")).Returns(client);
            _mapperMock.Setup(m => m.Map<Document>(It.IsAny<DocumentDTO>())).Returns(document);

            // Act
            var result = await _controller.Create(documentDto);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result); // Expect ObjectResult
            Assert.Equal(200, objectResult.StatusCode); // HTTP 200 OK

            var returnedDocument = Assert.IsType<Document>(objectResult.Value); // Validate returned value
            Assert.Equal(1, returnedDocument.Id);
            Assert.Equal("Test Title", returnedDocument.Title);
        }


        [Fact]
        public async Task Create_InvalidModel_ReturnsBadRequest()
        {
            // Arrange
            var documentDto = new DocumentDTO { Id = 1, Title = "Test Title" };
            _controller.ModelState.AddModelError("CreatedAt", "CreatedAt is required");

            // Act
            var result = await _controller.Create(documentDto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Update_ValidDocument_ReturnsNoContent()
        {
            // Arrange
            var documentDto = new DocumentDTO { Id = 1, Title = "Updated Title" };
            var document = new Document { Id = 1, Title = "Updated Title" };

            var mockClient = new Mock<HttpMessageHandler>();
            mockClient.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NoContent
                });

            var client = new HttpClient(mockClient.Object)
            {
                BaseAddress = new Uri("http://localhost") 
            };
            _httpClientFactoryMock.Setup(f => f.CreateClient("DAL")).Returns(client);
            _mapperMock.Setup(m => m.Map<Document>(It.IsAny<DocumentDTO>())).Returns(document);

            // Act
            var result = await _controller.Update(1, documentDto);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }
    }

}
