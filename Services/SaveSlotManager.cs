using ChastiMaid.Models;
using ChastiMaid.Repositories;

namespace ChastiMaid.Services;

public record SaveSlotInfo(
    string SlotName,
    DateTime StartedAt,
    DateTime LastSessionAt,
    decimal CurrentDebt,
    bool IsCompleted,
    bool IsSafeLocked);

public class SaveSlotManager(DatabaseService dbService)
{
    /// <summary>A name for a new save slot, unique among the saves on disk.</summary>
    public string NewSlotName() => NewSlotName(DateTime.Now, dbService.ListSaveSlots());

    /// <summary>
    ///     <c>save_YYYYMMDD_HHMM</c> from <paramref name="now" />, with a <c>_2</c>, <c>_3</c>… suffix when that
    ///     name is already taken (two games started in the same minute would otherwise share one file).
    /// </summary>
    public static string NewSlotName(DateTime now, IEnumerable<string> existingSlots)
    {
        // Case-insensitive: slot names are file names, and Windows file names ignore case.
        var taken = existingSlots.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var baseName = $"save_{now:yyyyMMdd_HHmm}";
        var name = baseName;
        for (var suffix = 2; taken.Contains(name); suffix++)
        {
            name = $"{baseName}_{suffix}";
        }

        return name;
    }

    public List<SaveSlotInfo> ListSlots()
    {
        var slots = new List<SaveSlotInfo>();
        foreach (var slotName in dbService.ListSaveSlots())
        {
            try
            {
                using var saveDb = dbService.OpenSave(slotName);
                var state = saveDb.GetCollection<GameState>("game_state").FindById(1);
                if (state is null)
                {
                    continue;
                }

                slots.Add(new SaveSlotInfo(slotName, state.StartedAt, state.LastSessionAt, state.CurrentDebt,
                    state.IsCompleted, state.IsSafeLocked));
            }
            catch
            {
                // Corrupt or incompatible save file — skip
            }
        }

        return slots;
    }

    /// <summary>
    ///     The contract a save was started under, rebuilt from its new-game snapshot. Occasional tasks are
    ///     added mid-session, not agreed at signing, so they are left out.
    /// </summary>
    public GameContract ReadContract(string slotName)
    {
        using var saveDb = dbService.OpenSave(slotName);
        var save = new SaveRepository(saveDb);
        return new()
        {
            Settings = save.GetGameSettings(),
            Wardrobe = save.GetWardrobe(),
            OutfitRules = save.GetOutfitRules(),
            Tasks = [..save.GetChores().Where(c => !c.IsOccasional)],
            ForcedOutfitPresets = save.GetForcedOutfitPresets(),
            PlugAndPlayItems = save.GetPlugAndPlayItems(),
            DiaperPlayItems = save.GetDiaperPlayItems(),
            ChastityDeviceItems = save.GetChastityDeviceItems(),
            PermissionTypes = save.GetPermissionTypes(),
        };
    }

    public void DeleteSlot(string slotName)
    {
        dbService.DeleteSave(slotName);

        // Also delete the media folder (outfit video, safe combo, etc.)
        var mediaDir = AppPaths.SaveDir(slotName);
        if (Directory.Exists(mediaDir))
        {
            Directory.Delete(mediaDir, true);
        }
    }
}
