namespace DocumentLoader.Models
{
    public class DailyAccess
    {
        public int DocumentId { get; set; }   // link to Document
        public DateOnly Date { get; set; }     // batch date
        public int AccessCount { get; set; }
    }
}
