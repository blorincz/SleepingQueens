using Microsoft.AspNetCore.Mvc;
using SleepingQueens.Server.Data.Repositories;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlayersController(IGameRepository gameRepository, ILogger<PlayersController> logger) : ControllerBase
{
    private readonly IGameRepository _gameRepository = gameRepository;
    private readonly ILogger<PlayersController> _logger = logger;

    [HttpGet("{id}")]
    public async Task<ActionResult<PlayerDto>> GetPlayer(Guid id)
    {
        try
        {
            var player = await _gameRepository.GetPlayerAsync(id);
            if (player == null)
                return NotFound();

            var dto = new PlayerDto
            {
                Id = player.Id,
                Name = player.Name,
                Type = player.Type,
                Score = player.Score,
                IsCurrentTurn = player.IsCurrentTurn,
                GameId = player.GameId
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting player {PlayerId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("game/{gameId}")]
    public async Task<ActionResult<IEnumerable<PlayerDto>>> GetPlayersInGame(Guid gameId)
    {
        try
        {
            var players = await _gameRepository.GetPlayersInGameAsync(gameId);
            var dtos = players.Select(p => new PlayerDto
            {
                Id = p.Id,
                Name = p.Name,
                Type = p.Type,
                Score = p.Score,
                IsCurrentTurn = p.IsCurrentTurn,
                GameId = p.GameId
            });

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting players for game {GameId}", gameId);
            return StatusCode(500, "Internal server error");
        }
    }
}

public class PlayerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PlayerType Type { get; set; }
    public int Score { get; set; }
    public bool IsCurrentTurn { get; set; }
    public Guid GameId { get; set; }
}