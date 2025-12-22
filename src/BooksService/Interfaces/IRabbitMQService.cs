using BooksService.Messages;

namespace BooksService.Interfaces
{
    public interface IRabbitMQService
    {
        Task<CategoryExistsResponse> GetCategoryAsync(int categoryId);
        Task<AuthorExistsResponse> GetAuthorAsync(int authorId);
        void Dispose();
    }
}