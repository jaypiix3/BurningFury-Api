using BurningFuryApi.Models;
using BurningFuryApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BurningFuryApi.Controllers
{
    /// <summary>
    /// Controller for managing BurningFury players
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Tags("Players")]
    [Authorize] // Require authentication for all endpoints
    public class PlayersController : ControllerBase
    {
        private readonly IPlayerService _playerService;
        private readonly ILogger<PlayersController> _logger;

        public PlayersController(IPlayerService playerService, ILogger<PlayersController> logger)
        {
            _playerService = playerService;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves all players from the BurningFury database (Public endpoint - no authentication required)
        /// </summary>
        /// <returns>A list of all players</returns>
        /// <response code="200">Returns the list of players</response>
        /// <response code="500">If there was an internal server error</response>
        [HttpGet]
        [AllowAnonymous] // Allow public access without authentication
        [ProducesResponseType(typeof(IEnumerable<Player>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<Player>>> GetAllPlayers()
        {
            try
            {
                // Log access without requiring user authentication
                _logger.LogInformation("Public access: All players requested");
                
                var players = await _playerService.GetAllPlayersAsync();
                return Ok(players);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all players");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Retrieves a specific player by their unique identifier
        /// </summary>
        /// <param name="id">The unique identifier of the player</param>
        /// <returns>The player with the specified ID</returns>
        /// <response code="200">Returns the requested player</response>
        /// <response code="401">If the user is not authenticated</response>
        /// <response code="404">If the player is not found</response>
        /// <response code="500">If there was an internal server error</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Player), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Player>> GetPlayer(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("User {UserId} requested player {PlayerId}", userId, id);
                
                var player = await _playerService.GetPlayerByIdAsync(id);
                
                if (player == null)
                {
                    return NotFound($"Player with id {id} not found");
                }

                return Ok(player);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting player with id {PlayerId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Creates a new player in the BurningFury database
        /// </summary>
        /// <param name="player">The player object to create</param>
        /// <returns>The newly created player</returns>
        /// <response code="201">Returns the newly created player</response>
        /// <response code="400">If the player data is invalid</response>
        /// <response code="401">If the user is not authenticated</response>
        /// <response code="500">If there was an internal server error</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/players
        ///     {
        ///        "region": "US",
        ///        "realm": "Stormrage",
        ///        "name": "PlayerName"
        ///     }
        ///
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(typeof(Player), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Player>> CreatePlayer([FromBody] Player player)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("User {UserId} attempting to create player", userId);
                
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrWhiteSpace(player.Name) || 
                    string.IsNullOrWhiteSpace(player.Region) || 
                    string.IsNullOrWhiteSpace(player.Realm))
                {
                    return BadRequest("Name, Region, and Realm are required");
                }

                var createdPlayer = await _playerService.CreatePlayerAsync(player);
                _logger.LogInformation("User {UserId} created player {PlayerId}", userId, createdPlayer.Id);
                
                return CreatedAtAction(nameof(GetPlayer), new { id = createdPlayer.Id }, createdPlayer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating player");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Deletes a player from the BurningFury database
        /// </summary>
        /// <param name="id">The unique identifier of the player to delete</param>
        /// <returns>No content if successful</returns>
        /// <response code="204">If the player was successfully deleted</response>
        /// <response code="401">If the user is not authenticated</response>
        /// <response code="404">If the player is not found</response>
        /// <response code="500">If there was an internal server error</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeletePlayer(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("User {UserId} attempting to delete player {PlayerId}", userId, id);
                
                var deleted = await _playerService.DeletePlayerAsync(id);
                
                if (!deleted)
                {
                    return NotFound($"Player with id {id} not found");
                }

                _logger.LogInformation("User {UserId} deleted player {PlayerId}", userId, id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting player with id {PlayerId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets information about the current authenticated user
        /// </summary>
        /// <returns>User information from the JWT token</returns>
        /// <response code="200">Returns the current user information</response>
        /// <response code="401">If the user is not authenticated</response>
        [HttpGet("me")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult GetCurrentUser()
        {
            try
            {
                var userId = GetCurrentUserId();
                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
                var userName = User.FindFirst(ClaimTypes.Name)?.Value;
                
                return Ok(new
                {
                    UserId = userId,
                    Email = userEmail,
                    Name = userName,
                    Claims = User.Claims.Select(c => new { c.Type, c.Value })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user information");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Helper method to extract the current user ID from the JWT token
        /// </summary>
        /// <returns>The current user's ID</returns>
        private string GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                   User.FindFirst("sub")?.Value ?? 
                   "unknown";
        }
    }
}