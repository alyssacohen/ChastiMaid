using LiteDB;
using ChastiMaid.Core.Models;
using System;
using System.IO;

namespace ChastiMaid.Core.Services
{
    /// <summary>
    /// Database manager for ChastiMaid. Handles encrypted storage and 
    /// provides access to game and wardrobe collections.
    /// </summary>
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string password)
        {
            string baseFolder = AppDomain.CurrentDomain.BaseDirectory;
            string dataFolder = Path.Combine(baseFolder, "Data");

            if (!Directory.Exists(dataFolder))
                Directory.CreateDirectory(dataFolder);

            string dbPath = Path.Combine(dataFolder, "GameData.db");
            
            // Password enables AES encryption for the whole file
            _connectionString = $"Filename={dbPath};Password={password};Connection=shared";
            
            InitializeIndexes();
        }

        /// <summary>
        /// Sets up indexes for faster lookups on frequently queried fields.
        /// </summary>
        private void InitializeIndexes()
        {
            using (var db = new LiteDatabase(_connectionString))
            {
                var items = db.GetCollection<OutfitItem>("clothing_items");
                items.EnsureIndex(x => x.Name);

                var rules = db.GetCollection<OutfitRule>("outfit_rules");
                rules.EnsureIndex(x => x.ItemId);
                
                var state = db.GetCollection<GameState>("game_state");
                // GameState is usually a singleton (one record), but index Id just in case
                state.EnsureIndex(x => x.Id);
            }
        }

        public ILiteCollection<OutfitItem> GetItems() => 
            new LiteDatabase(_connectionString).GetCollection<OutfitItem>("clothing_items");

        public ILiteCollection<OutfitRule> GetRules() => 
            new LiteDatabase(_connectionString).GetCollection<OutfitRule>("outfit_rules");

        public ILiteCollection<GameState> GetGameState() => 
            new LiteDatabase(_connectionString).GetCollection<GameState>("game_state");
        
        /// <summary>
        /// Exposes the underlying LiteDatabase instance.
        /// Useful for complex queries or transactions involving multiple collections.
        /// NOTE: Remember to dispose the instance (using statement) after use.
        /// </summary>
        public LiteDatabase GetDatabase()
        {
            return new LiteDatabase(_connectionString);
        }
    }
}