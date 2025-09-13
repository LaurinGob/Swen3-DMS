using DocumentLoader.DAL.Repositories;
using DocumentLoader.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DocumentLoader.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentRepository _repository;

        public DocumentsController(IDocumentRepository repository)
        {
            _repository = repository;
        }

        // Upload endpoint
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

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
                var document = new Document
                {
                    FileName = file.FileName,
                    FilePath = filePath,
                    UploadedAt = DateTime.UtcNow,
                    Summary = "" // optional: can fill after OCR later
                };

                await _repository.AddAsync(document);

                var fileUrl = $"/uploads/{file.FileName}";
                return Created(fileUrl, new { document.Id, document.FileName, Url = fileUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // Search endpoint (basic example using repository)
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Search query cannot be empty.");

            // Simple search: return all documents where FileName or Summary contains the query
            var allDocs = await _repository.GetAllAsync();
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

        // Optional: add endpoints for Update/Delete/Metadata queries using repository
    }
}
