using ChastiMaid.Models;
using ChastiMaid.Services;
using LiteDB;

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
    private ILiteCollection<PoseCheckRecord> PoseChecks => db.GetCollection<PoseCheckRecord>("pose_checks");
    private ILiteCollection<CurtsyCheckRecord> CurtsyChecks => db.GetCollection<CurtsyCheckRecord>("curtsy_checks");
    private ILiteCollection<KneelCheckRecord> KneelChecks => db.GetCollection<KneelCheckRecord>("kneel_checks");

    private ILiteCollection<PlugAndPlayItemConfig> PlugAndPlayItems =>
        db.GetCollection<PlugAndPlayItemConfig>("plug_and_play_items");

    private ILiteCollection<ActivePlugAndPlayAccessory> ActivePlugAndPlayAccessoryCollection =>
        db.GetCollection<ActivePlugAndPlayAccessory>("active_plug_and_play_accessory");

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
            item.Weight = Math.Max(
                WardrobeBalance.AcceptWeightFloor,
                item.Weight - WardrobeBalance.AcceptWeightDecrement);
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
            item.Weight += WardrobeBalance.RejectWeightIncrement;
            item.RejectionCount++;
            Wardrobe.Update(item);
        }
    }

    // --- In-game chores (can add during session, don't affect profile) ---
    public void AddChore(Chore chore)
    {
        Chores.Insert(chore);
    }

    public void UpdateChoreCompletion(int choreId, DateTime completedAt)
    {
        var chore = Chores.FindById(choreId);
        if (chore is null)
        {
            return;
        }

        chore.LastCompletedAt = completedAt;
        Chores.Update(chore);
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

    // --- Pose checks ---
    public void SavePoseCheckRecord(PoseCheckRecord record)
    {
        PoseChecks.Upsert(record);
    }

    public List<PoseCheckRecord> GetPendingPoseChecks()
    {
        return PoseChecks.Find(r => !r.Reviewed).ToList();
    }

    // --- Curtsy checks ---
    public void SaveCurtsyCheckRecord(CurtsyCheckRecord record)
    {
        CurtsyChecks.Upsert(record);
    }

    public List<CurtsyCheckRecord> GetPendingCurtsyChecks()
    {
        return CurtsyChecks.Find(r => !r.Reviewed).ToList();
    }

    // --- Kneel checks ---
    public void SaveKneelCheckRecord(KneelCheckRecord record)
    {
        KneelChecks.Upsert(record);
    }

    public List<KneelCheckRecord> GetPendingKneelChecks()
    {
        return KneelChecks.Find(r => !r.Reviewed).ToList();
    }

    // --- Plug and Play ---
    public List<PlugAndPlayItemConfig> GetPlugAndPlayItems()
    {
        return PlugAndPlayItems.FindAll().ToList();
    }

    public ActivePlugAndPlayAccessory? GetActivePlugAndPlayAccessory()
    {
        return ActivePlugAndPlayAccessoryCollection.FindById(1);
    }

    public void SaveActivePlugAndPlayAccessory(ActivePlugAndPlayAccessory accessory)
    {
        ActivePlugAndPlayAccessoryCollection.Upsert(accessory);
    }

    public void ClearActivePlugAndPlayAccessory()
    {
        ActivePlugAndPlayAccessoryCollection.Delete(1);
    }

    /// <summary>Assigns a random configured punishment template, applying its immediate debt increase.</summary>
    public void AssignRandomPunishment()
    {
        var templates = GetPunishmentTemplates();
        if (templates.Count == 0)
        {
            return;
        }

        var template = templates[Random.Shared.Next(templates.Count)];

        if (template.DebtIncrease is { } increase)
        {
            var state = GetGameState();
            if (state is not null)
            {
                state.CurrentDebt += increase;
                SaveGameState(state);
            }
        }

        SaveActivePunishment(new ActivePunishment
        {
            TemplateId = template.Id,
            TemplateName = template.Name,
            AssignedAt = DateTime.UtcNow,
            DebtApplied = template.DebtIncrease is not null,
            RemainingOutfitForce = template.OutfitForceDuration,
            ForcedClothingItemIds = template.ForcedClothingItemIds,
            ForceFullOutfit = template.ForceFullOutfit,
            RemainingMultiplierPenalty = template.MultiplierPenaltyDuration,
            MultiplierPenaltyFactor = template.MultiplierPenaltyFactor,
            PendingChoreIds = template.RequiredChoreIds
                ?? (template.RequiredChoreCount is { } n
                    ? GetChores().Select(c => c.Id).OrderBy(_ => Random.Shared.Next()).Take(n).ToList()
                    : null),
        });
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

        // Snapshot plug and play item configs
        var plugAndPlayCol = saveDb.GetCollection<PlugAndPlayItemConfig>("plug_and_play_items");
        plugAndPlayCol.InsertBulk(profile.GetPlugAndPlayItems());

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
            StartedAt = DateTime.UtcNow,
            LastSessionAt = DateTime.UtcNow,
        });

        return save;
    }
}
