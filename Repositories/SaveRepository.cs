using ChastiMaid.Models;
using ChastiMaid.Services;
using LiteDB;
using static ChastiMaid.Services.GameClock;

namespace ChastiMaid.Repositories;

public class SaveRepository(LiteDatabase db)
{
    private ILiteCollection<ClothingItem> Wardrobe => db.GetCollection<ClothingItem>("wardrobe");
    private ILiteCollection<OutfitRule> OutfitRules => db.GetCollection<OutfitRule>("outfit_rules");
    private ILiteCollection<Chore> Chores => db.GetCollection<Chore>("chores");

    private ILiteCollection<PunishmentTemplate> PunishmentTemplates =>
        db.GetCollection<PunishmentTemplate>("punishment_templates");

    private ILiteCollection<ActivePunishment> ActivePunishments =>
        db.GetCollection<ActivePunishment>("active_punishments");

    private ILiteCollection<Session> Sessions => db.GetCollection<Session>("sessions");
    private ILiteCollection<GameState> GameStates => db.GetCollection<GameState>("game_state");
    private ILiteCollection<GameSettings> Settings => db.GetCollection<GameSettings>("game_settings");

    // --- GameState ---
    public GameState? GetGameState()
    {
        return GameStates.FindById(1);
    }

    public void SaveGameState(GameState state)
    {
        GameStates.Upsert(state);
    }

    // --- GameSettings ---
    public GameSettings GetGameSettings()
    {
        return Settings.FindById(1) ?? new GameSettings();
    }

    // --- Snapshot collections (read-only after new game) ---
    public List<ClothingItem> GetWardrobe()
    {
        return Wardrobe.FindAll().ToList();
    }

    public List<OutfitRule> GetOutfitRules()
    {
        return OutfitRules.FindAll().ToList();
    }

    public List<Chore> GetChores()
    {
        return Chores.FindAll().ToList();
    }

    public List<PunishmentTemplate> GetPunishmentTemplates()
    {
        return PunishmentTemplates.FindAll().ToList();
    }

    // --- Wardrobe weight adjustments ---
    public void AcceptItems(IEnumerable<ClothingItem> items)
    {
        foreach (var item in items)
        {
            item.Weight = Math.Max(0.1, item.Weight - 0.1);
            if (item.RejectionCount > 0)
            {
                item.RejectionCount--;
            }

            Wardrobe.Update(item);
        }
    }

    public void RejectItems(IEnumerable<ClothingItem> items)
    {
        foreach (var item in items)
        {
            item.Weight += 0.3;
            item.RejectionCount++;
            Wardrobe.Update(item);
        }
    }

    // --- In-game chores (can add during session, don't affect profile) ---
    public void AddChore(Chore chore)
    {
        Chores.Insert(chore);
    }

    // --- Active punishments ---
    public List<ActivePunishment> GetActivePunishments()
    {
        return ActivePunishments.Find(p => !p.IsResolved).ToList();
    }

    public void SaveActivePunishment(ActivePunishment punishment)
    {
        ActivePunishments.Upsert(punishment);
    }

    // --- Sessions ---
    public void AddSession(Session session)
    {
        Sessions.Insert(session);
    }

    public void UpdateSession(Session session)
    {
        Sessions.Upsert(session);
    }

    public List<Session> GetSessions()
    {
        return Sessions.FindAll().ToList();
    }

    /// Creates a new save slot by snapshotting the current profile data.
    /// Profile changes after this point will not affect the save.
    public static SaveRepository CreateFromProfile(
        ProfileRepository profile,
        DatabaseService dbService,
        string slotName,
        decimal initialDebt)
    {
        var saveDb = dbService.OpenSave(slotName);
        var save = new SaveRepository(saveDb);

        // Snapshot wardrobe
        var wardrobeCol = saveDb.GetCollection<ClothingItem>("wardrobe");
        wardrobeCol.InsertBulk(profile.GetWardrobe());

        // Snapshot outfit rules
        var rulesCol = saveDb.GetCollection<OutfitRule>("outfit_rules");
        rulesCol.InsertBulk(profile.GetOutfitRules());

        // Snapshot chores
        var choresCol = saveDb.GetCollection<Chore>("chores");
        choresCol.InsertBulk(profile.GetChores());

        // Snapshot punishment templates
        var templatesCol = saveDb.GetCollection<PunishmentTemplate>("punishment_templates");
        templatesCol.InsertBulk(profile.GetPunishmentTemplates());

        // Snapshot game settings
        var settings = profile.GetGameSettings();
        var settingsCol = saveDb.GetCollection<GameSettings>("game_settings");
        settingsCol.Upsert(settings);

        // Create initial game state
        save.SaveGameState(new GameState
        {
            SlotName = slotName,
            InitialDebt = initialDebt,
            CurrentDebt = initialDebt,
            DailyInterestRate = settings.DailyInterestRate,
            StartedAt = UtcNow(),
            LastSessionAt = UtcNow(),
        });

        return save;
    }
}
