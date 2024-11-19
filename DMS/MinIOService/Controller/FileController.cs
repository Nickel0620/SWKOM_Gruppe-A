using Microsoft.AspNetCore.Mvc;
using Minio;
using Minio.DataModel.Args;
using System.Reactive.Linq;

namespace MinioService.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class FileController : ControllerBase
    {
        private readonly IMinioClient _minioClient;
        private const string BucketName = "uploads";

        public FileController()
        {
            _minioClient = new MinioClient()
                .WithEndpoint("localhost", 9000)
                .WithCredentials("minioadmin", "minioadmin")
                .WithSSL(false)
                .Build();
        }

        [HttpGet("files")]
        public async Task<IActionResult> ListFiles()
        {
            var objects = new List<string>();

            await _minioClient.ListObjectsAsync(new ListObjectsArgs().WithBucket(BucketName))
                .ForEachAsync(item =>
                {
                    objects.Add(item.Key);
                });

            return Ok(objects);
        }



        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Datei fehlt!");

            await EnsureBucketExists();

            var fileName = Path.GetFileName(file.FileName);
            await using var fileStream = file.OpenReadStream();

            await _minioClient.PutObjectAsync(new PutObjectArgs()
                .WithBucket(BucketName)
                .WithObject(fileName)
                .WithStreamData(fileStream)
                .WithObjectSize(file.Length));

            return Ok(new { fileName });
        }

        [HttpGet("download/{fileName}")]
        public async Task<IActionResult> DownloadFile(string fileName)
        {
            var memoryStream = new MemoryStream();

            await _minioClient.GetObjectAsync(new GetObjectArgs()
                .WithBucket(BucketName)
                .WithObject(fileName)
                .WithCallbackStream(stream =>
                {
                    stream.CopyTo(memoryStream);
                }));

            memoryStream.Position = 0;
            return File(memoryStream, "application/octet-stream", fileName);
        }

        private async Task EnsureBucketExists()
        {
            bool found = await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(BucketName));
            if (!found)
            {
                await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(BucketName));
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

                return Ok(new { message = $"Datei '{fileName}' erfolgreich gelöscht." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Fehler beim Löschen der Datei: {ex.Message}" });
            }
        }

    }
}
