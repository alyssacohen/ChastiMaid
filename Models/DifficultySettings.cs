namespace ChastiMaid.Models;

public record DifficultyConfig(decimal MinDebt, decimal MaxDebt, double DailyInterestRate);

public static class DifficultySettings
{
    public static readonly Dictionary<Difficulty, DifficultyConfig> Configs = new()
    {
        [Difficulty.Easy]   = new(200,   500,  0.01),
        [Difficulty.Medium] = new(500,  1000,  0.03),
        [Difficulty.Hard]   = new(1000, 2000,  0.05),
    };

    public static DifficultyConfig Get(Difficulty d) => Configs[d];

    public static decimal RollDebt(Difficulty d, Random? rng = null)
    {
        var cfg = Get(d);
        rng ??= Random.Shared;
        return Math.Round(cfg.MinDebt + (decimal)rng.NextDouble() * (cfg.MaxDebt - cfg.MinDebt), 0);
    }
}
