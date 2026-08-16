namespace ChastiMaid.Services;

/// <summary>The two phases of a single <see cref="ISpeechRecognitionService.ListenForPhraseAsync" /> attempt.</summary>
public enum SpeechListenPhase
{
    /// <summary>The microphone is open and recording — bounded by <see cref="ISpeechRecognitionService.AttemptRecordDuration" />.</summary>
    Listening,

    /// <summary>Recording has stopped; the captured audio is being transcribed. Duration is not predictable
    /// (depends on CPU speed and utterance length), so callers should show an indeterminate indicator, not
    /// a countdown, during this phase.</summary>
    Processing
}

/// <summary>
///     Verifies that the user reads a specific phrase aloud. See <see cref="SherpaOnnxSttService" /> for the
///     implementation.
/// </summary>
public interface ISpeechRecognitionService
{
    /// <summary>Whether the STT model loaded successfully (lazily, on first use).</summary>
    bool IsModelReady { get; }

    /// <summary>
    ///     How long a single <see cref="ListenForPhraseAsync" /> attempt records for before moving to the
    ///     (unbounded) transcription phase. Exposed so callers can size a per-attempt countdown correctly
    ///     instead of duplicating the constant.
    /// </summary>
    TimeSpan AttemptRecordDuration { get; }

    /// <summary>Whether an input audio device is available.</summary>
    bool DetectMicrophone();

    /// <summary>
    ///     Listens for up to <paramref name="maxAttempts" /> tries within <paramref name="timeout" /> total,
    ///     accepting only transcriptions whose similarity to <paramref name="targetPhrase" /> (see
    ///     <see cref="SpeechTextMatcher" />) meets <paramref name="minSimilarity" />. Returns true on the first
    ///     accepted match, false if attempts or the timeout are exhausted first.
    ///     <paramref name="timeout" /> only gates whether a *new* attempt is allowed to start — once an attempt's
    ///     recording window ends, its transcription always runs to completion, since decoding can't be aborted
    ///     mid-flight. The overall call can therefore run somewhat longer than <paramref name="timeout" />.
    /// </summary>
    Task<bool> ListenForPhraseAsync(
        string targetPhrase,
        int maxAttempts,
        TimeSpan timeout,
        double minSimilarity,
        IProgress<int>? attemptsRemaining = null,
        IProgress<SpeechListenPhase>? phase = null,
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
