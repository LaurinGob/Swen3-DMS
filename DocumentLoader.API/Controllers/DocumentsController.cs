using DocumentLoader.DAL.Repositories;
using DocumentLoader.Models;
using DocumentLoader.RabbitMQ;
using DocumentLoader.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Minio;
using Minio.DataModel.Args;
using System.IO;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Nodes;


namespace DocumentLoader.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentRepository _repository;
        private readonly IUserRepository _userRepository;
        private readonly IMinioClient _minioClient;
        private readonly IAccessLogService _service;
        private readonly ILogger _logger;
        private readonly IRabbitMqPublisher _publisher;
        private const string BucketName = "uploads";
        private readonly ElasticsearchClient _elasticClient;


        public DocumentsController(ILogger<DocumentsController> logger, IDocumentRepository repository, IUserRepository userRepository, IAccessLogService service, IRabbitMqPublisher publisher, ElasticsearchClient client, IMinioClient minioClient)
        {
            _repository = repository;
            _userRepository = userRepository;
            _logger = logger;
            _service = service;
            _minioClient = minioClient;
            _publisher = publisher;
            _elasticClient = client;
        }

        [HttpPost("upload")]
        [RequestSizeLimit(100_000_000)]
        public async Task<IActionResult> Upload(IFormFile file, [FromForm] string username)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest("Username is required.");
            try
            {
                var user = await _userRepository.GetOrCreateUserAsync(username);
                // Ensure bucket exists
                bool exists = await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(BucketName));
                if (!exists)
                {
                    await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(BucketName));
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
                    Summary = "",
                    UserId = user.Id,
                };
                await _repository.AddAsync(document);

                var job = new OcrJob
                {
                    DocumentId = document.Id,
                    Bucket = BucketName,
                    ObjectName = file.FileName
                };

                // Serialize and publish
                await _publisher.PublishAsync(RabbitMqQueues.OCR_QUEUE, JsonSerializer.Serialize(job));

                return Created($"/documents/{document.Id}", new { document.Id, document.FileName, Bucket = BucketName, CreatedBy = user.Username });

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
            try
            {
                IEnumerable<Models.Document> documents;

                if (string.IsNullOrWhiteSpace(query))
                {
                    documents = await _repository.GetAllAsync();
                }
                else
                {
                    var response = await _elasticClient.SearchAsync<Models.Document>(s => s
                        .Index("documents")
                        .Query(q => q.MultiMatch(m => m
                            .Fields(new[] { "fileName", "summary", "content", "user.username" })
                            .Query(query)
                            .Fuzziness(new Fuzziness(2))))
                    );

                    if (!response.IsSuccess())
                    {
                        _logger.LogWarning("Elasticsearch Suche nicht erfolgreich: {debug}", response.DebugInformation);
                        return Ok(new { results = new List<object>() });
                    }
                    documents = response.Documents;
                }

                var formattedResults = documents.Select(d => new
                {
                    d.Id,
                    d.FileName,
                    d.Summary,
                    d.UploadedAt,
                    Username = d.User?.Username ?? "System"
                });

                return Ok(new { results = formattedResults });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler im Search-Endpoint");
                return Ok(new { results = new List<object>(), error = ex.Message });
            }
        }

        [HttpPost("searches/querystring")]
        public async Task<IActionResult> SearchByQueryString([FromBody] string searchTerm)
        {
            // Analytics Log für Swen
            _logger.LogInformation("ANALYTICS_EVENT: WildcardSearch | Term: {term}", searchTerm);

            if (string.IsNullOrWhiteSpace(searchTerm))
                return BadRequest(new { message = "Search term cannot be empty" });

            // Suche über QueryString (erlaubt Wildcards wie *test*)
            var response = await _elasticClient.SearchAsync<Models.Document>(s => s
                .Index("documents")
                .Query(q => q
                    .QueryString(qs => qs
                        .Query($"*{searchTerm}*")
                    // searches in all fields
                    )
                )
                .Sort(sort => sort.Field(f => f.UploadedAt, sc => sc.Order(SortOrder.Desc)))
            );

            return HandleSearchResponse(response);
        }

        // Fuzzy search
        [HttpPost("searches/fuzzy")]
        public async Task<IActionResult> SearchByFuzzy([FromBody] string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return BadRequest(new { message = "Search term cannot be empty" });
            }
            _logger.LogInformation("ANALYTICS_EVENT: Search | Term: {term} | Time: {time}", searchTerm, DateTime.UtcNow);

            var response = await _elasticClient.SearchAsync<Models.Document>(s => s
                .Index("documents")
                .Query(q => q
                    .MultiMatch(m => m
                        .Fields(new[] { "fileName", "summary", "content", "user.username" }) //searches everywhere
                        .Query(searchTerm)
                        .Fuzziness(new Fuzziness(2))
                    )
                )
                .Sort(sort => sort.Field(f => f.UploadedAt, sc => sc.Order(SortOrder.Desc)))
            );
            if (!response.IsSuccess()) return StatusCode(500, "Elasticsearch error");

            return Ok(new { results = response.Documents });
        }
        private IActionResult HandleSearchResponse(SearchResponse<Models.Document> response)
        {
            if (response.IsValidResponse)
            {
                // return documents if there 
                return MapAndReturnResults(response.Documents);
            }
            // if elasticsearch fails
            _logger.LogError("Elasticsearch search failed: {debug}", response.DebugInformation);
            return StatusCode(500, new { message = "Failed to search documents", details = response.DebugInformation });
        }

        private IActionResult MapAndReturnResults(IEnumerable<Models.Document> documents)
        {
            var formattedResults = documents.Select(d => new
            {
                d.Id,
                d.FileName,
                d.Summary,
                d.UploadedAt,
                Username = d.User?.Username ?? "System"
            });

            return Ok(new { results = formattedResults });
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
            catch (Exception ex)
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

            // update summary
            await _repository.UpdateAsync(dto.DocumentId, dto.Content);

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

            await _publisher.PublishAsync(
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
                uploadedAt = doc.UploadedAt,
                username = doc.User?.Username ?? "Unknown"
            });

        }


        [HttpPost("accesses")]

        public async Task<IActionResult> StoreBatchAccess([FromBody] List<DailyAccessDto> dtos)
        {
            if (dtos == null || !dtos.Any()) return BadRequest("No data provided.");

            var success = await _service.StoreBatchAsync(dtos);

            if (!success)
            {
                // Return 400 instead of 500
                return BadRequest("Batch failed. Verify all Document IDs exist in the system.");
            }

            return Ok(new { Message = $"Successfully processed {dtos.Count} entries." });
        }
    }
}
