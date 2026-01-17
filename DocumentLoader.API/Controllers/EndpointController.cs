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


namespace DocumentLoader.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentRepository _repository;
        private readonly IMinioClient _minioClient;
        private readonly ILogger _logger;
        private const string BucketName = "uploads";

        public DocumentsController(ILogger<DocumentsController> logger, IDocumentRepository repository)
        {
            _repository = repository;
            _logger = logger;
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
                return BadRequest("No file uploaded.");

            var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
            if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

            var filePath = Path.Combine(uploadPath, file.FileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var document = new Document
            {
                FileName = file.FileName,
                FilePath = filePath,
                UploadedAt = DateTime.UtcNow,
                Summary = ""
            };

            await _repository.AddAsync(document);

            return Created($"/uploads/{file.FileName}", new UploadResultDto
            {
                Id = document.Id,
                FileName = document.FileName,
                Url = $"/uploads/{file.FileName}"
            });
        }


        // Search endpoint (basic example using repository)
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? query)
        {


            var allDocs = await _repository.GetAllAsync();

            if (string.IsNullOrWhiteSpace(query))
                return Ok(new { results = allDocs }); //TODO: DAL get every datapoint

            var results = allDocs
                .Where(d => d.FileName.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || d.Summary.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(d => new DocumentDto { Id = d.Id, FileName = d.FileName, Summary = d.Summary });

            return Ok(new SearchResultDto
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

        public class UpdateDocumentDto
        {
            public int DocumentId { get; set; }
            public string Content { get; set; }
        }

        public class DocumentDto
        {
            public int Id { get; set; }
            public string FileName { get; set; } = "";
            public string Summary { get; set; } = "";
        }

        public class SearchResultDto
        {
            public string Query { get; set; } = "";
            public IEnumerable<DocumentDto> Results { get; set; } = new List<DocumentDto>();
        }

        public class UploadResultDto
        {
            public int Id { get; set; }
            public string FileName { get; set; } = "";
            public string Url { get; set; } = "";
        }

    }
}
