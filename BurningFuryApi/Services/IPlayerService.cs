using BurningFuryApi.Models;

namespace BurningFuryApi.Services
{
    public interface IPlayerService
    {
        Task InitializeDatabaseAsync();
        Task<bool> CheckDatabaseConnectionAsync();
        Task<IEnumerable<Player>> GetAllPlayersAsync();
        Task<PaginatedResult<Player>> GetPlayersAsync(PlayerSearchParameters parameters);
        Task<Player?> GetPlayerByIdAsync(Guid id);
        Task<Player> CreatePlayerAsync(Player player);
        Task<Player?> UpdatePlayerAsync(Guid id, Player player);
        Task<bool> DeletePlayerAsync(Guid id);
    }
}