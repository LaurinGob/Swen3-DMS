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
        public async Task<IActionResult> Search([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Search query cannot be empty.");

            var allDocs = await _repository.GetAllAsync();
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
        public async Task<IActionResult> Delete([FromQuery]int? document_id)
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
       

        // update object from database
        [HttpPut("update")]
        public async Task<IActionResult> Update([FromBody] UpdateDocumentDto dto)
        {
            if (dto == null || dto.DocumentId <= 0) return BadRequest();
            if (string.IsNullOrWhiteSpace(dto.Content)) return BadRequest();

            // Update document in DB
            return Ok($"Document with ID {dto.DocumentId} has been updated");


            // Todo: Update document with corresponding document_id from db

            //return Ok("Document with the provided id " + dto.DocumentId + " has been updated");
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
