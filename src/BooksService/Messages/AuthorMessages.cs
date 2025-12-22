namespace BooksService.Messages
{
    public class AuthorExistsRequest
    {
        public int AuthorId { get; set; }
        public string RequestId { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class AuthorExistsResponse
    {
        public bool Exists { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}