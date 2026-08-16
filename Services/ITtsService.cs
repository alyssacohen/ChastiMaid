namespace ChastiMaid.Services;

/// <summary>Speaks text aloud. See <see cref="SherpaOnnxTtsService" /> for the implementation.</summary>
public interface ITtsService
{
    Task SpeakAsync(string text, CancellationToken ct = default);
}
