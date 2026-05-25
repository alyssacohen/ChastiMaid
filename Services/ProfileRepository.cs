using ChastiMaid.Models;
using LiteDB;

namespace ChastiMaid.Services;

public class ProfileRepository(LiteDatabase db)
{
    private ILiteCollection<ClothingItem> Wardrobe => db.GetCollection<ClothingItem>("wardrobe");
    private ILiteCollection<OutfitRule> OutfitRules => db.GetCollection<OutfitRule>("outfit_rules");
    private ILiteCollection<Chore> Chores => db.GetCollection<Chore>("chores");
    private ILiteCollection<PunishmentTemplate> PunishmentTemplates => db.GetCollection<PunishmentTemplate>("punishment_templates");
    private ILiteCollection<PlayerStats> Stats => db.GetCollection<PlayerStats>("player_stats");
    private ILiteCollection<GameSettings> Settings => db.GetCollection<GameSettings>("game_settings");

    public List<ClothingItem> GetWardrobe() => Wardrobe.FindAll().ToList();
    public void SaveClothingItem(ClothingItem item) => Wardrobe.Upsert(item);
    public void DeleteClothingItem(int id) => Wardrobe.Delete(id);

    public List<OutfitRule> GetOutfitRules() => OutfitRules.FindAll().ToList();
    public void SaveOutfitRule(OutfitRule rule) => OutfitRules.Upsert(rule);
    public void DeleteOutfitRule(int id) => OutfitRules.Delete(id);

    public List<Chore> GetChores() => Chores.FindAll().ToList();
    public void SaveChore(Chore chore) => Chores.Upsert(chore);
    public void DeleteChore(int id) => Chores.Delete(id);

    public List<PunishmentTemplate> GetPunishmentTemplates() => PunishmentTemplates.FindAll().ToList();
    public void SavePunishmentTemplate(PunishmentTemplate template) => PunishmentTemplates.Upsert(template);
    public void DeletePunishmentTemplate(int id) => PunishmentTemplates.Delete(id);

    public PlayerStats GetStats() => Stats.FindById(1) ?? new PlayerStats();
    public void SaveStats(PlayerStats stats) => Stats.Upsert(stats);

    public GameSettings GetGameSettings() => Settings.FindById(1) ?? new GameSettings();
    public void SaveGameSettings(GameSettings settings) => Settings.Upsert(settings);
}
