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

        // delete object from database
        [HttpPost("delete")]
        public async Task<IActionResult> Delete([FromQuery] string document_id)
        {
            if (string.IsNullOrWhiteSpace(document_id))
                return BadRequest("No Document ID provided");
            try
            {
                int id = int.Parse(document_id);
            }
            catch
            {
                return BadRequest("Provided Document ID could not be converted to Integer");
            }
            

            // Todo: Delete document with corresponding document_id from db

            return Ok("Document with the provided id " + document_id + " has been deleted");
        }

        // delete object from database
        [HttpPost("update")]
        public async Task<IActionResult> Update([FromQuery] string document_id, [FromQuery] string content)
        {
            if (string.IsNullOrWhiteSpace(document_id))
                return BadRequest("No Document ID provided");
            else if (string.IsNullOrWhiteSpace(content))
                return BadRequest("Content must not be empty");

            try
            {
                int id = int.Parse(document_id);
            }
            catch
            {
                return BadRequest("Provided Document ID could not be converted to Integer");
            }


            // Todo: Update document with corresponding document_id from db

            return Ok("Document with the provided id " + document_id + " has been updated");
        }
    }
}
