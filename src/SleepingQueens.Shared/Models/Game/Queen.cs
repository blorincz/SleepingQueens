using SleepingQueens.Shared.Models.Game.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SleepingQueens.Shared.Models.Game;

public class Queen
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column(TypeName = "nvarchar(20)")]
    public QueenType Type { get; set; }

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    public int PointValue { get; set; }

    [MaxLength(500)]
    public string ImagePath { get; set; } = string.Empty;

    public bool IsAwake { get; set; } = false;

    // Navigation properties
    public Guid? PlayerId { get; set; }
    public virtual Player? Player { get; set; }

    [Required]
    public Guid GameId { get; set; }
    public virtual Game Game { get; set; } = null!;
}