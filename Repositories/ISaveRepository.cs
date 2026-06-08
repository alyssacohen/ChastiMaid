using ChastiMaid.Models;

namespace ChastiMaid.Repositories;

public interface ISaveRepository
{
    GameState? GetGameState();
    void SaveGameState(GameState state);

    GameSettings GetGameSettings();

    List<ClothingItem> GetWardrobe();
    List<OutfitRule> GetOutfitRules();
    List<Chore> GetChores();
    List<PunishmentTemplate> GetPunishmentTemplates();

    void AcceptItems(IEnumerable<ClothingItem> items);
    void RejectItems(IEnumerable<ClothingItem> items);

    void AddChore(Chore chore);

    List<ActivePunishment> GetActivePunishments();
    void SaveActivePunishment(ActivePunishment punishment);

    void AddSession(Session session);
    void UpdateSession(Session session);
    List<Session> GetSessions();
}
