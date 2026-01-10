using ChastiMaid.Core.Models;
using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ChastiMaid.Core.Services
{
    public class WardrobeService
    {
        private readonly DatabaseService _dbService;

        public WardrobeService(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        /// <summary>
        /// Retrieves the outfit generated for the specific date. 
        /// If none exists, it generates a new one automatically.
        /// </summary>
        public Outfit GetTodaysOutfit()
        {
            using var db = _dbService.GetDatabase();
            var dailyCol = db.GetCollection<DailyOutfit>("daily_outfits");

            // We strip the time part to ensure we match the date correctly
            var today = DateTime.Today;
            var record = dailyCol.FindOne(x => x.Date == today);

            if (record != null)
            {
                // If found, reload the full item objects from the IDs
                return ReconstructOutfitFromIds(db, record.ItemIds);
            }

            // Otherwise, generate a fresh one
            return GenerateAndSaveNewOutfit(db, today);
        }

        /// <summary>
        /// Forces the creation of a new outfit for the given date, overwriting any existing one.
        /// </summary>
        public Outfit ForceRegenerate(DateTime date)
        {
            using var db = _dbService.GetDatabase();
            return GenerateAndSaveNewOutfit(db, date);
        }

        private Outfit GenerateAndSaveNewOutfit(LiteDatabase db, DateTime date)
        {
            var itemsCol = db.GetCollection<OutfitItem>("clothing_items");
            var rulesCol = db.GetCollection<OutfitRule>("outfit_rules");

            var allItems = itemsCol.FindAll().ToList();
            var allRules = rulesCol.FindAll().ToList();

            // Safety check: if wardrobe is empty, return empty outfit
            if (!allItems.Any())
            {
                return new Outfit();
            }

            // Initialize the Generator logic
            var generator = new OutfitGenerator(allItems, allRules);

            // Generate the outfit (passing an empty list of seed items for now)
            var newOutfit = generator.Generate(new List<OutfitItem>());

            // Save the result to the database (DailyOutfit collection)
            var dailyCol = db.GetCollection<DailyOutfit>("daily_outfits");

            // Extract the ObjectIds to store them lightly
            var itemIds = newOutfit.Items.Select(i => i.Id).ToList();

            // Clean up previous generation for this date if exists
            dailyCol.DeleteMany(x => x.Date == date);

            var dailyRecord = new DailyOutfit
            {
                Id = ObjectId.NewObjectId(),
                Date = date,
                ItemIds = itemIds
            };

            dailyCol.Insert(dailyRecord);

            return newOutfit;
        }

        private Outfit ReconstructOutfitFromIds(LiteDatabase db, List<ObjectId> ids)
        {
            if (ids == null || !ids.Any()) return new Outfit();

            var itemsCol = db.GetCollection<OutfitItem>("clothing_items");

            // LiteDB allows checking if an Id is contained in a list using LINQ
            var items = itemsCol.Find(x => ids.Contains(x.Id)).ToList();

            // Re-create the Outfit object (the constructor will recalculate layers/slots)
            return new Outfit(items);
        }
    }
}