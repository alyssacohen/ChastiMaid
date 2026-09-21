using ChastiMaid.Models;
using LiteDB;

namespace ChastiMaid.Repositories;

public class ProfileRepository(LiteDatabase db)
{
    private ILiteCollection<ClothingItem> Wardrobe => db.GetCollection<ClothingItem>("wardrobe");
    // "outfit_rules_v2": renamed from "outfit_rules" when rules moved from item-to-item pairs to tag-to-tag pairs.
    // The old collection is left behind (dead, unread) rather than migrated — see docs/specs/wardrobe.md.
    private ILiteCollection<OutfitRule> OutfitRules => db.GetCollection<OutfitRule>("outfit_rules_v2");
    private ILiteCollection<Chore> Chores => db.GetCollection<Chore>("chores");

    private ILiteCollection<PlugAndPlayItemConfig> PlugAndPlayItems =>
        db.GetCollection<PlugAndPlayItemConfig>("plug_and_play_items");

    private ILiteCollection<DiaperPlayItemConfig> DiaperPlayItems =>
        db.GetCollection<DiaperPlayItemConfig>("diaper_play_items");

    private ILiteCollection<ChastityDeviceItemConfig> ChastityDeviceItems =>
        db.GetCollection<ChastityDeviceItemConfig>("chastity_device_items");

    private ILiteCollection<PlayerStats> Stats => db.GetCollection<PlayerStats>("player_stats");
    private ILiteCollection<GameSettings> Settings => db.GetCollection<GameSettings>("game_settings");

    private ILiteCollection<ForcedOutfitPreset> ForcedOutfitPresets =>
        db.GetCollection<ForcedOutfitPreset>("forced_outfit_presets");

    private ILiteCollection<PermissionTypeConfig> PermissionTypes =>
        db.GetCollection<PermissionTypeConfig>("permission_types");

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

    public void ReplaceWardrobe(IEnumerable<ClothingItem> items)
    {
        Wardrobe.DeleteAll();
        Wardrobe.InsertBulk(items);
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

    public void ReplaceOutfitRules(IEnumerable<OutfitRule> rules)
    {
        OutfitRules.DeleteAll();
        OutfitRules.InsertBulk(rules);
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

    public void ReplaceChores(IEnumerable<Chore> chores)
    {
        Chores.DeleteAll();
        Chores.InsertBulk(chores);
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

    public List<DiaperPlayItemConfig> GetDiaperPlayItems()
    {
        return DiaperPlayItems.FindAll().ToList();
    }

    public void SaveDiaperPlayItem(DiaperPlayItemConfig item)
    {
        DiaperPlayItems.Upsert(item);
    }

    public void DeleteDiaperPlayItem(int id)
    {
        DiaperPlayItems.Delete(id);
    }

    public List<ChastityDeviceItemConfig> GetChastityDeviceItems()
    {
        return ChastityDeviceItems.FindAll().ToList();
    }

    public void SaveChastityDeviceItem(ChastityDeviceItemConfig item)
    {
        ChastityDeviceItems.Upsert(item);
    }

    public void DeleteChastityDeviceItem(int id)
    {
        ChastityDeviceItems.Delete(id);
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

    public void ReplacePlugAndPlayItems(IEnumerable<PlugAndPlayItemConfig> items)
    {
        PlugAndPlayItems.DeleteAll();
        PlugAndPlayItems.InsertBulk(items);
    }

    public void ReplaceDiaperPlayItems(IEnumerable<DiaperPlayItemConfig> items)
    {
        DiaperPlayItems.DeleteAll();
        DiaperPlayItems.InsertBulk(items);
    }

    public void ReplaceChastityDeviceItems(IEnumerable<ChastityDeviceItemConfig> items)
    {
        ChastityDeviceItems.DeleteAll();
        ChastityDeviceItems.InsertBulk(items);
    }

    public void ReplaceForcedOutfitPresets(IEnumerable<ForcedOutfitPreset> presets)
    {
        ForcedOutfitPresets.DeleteAll();
        ForcedOutfitPresets.InsertBulk(presets);
    }

    public void ReplacePermissionTypes(IEnumerable<PermissionTypeConfig> configs)
    {
        PermissionTypes.DeleteAll();
        PermissionTypes.InsertBulk(configs);
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

    public List<PermissionTypeConfig> GetPermissionTypes()
    {
        return PermissionTypes.FindAll().ToList();
    }

    public void SavePermissionType(PermissionTypeConfig config)
    {
        PermissionTypes.Upsert(config);
    }
}
