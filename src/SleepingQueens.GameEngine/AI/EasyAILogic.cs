using Microsoft.Extensions.Logging;
using SleepingQueens.Server.Data.Repositories;

namespace SleepingQueens.GameEngine.AI;

public class EasyAILogic(IGameRepository gameRepository, ILogger logger) : IAILogic
{
    private readonly IGameRepository _gameRepository = gameRepository;
    private readonly ILogger _logger = logger;

    public async Task<AIDecision> DecideMoveAsync(Guid gameId, Guid aiPlayerId)
    {
        // Simple random AI logic
        var random = new Random();
        var actions = new[] { AIAction.PlayCard, AIAction.DrawCard, AIAction.EndTurn };
        var action = actions[random.Next(actions.Length)];

        return new AIDecision(action);
    }
}