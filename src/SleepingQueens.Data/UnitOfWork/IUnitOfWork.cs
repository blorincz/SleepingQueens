using SleepingQueens.Data.Repositories;

namespace SleepingQueens.Data.UnitOfWork;

public interface IUnitOfWork : IDisposable
{
    IGameRepository Games { get; }

    Task<int> CompleteAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}