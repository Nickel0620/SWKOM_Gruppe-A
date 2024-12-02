using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.DataModel.Response;
using Moq;
using REST_API.Controllers;
using Xunit;
using System.Linq;

namespace DMS.Tests.REST_API.Tests
{
    public class FileControllerTests
    {
        private readonly Mock<IMinioClient> _mockMinioClient;
        private readonly Mock<ILogger<FileController>> _mockLogger;
        private readonly FileController _controller;

        public FileControllerTests()
        {
            _mockMinioClient = new Mock<IMinioClient>();
            _mockLogger = new Mock<ILogger<FileController>>();
            _controller = new FileController(_mockLogger.Object)
            {
                _minioClient = _mockMinioClient.Object
            };
        }

        [Fact]
        public async Task UploadFile_ReturnsOkResultOnSuccess()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            var content = "file content";
            var fileName = "test.txt";
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(content);
            writer.Flush();
            stream.Position = 0;

            fileMock.Setup(f => f.FileName).Returns(fileName);
            fileMock.Setup(f => f.Length).Returns(stream.Length);
            fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
            fileMock.Setup(f => f.ContentType).Returns("text/plain");

            _mockMinioClient
                .Setup(client => client.BucketExistsAsync(It.IsAny<BucketExistsArgs>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UploadFile(fileMock.Object);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value;

            // Use reflection to access the property
            var fileNameProperty = response.GetType().GetProperty("fileName");
            Assert.NotNull(fileNameProperty);
            var returnedFileName = fileNameProperty.GetValue(response, null)?.ToString();
            Assert.Equal(fileName, returnedFileName);
        }

        [Fact]
        public async Task UploadFile_ReturnsBadRequestForEmptyFile()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(0);

            // Act
            var result = await _controller.UploadFile(fileMock.Object);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var response = badRequestResult.Value;

            // Use reflection to access the property
            var errorProperty = response.GetType().GetProperty("error");
            Assert.NotNull(errorProperty);
            var errorMessage = errorProperty.GetValue(response, null)?.ToString();
            Assert.Equal("No file provided!", errorMessage);
        }

        [Fact]
        public async Task DownloadFile_ReturnsInternalServerErrorOnException()
        {
            // Arrange
            var fileName = "test.txt";
            _mockMinioClient
                .Setup(client => client.GetObjectAsync(It.IsAny<GetObjectArgs>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Download error"));

            // Act
            var result = await _controller.DownloadFile(fileName);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task DeleteFile_ReturnsInternalServerErrorOnException()
        {
            // Arrange
            var fileName = "test.txt";
            _mockMinioClient
                .Setup(client => client.RemoveObjectAsync(It.IsAny<RemoveObjectArgs>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Delete error"));

            // Act
            var result = await _controller.DeleteFile(fileName);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task EnsureBucketExists_CreatesBucketIfNotExists()
        {
            // Arrange
            _mockMinioClient
                .Setup(client => client.BucketExistsAsync(It.IsAny<BucketExistsArgs>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false); // Simulate bucket does not exist

            _mockMinioClient
                .Setup(client => client.MakeBucketAsync(It.IsAny<MakeBucketArgs>(), It.IsAny<CancellationToken>()))
                .Verifiable(); // Expect MakeBucketAsync to be called

            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("test.txt");
            fileMock.Setup(f => f.Length).Returns(1);
            fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[1]));
            fileMock.Setup(f => f.ContentType).Returns("text/plain");

            // Act
            await _controller.UploadFile(fileMock.Object);

            // Assert
            _mockMinioClient.Verify(client => client.MakeBucketAsync(It.IsAny<MakeBucketArgs>(), It.IsAny<CancellationToken>()), Times.Once);

            // Verify logger log message
            _mockLogger.Verify(
                logger => logger.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("created successfully")),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task EnsureBucketExists_DoesNotCreateBucketIfExists()
        {
            // Arrange
            _mockMinioClient
                .Setup(client => client.BucketExistsAsync(It.IsAny<BucketExistsArgs>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true); // Simulate bucket exists

            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("test.txt");
            fileMock.Setup(f => f.Length).Returns(1);
            fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[1]));
            fileMock.Setup(f => f.ContentType).Returns("text/plain");

            // Act
            await _controller.UploadFile(fileMock.Object);

            // Assert
            _mockMinioClient.Verify(client => client.MakeBucketAsync(It.IsAny<MakeBucketArgs>(), It.IsAny<CancellationToken>()), Times.Never);

            // Verify that `Log` was called
            _mockLogger.Verify(
                logger => logger.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("already exists")),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task ListFiles_ReturnsListOfFiles()
        {
            // Arrange
            var mockObjects = new List<Item>
            {
                new Item { Key = "file1.txt" },
                new Item { Key = "file2.txt" }
            };

            // Create a mocked observable that emits the mock items
            var observableMock = new Mock<IObservable<Item>>();
            observableMock.Setup(obs => obs.Subscribe(It.IsAny<IObserver<Item>>()))
                          .Callback<IObserver<Item>>(observer =>
                          {
                              foreach (var item in mockObjects)
                              {
                                  observer.OnNext(item); // Emit each item
                              }
                              observer.OnCompleted(); // Signal completion
                          });

            _mockMinioClient
                .Setup(client => client.ListObjectsAsync(It.IsAny<ListObjectsArgs>(), It.IsAny<CancellationToken>()))
                .Returns(observableMock.Object);

            // Act
            var result = await _controller.ListFiles();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var files = Assert.IsType<List<string>>(okResult.Value);
            Assert.Equal(2, files.Count);
            Assert.Contains("file1.txt", files);
            Assert.Contains("file2.txt", files);
        }

        [Fact]
        public async Task ListFiles_ReturnsInternalServerErrorOnException()
        {
            // Arrange
            _mockMinioClient
                .Setup(client => client.ListObjectsAsync(It.IsAny<ListObjectsArgs>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception("Error listing files")); // Simulate exception for ListObjectsAsync

            // Act
            var result = await _controller.ListFiles();

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            var response = statusCodeResult.Value;

            // Use reflection to access the "error" property
            var errorProperty = response.GetType().GetProperty("error");
            Assert.NotNull(errorProperty);
            var errorMessage = errorProperty.GetValue(response, null)?.ToString();
            Assert.Contains("Error listing files", errorMessage);
        }

    }
}
