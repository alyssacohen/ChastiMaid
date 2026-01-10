using System;

namespace ChastiMaid.Core.Models
{
    /// <summary>
    /// Represents the persistent state of the game to prevent cheating.
    /// This object will be stored encrypted in LiteDB.
    /// </summary>
    public class GameState
    {
        public int Id { get; set; }
        public int TotalExperience { get; set; }
        public int Level { get; set; }
        public TimeSpan TotalPlayTime { get; set; }
        public DateTime LastSessionDate { get; set; }

        /// <summary>
        /// Current active task being timed.
        /// </summary>
        public string? CurrentTaskName { get; set; }
    }
}