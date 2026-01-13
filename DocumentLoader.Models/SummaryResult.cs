namespace DocumentLoader.Models
{
    public class SummaryResult
    {
        public int DocumentId { get; set; }
        public string ObjectName { get; set; } = string.Empty;
        public string SummaryText { get; set; } = string.Empty;
    }
}
