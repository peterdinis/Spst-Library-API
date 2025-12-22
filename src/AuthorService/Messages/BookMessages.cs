namespace AuthorService.Messages
{
    public class GetBooksByAuthorRequest
    {
        public int AuthorId { get; set; }
        public string RequestId { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class GetBooksByAuthorResponse
    {
        public List<AuthorBookDto> Books { get; set; } = new();
        public string RequestId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class AuthorBookDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public int Year { get; set; }
        public string ISBN { get; set; } = string.Empty;
        public int Pages { get; set; }
        public string Category { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string Language { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PhotoPath { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public DateTime? AddedToLibrary { get; set; }
    }
}