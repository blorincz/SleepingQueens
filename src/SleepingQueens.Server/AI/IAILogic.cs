namespace SleepingQueens.Server.AI;

public interface IAILogic
{
    Task<AIDecision> DecideMoveAsync(Guid gameId, Guid aiPlayerId);
}

public record AIDecision(
    AIAction Action,
    Guid? CardId = null,
    Guid? TargetPlayerId = null,
    Guid? TargetQueenId = null);

public enum AIAction
{
    PlayCard,
    DrawCard,
    EndTurn,
    Discard
}
