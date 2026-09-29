namespace ChastiMaid.Services;

public static class AppPaths
{
    public static string Root =>
        Environment.GetEnvironmentVariable("CHASTIMAID_ROOT") ?? AppContext.BaseDirectory;

    /// <summary>A save slot's media folder (photos, videos, the encrypted safe combination).</summary>
    public static string SaveDir(string slotName) => Path.Combine(Root, "data", "saves", slotName);
}
