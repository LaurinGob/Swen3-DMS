using DocumentLoader.DAL.Repositories;
using DocumentLoader.Models;
using DocumentLoader.RabbitMQ;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Minio;
using Minio.DataModel.Args;
using System.IO;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Threading.Tasks;
using DocumentLoader.API.Services;


namespace DocumentLoader.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentRepository _repository;
        private readonly IMinioClient _minioClient;
        private readonly IAccessLogService _service;
        private readonly ILogger _logger;
        private const string BucketName = "uploads";

        public DocumentsController(ILogger<DocumentsController> logger, IDocumentRepository repository, IAccessLogService service)
        {
            _repository = repository;
            _logger = logger;
            _service = service;
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
                    Summary = ""
                };
                await _repository.AddAsync(document);

                var job = new OcrJob
                {
                    DocumentId = document.Id,
                    Bucket = BucketName,
                    ObjectName = file.FileName
                };

                // Serialize and publish
                RabbitMqPublisher.Instance.Publish(RabbitMqQueues.OCR_QUEUE, JsonSerializer.Serialize(job));

                return Created($"/documents/{document.Id}", new { document.Id, document.FileName, Bucket = BucketName });
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
                return Ok(new { results = allDocs }); //TODO: DAL get every datapoint

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
        public async Task<IActionResult> Delete([FromQuery] int? document_id)
        {
            if (document_id == null)
                return BadRequest("No Document ID provided");
            try
            {
                int id = (int)document_id;
                await _repository.DeleteAsync(id);
                return Ok("Document with the provided id " + document_id + " has been deleted");
            }
            catch(Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}, Document could not be deleted" });
            }

        }

        public class UpdateDocumentDto
        {
            public int DocumentId { get; set; }
            public required string Content { get; set; }
        }

        // update object from database
        [HttpPut("update")]
        public async Task<IActionResult> Update([FromBody] UpdateDocumentDto dto)
        {
            if (dto == null || dto.DocumentId <= 0) return BadRequest();
            if (string.IsNullOrWhiteSpace(dto.Content)) return BadRequest();

            await _repository.UpdateAsync(new Models.Document
            {
                Id = dto.DocumentId,
                Summary = dto.Content
            });

            return Ok($"Document with ID {dto.DocumentId} has been updated");


        }

        [HttpPost("{documentId}/summaries")]
        public async Task<IActionResult> GenerateSummary(int documentId)
        {
            var doc = await _repository.GetByIdAsync(documentId);
            if (doc == null)
                return NotFound($"Document with ID {documentId} not found.");

            if (!string.IsNullOrWhiteSpace(doc.Summary))
                return BadRequest("Document already has a summary.");

            var job = new OcrJob
            {
                DocumentId = doc.Id,
                Bucket = "uploads",
                ObjectName = doc.FileName
            };

            RabbitMqPublisher.Instance.Publish(
                RabbitMqQueues.OCR_QUEUE,
                JsonSerializer.Serialize(job)
            );

            return Accepted(new
            {
                message = "Summary generation triggered.",
                documentId = doc.Id
            });
        }


        [HttpGet("{documentId}")]

        public async Task<IActionResult> GetById(int documentId)
        {
            var doc = await _repository.GetByIdAsync(documentId);

            if (doc == null)
                return NotFound();

           _logger.LogDebug(doc.Id, doc.FileName, doc.Summary);

            return Ok(new
            {
                id = doc.Id,
                fileName = doc.FileName,
                summary = doc.Summary,
                uploadedAr = doc.UploadedAt
            });

        }


        [HttpPost("accesses")]

        public async Task<IActionResult> StoreDailyAccess(DailyAccessDto dto)
        {
            await _service.StoreDailyAsync(
                dto.Date,
                dto.DocumentId,
                dto.AccessCount);

            _logger.LogInformation($"Stored daily access for Document ID {dto.DocumentId} on {dto.Date} with count {dto.AccessCount}");
            return Ok();
        }
    }
}
