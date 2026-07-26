using ChastiMaid.Models;
using LiteDB;

namespace ChastiMaid.Repositories;

public class ProfileRepository(LiteDatabase db)
{
    private ILiteCollection<ClothingItem> Wardrobe => db.GetCollection<ClothingItem>("wardrobe");
    private ILiteCollection<OutfitRule> OutfitRules => db.GetCollection<OutfitRule>("outfit_rules");
    private ILiteCollection<Chore> Chores => db.GetCollection<Chore>("chores");

    private ILiteCollection<PlugAndPlayItemConfig> PlugAndPlayItems =>
        db.GetCollection<PlugAndPlayItemConfig>("plug_and_play_items");

    private ILiteCollection<PlayerStats> Stats => db.GetCollection<PlayerStats>("player_stats");
    private ILiteCollection<GameSettings> Settings => db.GetCollection<GameSettings>("game_settings");

    private ILiteCollection<ForcedOutfitPreset> ForcedOutfitPresets =>
        db.GetCollection<ForcedOutfitPreset>("forced_outfit_presets");

    public List<ClothingItem> GetWardrobe()
    {
        return Wardrobe.FindAll().ToList();
    }

    public void SaveClothingItem(ClothingItem item)
    {
        Wardrobe.Upsert(item);
    }

    public void DeleteClothingItem(int id)
    {
        Wardrobe.Delete(id);
    }

    public List<OutfitRule> GetOutfitRules()
    {
        return OutfitRules.FindAll().ToList();
    }

    public void SaveOutfitRule(OutfitRule rule)
    {
        OutfitRules.Upsert(rule);
    }

    public void DeleteOutfitRule(int id)
    {
        OutfitRules.Delete(id);
    }

    public List<Chore> GetChores()
    {
        return Chores.FindAll().ToList();
    }

    public void SaveChore(Chore chore)
    {
        Chores.Upsert(chore);
    }

    public void DeleteChore(int id)
    {
        Chores.Delete(id);
    }

    public List<PlugAndPlayItemConfig> GetPlugAndPlayItems()
    {
        return PlugAndPlayItems.FindAll().ToList();
    }

    public void SavePlugAndPlayItem(PlugAndPlayItemConfig item)
    {
        PlugAndPlayItems.Upsert(item);
    }

    public void DeletePlugAndPlayItem(int id)
    {
        PlugAndPlayItems.Delete(id);
    }

    public PlayerStats GetStats()
    {
        return Stats.FindById(1) ?? new PlayerStats();
    }

    public void SaveStats(PlayerStats stats)
    {
        Stats.Upsert(stats);
    }

    public GameSettings GetGameSettings()
    {
        return Settings.FindById(1) ?? new GameSettings();
    }

    public void SaveGameSettings(GameSettings settings)
    {
        Settings.Upsert(settings);
    }

    public List<ForcedOutfitPreset> GetForcedOutfitPresets()
    {
        return ForcedOutfitPresets.FindAll().ToList();
    }

    public void SaveForcedOutfitPreset(ForcedOutfitPreset preset)
    {
        ForcedOutfitPresets.Upsert(preset);
    }

    public void DeleteForcedOutfitPreset(int id)
    {
        ForcedOutfitPresets.Delete(id);
    }
}
