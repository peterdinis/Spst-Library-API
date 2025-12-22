namespace AuthorService.Repositories
{
    public interface IUnitOfWork : IDisposable
    {
        IAuthorRepository Authors { get; }
        Task<int> CompleteAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}