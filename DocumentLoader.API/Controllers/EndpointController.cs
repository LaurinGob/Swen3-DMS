using DocumentLoader.API.Messaging;
using DocumentLoader.DAL.Repositories;
using DocumentLoader.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Reflection.Metadata;


namespace DocumentLoader.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentRepository _repository;
        private readonly IRabbitMqPublisher _publisher;

        public DocumentsController(IDocumentRepository repository, IRabbitMqPublisher publisher)
        {
            _repository = repository;
            _publisher = publisher;
        }

        // Upload endpoint
        [HttpPost("upload")]
        [RequestSizeLimit(100_000_000)]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Ok("No file provided.");

            var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            var filePath = Path.Combine(uploadPath, file.FileName);

            try
            {
                // Save file to disk
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Save metadata to database
                var document = new Models.Document
                {
                    FileName = file.FileName,
                    FilePath = filePath,
                    UploadedAt = DateTime.UtcNow,
                    Summary = "" // optional: can fill after OCR later
                };

                await _repository.AddAsync(document);
                //await _publisher.PublishDocumentUploadedAsync(document);

                var fileUrl = $"/uploads/{file.FileName}";
                return Created(fileUrl, new { document.Id, document.FileName, Url = fileUrl });
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
