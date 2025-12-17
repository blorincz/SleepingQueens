using SleepingQueens.Shared.Data.Repositories;

namespace SleepingQueens.Server.Data.UnitOfWork;

public interface IUnitOfWork : IDisposable
{
    IGameRepository Games { get; }
    ICardRepository Cards { get; }

    Task<int> CompleteAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}