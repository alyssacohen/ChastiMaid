namespace ChastiMaid.Services;

/// <summary>
///     Verifies that the user reads a specific phrase aloud. See <see cref="SherpaOnnxSttService" /> for the
///     implementation.
/// </summary>
public interface ISpeechRecognitionService
{
    /// <summary>Whether the STT model loaded successfully (lazily, on first use).</summary>
    bool IsModelReady { get; }

    /// <summary>Whether an input audio device is available.</summary>
    bool DetectMicrophone();

    /// <summary>
    ///     Listens for up to <paramref name="maxAttempts" /> tries within <paramref name="timeout" /> total,
    ///     accepting only transcriptions whose similarity to <paramref name="targetPhrase" /> (see
    ///     <see cref="SpeechTextMatcher" />) meets <paramref name="minSimilarity" />. Returns true on the first
    ///     accepted match, false if attempts or the timeout are exhausted first.
    /// </summary>
    Task<bool> ListenForPhraseAsync(
        string targetPhrase,
        int maxAttempts,
        TimeSpan timeout,
        double minSimilarity,
        IProgress<int>? attemptsRemaining = null,
        CancellationToken ct = default);

    /// <summary>
    ///     Starts an open-ended test recording (Settings' "try your sensitivity" panel) — no fixed duration,
    ///     the caller decides when to stop via <see cref="StopTestListeningAsync" /> or
    ///     <see cref="CancelTestListening" />.
    /// </summary>
    void StartTestListening();

    /// <summary>Stops a test recording started by <see cref="StartTestListening" /> without transcribing it.</summary>
    void CancelTestListening();

    /// <summary>
    ///     Stops a test recording started by <see cref="StartTestListening" />, transcribes it, and returns the
    ///     raw similarity score against <paramref name="targetPhrase" /> — not a pass/fail, so Settings can show
    ///     the user the actual number while they tune the sensitivity. Returns 0 if the model isn't ready, no
    ///     recording was active, or nothing was captured.
    /// </summary>
    Task<double> StopTestListeningAsync(string targetPhrase, CancellationToken ct = default);
}
