using ChastiMaid.Core.Models;
using ChastiMaid.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ChastiMaid.Core.Helpers;

public static class DataSeeder
{
    public static void EnsureSeedData(DatabaseService dbService)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string seedPath = Path.Combine(baseDir, "seed.json");
        using var db = dbService.GetDatabase();
        var itemsCol = db.GetCollection<OutfitItem>("clothing_items");

        if (itemsCol.Count() > 0) return;

        try
        {
            string jsonContent;

            if (File.Exists(seedPath))
            {
                // Load from external file
                jsonContent = File.ReadAllText(seedPath);
            }
            else
            {
                // Fallback internal string in case the file is missing
                jsonContent = "[]";
                System.Diagnostics.Debug.WriteLine($"Warning: {seedPath} not found.");
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var items = JsonSerializer.Deserialize<List<OutfitItem>>(jsonContent, options);

            if (items != null && items.Any())
            {
                itemsCol.InsertBulk(items);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during seeding: {ex.Message}");
        }
    }
}