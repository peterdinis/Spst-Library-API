using AuthorService.Messages;

namespace AuthorService.Interfaces
{
    public interface IBookService
    {
        Task<List<AuthorBookDto>> GetBooksByAuthorAsync(int authorId);
        Task<int> GetBooksCountByAuthorAsync(int authorId);
        void Dispose();
    }
}