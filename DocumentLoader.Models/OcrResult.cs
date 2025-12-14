namespace DocumentLoader.Models
{
    public class OcrResult
    {
        public int DocumentId { get; set; } = 0;        
        public string Bucket { get; set; } = null!;
        public string ObjectName { get; set; } = null!;
        public string OcrText { get; set; } = string.Empty;
    }
}
