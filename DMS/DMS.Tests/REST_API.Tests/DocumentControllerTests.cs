//using System.Net;
//using System.Net.Http;
//using System.Threading.Tasks;
//using Xunit;
//using Moq;
//using Microsoft.AspNetCore.Mvc;
//using REST_API.Controllers;
//using REST_API.DTOs;
//using AutoMapper;
//using REST_API.Services;
//using System.Collections.Generic;
//using System.Text.Json;
//using System.Linq;
//using DAL.Entities;
//using Moq.Protected;

//namespace DMS.Tests.REST_API.Tests
//{
//    public class DocumentControllerTests
//    {
//        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
//        private readonly Mock<IMapper> _mapperMock;
//        private readonly Mock<IMessageQueueService> _messageQueueServiceMock;
//        private readonly DocumentController _controller;

//        public DocumentControllerTests()
//        {
//            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
//            _mapperMock = new Mock<IMapper>();
//            _messageQueueServiceMock = new Mock<IMessageQueueService>();
//            _controller = new DocumentController(_httpClientFactoryMock.Object, _mapperMock.Object, _messageQueueServiceMock.Object);
//        }

//        [Fact]
//        public async Task Get_ReturnsOkWithDocuments()
//        {
//            // Arrange
//            var mockClient = new Mock<HttpMessageHandler>();
//            var documents = new List<Document> { new Document { Id = 1, Title = "Test Title" } };
//            var jsonResponse = JsonSerializer.Serialize(documents);
//            mockClient.Protected()
//                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
//                .ReturnsAsync(new HttpResponseMessage
//                {
//                    StatusCode = HttpStatusCode.OK,
//                    Content = new StringContent(jsonResponse)
//                });

//            var client = new HttpClient(mockClient.Object);
//            _httpClientFactoryMock.Setup(f => f.CreateClient("DAL")).Returns(client);
//            _mapperMock.Setup(m => m.Map<IEnumerable<DocumentDTO>>(It.IsAny<IEnumerable<Document>>()))
//                       .Returns(documents.Select(d => new DocumentDTO { Id = d.Id, Title = d.Title }));

//            // Act
//            var result = await _controller.Get();

//            // Assert
//            var okResult = Assert.IsType<OkObjectResult>(result);
//            var returnedDocuments = Assert.IsAssignableFrom<IEnumerable<DocumentDTO>>(okResult.Value);
//            Assert.Single(returnedDocuments);
//            Assert.Equal("Test Title", returnedDocuments.First().Title);
//        }

//        [Fact]
//        public async Task Get_ReturnsErrorWhenDALFails()
//        {
//            // Arrange
//            var mockClient = new Mock<HttpMessageHandler>();
//            mockClient.Protected()
//                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
//                .ReturnsAsync(new HttpResponseMessage
//                {
//                    StatusCode = HttpStatusCode.InternalServerError
//                });

//            var client = new HttpClient(mockClient.Object);
//            _httpClientFactoryMock.Setup(f => f.CreateClient("DAL")).Returns(client);

//            // Act
//            var result = await _controller.Get();

//            // Assert
//            var errorResult = Assert.IsType<ObjectResult>(result);
//            Assert.Equal(500, errorResult.StatusCode);
//        }

//        [Fact]
//        public async Task Create_ValidDocument_ReturnsCreated()
//        {
//            // Arrange
//            var documentDto = new DocumentDTO { Id = 1, Title = "Test Title" };
//            var document = new Document { Id = 1, Title = "Test Title" };
//            var jsonResponse = JsonSerializer.Serialize(document);

//            var mockClient = new Mock<HttpMessageHandler>();
//            mockClient.Protected()
//                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
//                .ReturnsAsync(new HttpResponseMessage
//                {
//                    StatusCode = HttpStatusCode.Created,
//                    Content = new StringContent(jsonResponse)
//                });

//            var client = new HttpClient(mockClient.Object);
//            _httpClientFactoryMock.Setup(f => f.CreateClient("DAL")).Returns(client);
//            _mapperMock.Setup(m => m.Map<Document>(It.IsAny<DocumentDTO>())).Returns(document);

//            // Act
//            var result = await _controller.Create(documentDto);

//            // Assert
//            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
//            var returnedDocument = Assert.IsType<Document>(createdResult.Value);
//            Assert.Equal(1, returnedDocument.Id);
//            Assert.Equal("Test Title", returnedDocument.Title);
//        }

//        [Fact]
//        public async Task Create_InvalidModel_ReturnsBadRequest()
//        {
//            // Arrange
//            var documentDto = new DocumentDTO();
//            _controller.ModelState.AddModelError("Title", "The Title field is required.");

//            // Act
//            var result = await _controller.Create(documentDto);

//            // Assert
//            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
//            var errors = Assert.IsAssignableFrom<IEnumerable<string>>(badRequestResult.Value);
//            Assert.Contains("The Title field is required.", errors);
//        }

//        [Fact]
//        public async Task Update_ValidDocument_ReturnsNoContent()
//        {
//            // Arrange
//            var documentDto = new DocumentDTO { Id = 1, Title = "Updated Title" };
//            var document = new Document { Id = 1, Title = "Updated Title" };

//            var mockClient = new Mock<HttpMessageHandler>();
//            mockClient.Protected()
//                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
//                .ReturnsAsync(new HttpResponseMessage
//                {
//                    StatusCode = HttpStatusCode.NoContent
//                });

//            var client = new HttpClient(mockClient.Object);
//            _httpClientFactoryMock.Setup(f => f.CreateClient("DAL")).Returns(client);
//            _mapperMock.Setup(m => m.Map<Document>(It.IsAny<DocumentDTO>())).Returns(document);

//            // Act
//            var result = await _controller.Update(1, documentDto);

//            // Assert
//            Assert.IsType<NoContentResult>(result);
//        }

//        [Fact]
//        public async Task Delete_ValidId_ReturnsNoContent()
//        {
//            // Arrange
//            var mockClient = new Mock<HttpMessageHandler>();
//            mockClient.Protected()
//                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
//                .ReturnsAsync(new HttpResponseMessage
//                {
//                    StatusCode = HttpStatusCode.NoContent
//                });

//            var client = new HttpClient(mockClient.Object);
//            _httpClientFactoryMock.Setup(f => f.CreateClient("DAL")).Returns(client);

//            // Act
//            var result = await _controller.Delete(1);

//            // Assert
//            Assert.IsType<NoContentResult>(result);
//        }
//    }

//}
