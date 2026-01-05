namespace DocumentLoader.Models
{
    public class OcrResult
    {
        public int DocumentId { get; set; } = 0;    
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public string Bucket { get; set; } = null!;
        public string ObjectName { get; set; } = null!;
        public string OcrText { get; set; } = string.Empty;
    }
}
