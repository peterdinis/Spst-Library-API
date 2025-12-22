using CategoryService.Messages;

namespace CategoryService.Interfaces
{
    public interface IBookService
    {
        Task<List<BookDto>> GetBooksByCategoryAsync(int categoryId);
        Task<int> GetBooksCountByCategoryAsync(int categoryId);
        void Dispose();
    }
}