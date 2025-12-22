namespace OrderService.DTOs;

public record BookDto(
    int Id,
    string Title,
    string Author,
    int AuthorId,
    string Publisher,
    int Year,
    string ISBN,
    int Pages,
    int CategoryId,
    string Category,
    string Language,
    string Description,
    string PhotoPath,
    bool IsAvailable,
    DateTime AddedToLibrary
);
