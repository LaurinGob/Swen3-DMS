namespace DocumentLoader.Models
{
    public class Document
    {
        //test
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public string FilePath { get; set; } = string.Empty;
    }
}
