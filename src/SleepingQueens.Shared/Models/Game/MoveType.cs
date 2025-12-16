namespace SleepingQueens.Shared.Models.Game;

public enum MoveType
{
    DrawCard = 1,
    PlayCard = 2,
    WakeQueen = 3,
    StealQueen = 4,
    UsePotion = 5,
    BlockKnight = 6,
    EndTurn = 7,
    Discard = 8
}