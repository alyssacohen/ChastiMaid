namespace ChastiMaid.Services;

public static class AppPaths
{
    public static string Root =>
        Environment.GetEnvironmentVariable("CHASTIMAID_ROOT") ?? AppContext.BaseDirectory;
}
