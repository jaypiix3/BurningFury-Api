using BurningFuryApi.Models;
using BurningFuryApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BurningFuryApi.Controllers
{
    /// <summary>
    /// Public controller for browsing BurningFury players without authentication
    /// </summary>
    [ApiController]
    [Route("api/public/[controller]")]
    [Produces("application/json")]
    [Tags("Public Players")]
    [AllowAnonymous] // Allow public access without authentication
    public class PublicPlayersController : ControllerBase
    {
        private readonly IPlayerService _playerService;
        private readonly ILogger<PublicPlayersController> _logger;

        public PublicPlayersController(IPlayerService playerService, ILogger<PublicPlayersController> logger)
        {
            _playerService = playerService;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves all players with pagination and search functionality (Public endpoint - no authentication required)
        /// </summary>
        /// <param name="search">Optional search term to filter players by name (case-insensitive)</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Number of items per page (default: 10, max: 100)</param>
        /// <returns>A paginated list of players matching the search criteria</returns>
        /// <response code="200">Returns the paginated list of players</response>
        /// <response code="400">If the pagination parameters are invalid</response>
        /// <response code="500">If there was an internal server error</response>
        /// <remarks>
        /// Sample requests:
        /// 
        ///     GET /api/public/publicplayers
        ///     GET /api/public/publicplayers?page=2&amp;pageSize=20
        ///     GET /api/public/publicplayers?search=PlayerName
        ///     GET /api/public/publicplayers?search=Player&amp;page=1&amp;pageSize=5
        /// 
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(typeof(PaginatedResult<Player>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PaginatedResult<Player>>> GetAllPlayers(
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var parameters = new PlayerSearchParameters
                {
                    Search = search,
                    Page = page,
                    PageSize = pageSize
                };

                // Validate parameters
                if (page < 1)
                {
                    return BadRequest("Page number must be greater than 0");
                }

                if (pageSize < 1 || pageSize > 100)
                {
                    return BadRequest("Page size must be between 1 and 100");
                }

                _logger.LogInformation("Public access: Players requested with search '{Search}', page {Page}, pageSize {PageSize}", 
                    search, page, pageSize);
                
                var result = await _playerService.GetPlayersAsync(parameters);
                
                _logger.LogInformation("Public access: Returned {Count} players out of {Total} total", 
                    result.Items.Count(), result.TotalItems);
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting players with search '{Search}', page {Page}, pageSize {PageSize}", 
                    search, page, pageSize);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Retrieves a specific player by their unique identifier (Public endpoint - no authentication required)
        /// </summary>
        /// <param name="id">The unique identifier of the player</param>
        /// <returns>The player with the specified ID</returns>
        /// <response code="200">Returns the requested player</response>
        /// <response code="404">If the player is not found</response>
        /// <response code="500">If there was an internal server error</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Player), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Player>> GetPlayer(Guid id)
        {
            try
            {
                _logger.LogInformation("Public access: Player {PlayerId} requested", id);
                
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
    }
}