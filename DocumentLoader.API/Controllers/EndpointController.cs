using DocumentLoader.DAL.Repositories;
using DocumentLoader.Models;
using DocumentLoader.RabbitMQ;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Minio;
using Minio.DataModel.Args;
using System.IO;
using System.Reflection.Metadata;
using System.Text.Json;


namespace DocumentLoader.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentRepository _repository;
        private readonly IMinioClient _minioClient;
        private const string BucketName = "uploads";

        public DocumentsController(IDocumentRepository repository)
        {
            _repository = repository;
            _minioClient = new MinioClient()
                .WithEndpoint("minio", 9000)
                .WithCredentials("minioadmin", "minioadmin")
                .WithSSL(false)
                .Build();
        }

        [HttpPost("upload")]
        [RequestSizeLimit(100_000_000)]
        public async Task<IActionResult> Upload(IFormFile file, [FromServices] Minio.MinioClient minioClient)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");


            try
            {
                // Ensure bucket exists
                bool exists = await minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(BucketName));
                if (!exists)
                {
                    await minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(BucketName));
                }

                // Upload file to MinIO
                using var fileStream = file.OpenReadStream();

                await _minioClient.PutObjectAsync(new PutObjectArgs()
                .WithBucket(BucketName)
                .WithObject(file.FileName)
                .WithStreamData(fileStream)
                .WithObjectSize(file.Length));

                // Save metadata to database
                var document = new Models.Document
                {
                    FileName = file.FileName,
                    FilePath = $"minio://{BucketName}/{file.FileName}",
                    UploadedAt = DateTime.UtcNow,
                    Summary = ""
                };
                await _repository.AddAsync(document);

                var job = new OcrJob
                {
                    DocumentId = document.Id,
                    UploadedAt = document.UploadedAt,
                    Bucket = BucketName,
                    ObjectName = file.FileName
                };

                // Serialize and publish
                RabbitMqPublisher.Instance.Publish(RabbitMqQueues.OCR_QUEUE, JsonSerializer.Serialize(job));

                return Created($"/documents/{document.Id}", new { document.Id, document.UploadedAt, document.FileName, Bucket = BucketName });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }


        // Search endpoint (basic example using repository)
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? query)
        {


            // Simple search: return all documents where FileName or Summary contains the query
            var allDocs = await _repository.GetAllAsync();

            if (string.IsNullOrWhiteSpace(query))
                return Ok(new { Results = allDocs }); //TODO: DAL get every datapoint

            var results = allDocs
                .Where(d => d.FileName.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || d.Summary.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(d => new { d.Id, d.FileName, d.Summary });

            return Ok(new
            {
                Query = query,
                Results = results
            });
        }

        // delete object from database
        [HttpDelete("delete")]
        public IActionResult Delete([FromQuery] int? document_id)
        {
            if (document_id == null)
                return BadRequest("No Document ID provided");
            try
            {
                int id = (int)document_id;
            }
            catch
            {
                return BadRequest("Provided Document ID could not be converted to Integer");
            }


            // Todo: Delete document with corresponding document_id from db

            return Ok("Document with the provided id " + document_id + " has been deleted");
        }
        public class UpdateDocumentDto
        {
            public int DocumentId { get; set; }
            public required string Content { get; set; }
        }

        // update object from database
        [HttpPut("update")]
        public IActionResult Update([FromBody] UpdateDocumentDto dto)
        {
            if (dto == null || dto.DocumentId <= 0) return BadRequest();
            if (string.IsNullOrWhiteSpace(dto.Content)) return BadRequest();

            // Update document in DB
            return Ok($"Document with ID {dto.DocumentId} has been updated");


            // Todo: Update document with corresponding document_id from db

            //return Ok("Document with the provided id " + dto.DocumentId + " has been updated");
        }
    }
}
