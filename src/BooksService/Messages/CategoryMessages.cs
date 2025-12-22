namespace BooksService.Messages
{
    public class CategoryExistsRequest
    {
        public int CategoryId { get; set; }
        public string RequestId { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class CategoryExistsResponse
    {
        public bool Exists { get; set; }
        public string CategoryTitle { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}