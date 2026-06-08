using ChastiMaid.Models;

namespace ChastiMaid.Repositories;

public interface IProfileRepository
{
    List<ClothingItem> GetWardrobe();
    void SaveClothingItem(ClothingItem item);
    void DeleteClothingItem(int id);

    List<OutfitRule> GetOutfitRules();
    void SaveOutfitRule(OutfitRule rule);
    void DeleteOutfitRule(int id);

    List<Chore> GetChores();
    void SaveChore(Chore chore);
    void DeleteChore(int id);

    List<PunishmentTemplate> GetPunishmentTemplates();
    void SavePunishmentTemplate(PunishmentTemplate template);
    void DeletePunishmentTemplate(int id);

    PlayerStats GetStats();
    void SaveStats(PlayerStats stats);

    GameSettings GetGameSettings();
    void SaveGameSettings(GameSettings settings);
}
