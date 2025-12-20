using Microsoft.AspNetCore.Mvc;
using SleepingQueens.Data.Repositories;
using SleepingQueens.Server.GameEngine;
using SleepingQueens.Shared.Models.Game;
using SleepingQueens.Shared.Models.Game.Enums;
using SleepingQueens.Server.Logging;
using SleepingQueens.Shared.Models.DTOs;

namespace SleepingQueens.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GamesController(
    IGameRepository gameRepository,
    IGameEngine gameEngine,
    ILogger<GamesController> logger) : ControllerBase
{
    private readonly IGameRepository _gameRepository = gameRepository;
    private readonly IGameEngine _gameEngine = gameEngine;
    private readonly ILogger<GamesController> _logger = logger;

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<GameInfoDto>>> GetActiveGames()
    {
        try
        {
            var games = await _gameRepository.GetActiveGamesAsync();
            var dtos = games.Select(g => new GameInfoDto
            {
                Id = g.Id,
                Code = g.Code,
                Status = g.Status,
                PlayerCount = g.Players.Count,
                MaxPlayers = g.MaxPlayers,
                CreatedAt = g.CreatedAt
            });

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active games");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GameDto>> GetGame(Guid id)
    {
        try
        {
            var game = await _gameRepository.GetByIdAsync(id);
            if (game == null)
                return NotFound();

            var dto = new GameDto
            {
                Id = game.Id,
                Code = game.Code,
                Status = game.Status,
                Phase = game.Phase,
                MaxPlayers = game.MaxPlayers,
                TargetScore = game.TargetScore,
                CreatedAt = game.CreatedAt,
                StartedAt = game.StartedAt,
                EndedAt = game.EndedAt
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogGameFetchError(ex, id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<CreateGameResponseDto>> CreateGame([FromBody] CreateGameRequestDto request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.PlayerName))
                return BadRequest("Player name is required");

            var creator = new Player
            {
                Name = request.PlayerName,
                Type = PlayerType.Human
            };

            var game = _gameEngine.CreateGame(request.Settings ?? GameSettings.Default, creator);

            await _gameRepository.InitializeNewGameAsync(game, creator);

            var response = new CreateGameResponseDto
            {
                GameId = game.Id,
                GameCode = game.Code,
                PlayerId = creator.Id,
                Success = true
            };

            _logger.LogGameCreated(game.Code);

            return CreatedAtAction(nameof(GetGame), new { id = game.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating game via API");
            return BadRequest(new CreateGameResponseDto
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    [HttpPost("{gameCode}/join")]
    public async Task<ActionResult<JoinGameResponseDto>> JoinGame(string gameCode, [FromBody] JoinGameRequestDto request)
    {
        try
        {
            var game = await _gameRepository.GetByCodeAsync(gameCode);
            if (game == null)
                return NotFound(new JoinGameResponseDto
                {
                    Success = false,
                    ErrorMessage = "Game not found"
                });

            if (game.IsFull())
                return BadRequest(new JoinGameResponseDto
                {
                    Success = false,
                    ErrorMessage = "Game is full"
                });

            if (game.Status != GameStatus.Waiting)
                return BadRequest(new JoinGameResponseDto
                {
                    Success = false,
                    ErrorMessage = "Game already started"
                });

            var player = new Player
            {
                Name = request.PlayerName,
                Type = PlayerType.Human,
                GameId = game.Id
            };

            var addedPlayer = await _gameRepository.AddPlayerAsync(game.Id, player);

            return Ok(new JoinGameResponseDto
            {
                Success = true,
                GameId = game.Id,
                PlayerId = addedPlayer.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogGameJoinError(ex, gameCode);
            return BadRequest(new JoinGameResponseDto
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    [HttpPost("{id}/start")]
    public async Task<ActionResult> StartGame(Guid id)
    {
        try
        {
            var game = await _gameRepository.GetByIdAsync(id);
            if (game == null)
                return NotFound();

            if (game.Status != GameStatus.Waiting)
                return BadRequest("Game already started");

            if (game.Players.Count < game.Settings.MinPlayers)
                return BadRequest($"Need at least {game.Settings.MinPlayers} players to start");

            await _gameEngine.StartGameAsync(id);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogGameStartError(ex, id);
            return BadRequest(ex.Message);
        }
    }
}