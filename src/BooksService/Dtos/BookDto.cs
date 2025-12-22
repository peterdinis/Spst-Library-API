using System;

namespace BooksService.Dtos
{
    public class BookDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string Author { get; set; } = null!;
        public string Publisher { get; set; } = null!;
        public int Year { get; set; }
        public string ISBN { get; set; } = null!;
        public int Pages { get; set; }
        public string Category { get; set; } = null!;
        public int CategoryId {get; set;}

        public int AuthorId { get; set; }
        public string Language { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string PhotoPath { get; set; } = null!;
        public bool IsAvailable { get; set; }
        public DateTime? AddedToLibrary { get; set; }
    }
}
