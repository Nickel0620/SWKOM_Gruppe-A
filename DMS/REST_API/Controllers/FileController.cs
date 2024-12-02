using Microsoft.AspNetCore.Mvc;
using Minio.DataModel.Args;
using System.Reactive.Linq;
using Minio;

namespace REST_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FileController : ControllerBase
    {
        public IMinioClient _minioClient;
        private readonly ILogger<FileController> _logger;
        private const string BucketName = "uploads";

        public FileController(ILogger<FileController> logger)
        {
            _minioClient = new MinioClient()
                .WithEndpoint("minio", 9000)
                .WithCredentials("minioadmin", "minioadmin")
                .WithSSL(false)
                .Build();

            _logger = logger;
        }

        [HttpGet("files")]
        public async Task<IActionResult> ListFiles()
        {
            var objects = new List<string>();

            try
            {
                await _minioClient.ListObjectsAsync(new ListObjectsArgs().WithBucket(BucketName))
                    .ForEachAsync(item =>
                    {
                        objects.Add(item.Key);
                    });

                _logger.LogInformation("Successfully retrieved list of files from bucket '{BucketName}'", BucketName);
                return Ok(objects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing files in bucket '{BucketName}'", BucketName);
                return StatusCode(500, new { error = $"Error listing files: {ex.Message}" });
            }
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile([FromForm] IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    _logger.LogWarning("Upload attempted with no file or empty file.");
                    return BadRequest(new { error = "No file provided!" });
                }

                await EnsureBucketExists();

                var fileName = Path.GetFileName(file.FileName);
                await using var fileStream = file.OpenReadStream();

                await _minioClient.PutObjectAsync(new PutObjectArgs()
                    .WithBucket(BucketName)
                    .WithObject(fileName)
                    .WithStreamData(fileStream)
                    .WithObjectSize(file.Length)
                    .WithContentType(file.ContentType));

                _logger.LogInformation("Successfully uploaded file '{FileName}' to bucket '{BucketName}'", fileName, BucketName);
                return Ok(new { fileName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file to bucket '{BucketName}'", BucketName);
                return StatusCode(500, new { error = "Internal Server Error", details = ex.Message });
            }
        }

        [HttpGet("download/{fileName}")]
        public async Task<IActionResult> DownloadFile(string fileName)
        {
            var memoryStream = new MemoryStream();

            try
            {
                await _minioClient.GetObjectAsync(new GetObjectArgs()
                    .WithBucket(BucketName)
                    .WithObject(fileName)
                    .WithCallbackStream(stream =>
                    {
                        stream.CopyTo(memoryStream);
                    }));

                memoryStream.Position = 0;
                _logger.LogInformation("Successfully downloaded file '{FileName}' from bucket '{BucketName}'", fileName, BucketName);
                return File(memoryStream, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file '{FileName}' from bucket '{BucketName}'", fileName, BucketName);
                return StatusCode(500, new { error = $"Error downloading file: {ex.Message}" });
            }
        }

        private async Task EnsureBucketExists()
        {
            try
            {
                bool found = await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(BucketName));
                if (!found)
                {
                    await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(BucketName));
                    _logger.LogInformation("Bucket '{BucketName}' created successfully.", BucketName);
                }
                else
                {
                    _logger.LogInformation("Bucket '{BucketName}' already exists.", BucketName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring bucket '{BucketName}' exists.", BucketName);
                throw; // Re-throw to let the caller handle the error
            }
        }

        [HttpDelete("delete/{fileName}")]
        public async Task<IActionResult> DeleteFile(string fileName)
        {
            try
            {
                await _minioClient.RemoveObjectAsync(new RemoveObjectArgs()
                    .WithBucket(BucketName)
                    .WithObject(fileName));

                _logger.LogInformation("Successfully deleted file '{FileName}' from bucket '{BucketName}'", fileName, BucketName);
                return Ok(new { message = $"File '{fileName}' successfully deleted." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file '{FileName}' from bucket '{BucketName}'", fileName, BucketName);
                return StatusCode(500, new { message = $"Error deleting file: {ex.Message}" });
            }
        }
    }
}
