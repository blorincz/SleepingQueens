namespace SleepingQueens.Shared.Models.DTOs;

public class QueenDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PointValue { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public Guid? PlayerId { get; set; }
    public bool IsAwake { get; set; }
}
