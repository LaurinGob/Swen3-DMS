namespace DocumentLoader.Models
{
    public class OcrJob
    {
        public string Bucket { get; set; } = null!;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public int DocumentId { get; set; } = 0;
        public string ObjectName { get; set; } = null!;
    }
}