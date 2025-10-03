using BurningFuryApi.Models;
using Npgsql;
using System.Text;

namespace BurningFuryApi.Services
{
    public class PlayerService : IPlayerService
    {
        private readonly string _connectionString;
        private readonly ILogger<PlayerService> _logger;

        public PlayerService(IConfiguration configuration, ILogger<PlayerService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new ArgumentNullException(nameof(configuration), "DefaultConnection not found");
            _logger = logger;
        }

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("Starting database initialization...");
                
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                var createTableCommand = """
                    CREATE TABLE IF NOT EXISTS Player (
                        Id UUID PRIMARY KEY,
                        Region VARCHAR(100) NOT NULL,
                        Realm VARCHAR(100) NOT NULL,
                        Name VARCHAR(100) NOT NULL,
                        MainRaid BOOLEAN NOT NULL DEFAULT FALSE
                    );
                    """;

                using var command = new NpgsqlCommand(createTableCommand, connection);
                await command.ExecuteNonQueryAsync();

                // Add the MainRaid column if it doesn't exist (for existing databases)
                var addColumnCommand = """
                    ALTER TABLE Player 
                    ADD COLUMN IF NOT EXISTS MainRaid BOOLEAN NOT NULL DEFAULT FALSE;
                    """;

                using var addColumnCmd = new NpgsqlCommand(addColumnCommand, connection);
                await addColumnCmd.ExecuteNonQueryAsync();

                _logger.LogInformation("Player table created or already exists");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing database. Connection string: {ConnectionString}", 
                    _connectionString?.Substring(0, Math.Min(50, _connectionString.Length)) + "...");
                throw;
            }
        }

        public async Task<bool> CheckDatabaseConnectionAsync()
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database connection check failed");
                return false;
            }
        }

        public async Task<IEnumerable<Player>> GetAllPlayersAsync()
        {
            var players = new List<Player>();

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new NpgsqlCommand("SELECT Id, Region, Realm, Name, MainRaid FROM Player", connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    players.Add(new Player
                    {
                        Id = (Guid)reader["Id"],
                        Region = (string)reader["Region"],
                        Realm = (string)reader["Realm"],
                        Name = (string)reader["Name"],
                        MainRaid = (bool)reader["MainRaid"]
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all players");
                throw;
            }

            return players;
        }

        public async Task<Player?> GetPlayerByIdAsync(Guid id)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new NpgsqlCommand("SELECT Id, Region, Realm, Name, MainRaid FROM Player WHERE Id = @id", connection);
                command.Parameters.AddWithValue("@id", id);

                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new Player
                    {
                        Id = (Guid)reader["Id"],
                        Region = (string)reader["Region"],
                        Realm = (string)reader["Realm"],
                        Name = (string)reader["Name"],
                        MainRaid = (bool)reader["MainRaid"]
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting player with id {PlayerId}", id);
                throw;
            }

            return null;
        }

        public async Task<Player> CreatePlayerAsync(Player player)
        {
            try
            {
                player.Id = Guid.NewGuid();

                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new NpgsqlCommand(
                    "INSERT INTO Player (Id, Region, Realm, Name, MainRaid) VALUES (@id, @region, @realm, @name, @mainRaid)",
                    connection);

                command.Parameters.AddWithValue("@id", player.Id);
                command.Parameters.AddWithValue("@region", player.Region);
                command.Parameters.AddWithValue("@realm", player.Realm);
                command.Parameters.AddWithValue("@name", player.Name);
                command.Parameters.AddWithValue("@mainRaid", player.MainRaid);

                await command.ExecuteNonQueryAsync();

                _logger.LogInformation("Player created with id {PlayerId}", player.Id);

                return player;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating player");
                throw;
            }
        }

        public async Task<Player?> UpdatePlayerAsync(Guid id, Player player)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                using (var existsCmd = new NpgsqlCommand("SELECT COUNT(*) FROM Player WHERE Id = @id", connection))
                {
                    existsCmd.Parameters.AddWithValue("@id", id);
                    var count = (long)await existsCmd.ExecuteScalarAsync();
                    if (count == 0)
                    {
                        return null;
                    }
                }

                using var updateCmd = new NpgsqlCommand("""
                    UPDATE Player
                    SET Region = @region,
                        Realm = @realm,
                        Name = @name,
                        MainRaid = @mainRaid
                    WHERE Id = @id
                    RETURNING Id, Region, Realm, Name, MainRaid;
                    """, connection);

                updateCmd.Parameters.AddWithValue("@id", id);
                updateCmd.Parameters.AddWithValue("@region", player.Region);
                updateCmd.Parameters.AddWithValue("@realm", player.Realm);
                updateCmd.Parameters.AddWithValue("@name", player.Name);
                updateCmd.Parameters.AddWithValue("@mainRaid", player.MainRaid);

                using var reader = await updateCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var updated = new Player
                    {
                        Id = (Guid)reader["Id"],
                        Region = (string)reader["Region"],
                        Realm = (string)reader["Realm"],
                        Name = (string)reader["Name"],
                        MainRaid = (bool)reader["MainRaid"]
                    };
                    _logger.LogInformation("Player {PlayerId} updated", id);
                    return updated;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating player {PlayerId}", id);
                throw;
            }
        }

        public async Task<bool> DeletePlayerAsync(Guid id)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new NpgsqlCommand("DELETE FROM Player WHERE Id = @id", connection);
                command.Parameters.AddWithValue("@id", id);

                var rowsAffected = await command.ExecuteNonQueryAsync();

                var deleted = rowsAffected > 0;
                if (deleted)
                {
                    _logger.LogInformation("Player with id {PlayerId} deleted", id);
                }
                else
                {
                    _logger.LogWarning("Player with id {PlayerId} not found for deletion", id);
                }

                return deleted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting player with id {PlayerId}", id);
                throw;
            }
        }

        public async Task<PaginatedResult<Player>> GetPlayersAsync(PlayerSearchParameters parameters)
        {
            parameters.Normalize();
            
            var result = new PaginatedResult<Player>
            {
                Page = parameters.Page,
                PageSize = parameters.PageSize
            };

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Build the WHERE clause for search
                var whereClause = new StringBuilder();
                var searchParameter = string.Empty;
                
                if (!string.IsNullOrEmpty(parameters.Search))
                {
                    whereClause.Append("WHERE LOWER(Name) LIKE LOWER(@search)");
                    searchParameter = $"%{parameters.Search}%";
                }

                // Get total count
                var countQuery = $"SELECT COUNT(*) FROM Player {whereClause}";
                using var countCommand = new NpgsqlCommand(countQuery, connection);
                
                if (!string.IsNullOrEmpty(searchParameter))
                {
                    countCommand.Parameters.AddWithValue("@search", searchParameter);
                }

                result.TotalItems = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

                // Get paginated results
                var offset = (parameters.Page - 1) * parameters.PageSize;
                var dataQuery = $@"
                    SELECT Id, Region, Realm, Name, MainRaid 
                    FROM Player 
                    {whereClause}
                    ORDER BY Name
                    LIMIT @pageSize OFFSET @offset";

                using var dataCommand = new NpgsqlCommand(dataQuery, connection);
                dataCommand.Parameters.AddWithValue("@pageSize", parameters.PageSize);
                dataCommand.Parameters.AddWithValue("@offset", offset);
                
                if (!string.IsNullOrEmpty(searchParameter))
                {
                    dataCommand.Parameters.AddWithValue("@search", searchParameter);
                }

                var players = new List<Player>();
                using var reader = await dataCommand.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    players.Add(new Player
                    {
                        Id = (Guid)reader["Id"],
                        Region = (string)reader["Region"],
                        Realm = (string)reader["Realm"],
                        Name = (string)reader["Name"],
                        MainRaid = (bool)reader["MainRaid"]
                    });
                }

                result.Items = players;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting players with search '{Search}', page {Page}, pageSize {PageSize}", 
                    parameters.Search, parameters.Page, parameters.PageSize);
                throw;
            }

            return result;
        }
    }
}