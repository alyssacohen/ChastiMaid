using LiteDB;
using System;
using System.Collections.Generic;

namespace ChastiMaid.Core.Models
{
    /// <summary>
    /// Stores the generated outfit for a specific date.
    /// Ensures persistence across application restarts on the same day.
    /// </summary>
    public class DailyOutfit
    {
        public ObjectId Id { get; set; } = ObjectId.NewObjectId();
        public DateTime Date { get; set; }

        /// <summary>
        /// The list of IDs that were selected for this outfit.
        /// We store IDs to keep the DB light; we'll reload full objects at runtime.
        /// </summary>
        public List<ObjectId> ItemIds { get; set; } = new();

        /// <summary>
        /// The random seed used for this generation (useful for debugging or reproduction).
        /// </summary>
        public int GenerationSeed { get; set; }

        /// <summary>
        /// Whether the user has accepted/completed this outfit assignment.
        /// </summary>
        public bool IsAccepted { get; set; } = false;

        public bool IsRejected { get; set; } = false;
    }

    /// <summary>
    /// Configuration object to pass specific rules for generation
    /// (e.g., forced items or modified probabilities).
    /// </summary>
    public class GenerationRules
    {
        public List<ObjectId> ForcedItemIds { get; set; } = new();
        public Dictionary<string, double>? LayerProbabilities { get; set; }
    }
}