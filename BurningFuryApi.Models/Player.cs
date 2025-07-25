using System.ComponentModel.DataAnnotations;

namespace BurningFuryApi.Models
{
    /// <summary>
    /// Represents a player in the BurningFury system
    /// </summary>
    public class Player
    {
        /// <summary>
        /// Unique identifier for the player
        /// </summary>
        /// <example>550e8400-e29b-41d4-a716-446655440000</example>
        public Guid Id { get; set; }

        /// <summary>
        /// The region where the player is located
        /// </summary>
        /// <example>US</example>
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Region { get; set; } = string.Empty;

        /// <summary>
        /// The realm/server where the player character exists
        /// </summary>
        /// <example>Stormrage</example>
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Realm { get; set; } = string.Empty;

        /// <summary>
        /// The player's character name
        /// </summary>
        /// <example>PlayerName</example>
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the player is part of the main raid group
        /// </summary>
        /// <example>true</example>
        public bool MainRaid { get; set; }
    }

    /// <summary>
    /// Parameters for searching and paginating players
    /// </summary>
    public class PlayerSearchParameters
    {
        /// <summary>
        /// Search term to filter players by name (case-insensitive)
        /// </summary>
        /// <example>PlayerName</example>
        public string? Search { get; set; }

        /// <summary>
        /// Page number (1-based)
        /// </summary>
        /// <example>1</example>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Number of items per page (max 100)
        /// </summary>
        /// <example>10</example>
        public int PageSize { get; set; } = 10;

        /// <summary>
        /// Validates and normalizes the parameters
        /// </summary>
        public void Normalize()
        {
            Page = Math.Max(1, Page);
            PageSize = Math.Min(100, Math.Max(1, PageSize));
            Search = Search?.Trim();
        }
    }

    /// <summary>
    /// Paginated result for players
    /// </summary>
    public class PaginatedResult<T>
    {
        /// <summary>
        /// The items in the current page
        /// </summary>
        public IEnumerable<T> Items { get; set; } = new List<T>();

        /// <summary>
        /// Current page number (1-based)
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Number of items per page
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total number of items across all pages
        /// </summary>
        public int TotalItems { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

        /// <summary>
        /// Whether there is a previous page
        /// </summary>
        public bool HasPreviousPage => Page > 1;

        /// <summary>
        /// Whether there is a next page
        /// </summary>
        public bool HasNextPage => Page < TotalPages;
    }
}