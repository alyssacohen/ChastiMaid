namespace ChastiMaid.Services;

/// <summary>Speaks text aloud. See <see cref="SherpaOnnxTtsService" /> for the implementation.</summary>
public interface ITtsService
{
    Task SpeakAsync(string text, CancellationToken ct = default);

    /// <summary>
    ///     Loads the current UI culture's voice model in the background, ahead of the first
    ///     <see cref="SpeakAsync" /> call. Model loading alone takes several seconds — callers with a
    ///     lead-in window before narration matters (e.g. Cornertime's camera-framing step) should call
    ///     this early so that first call doesn't pay the load cost on top of generation.
    /// </summary>
    Task WarmUpAsync(CancellationToken ct = default);
}
