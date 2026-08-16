using System.Globalization;
using NAudio.Wave;
using SherpaOnnx;
using Windows.Devices.Enumeration;

namespace ChastiMaid.Services;

/// <summary>
///     Verifies that the user reads a specific phrase aloud, using sherpa-onnx's offline (non-streaming)
///     multilingual Whisper model shared across it/en — the target language doesn't need auto-detection, it's
///     always known from <see cref="System.Globalization.CultureInfo.CurrentUICulture" />, so decoding just
///     transcribes and <see cref="SpeechTextMatcher" /> does the comparison.
///     <para>
///         The recognizer and its native handles are loaded once, lazily, on first use, and kept for the
///         lifetime of the service (see <see cref="Dispose" />). Model load failures (missing/corrupt files)
///         leave <see cref="IsModelReady" /> false instead of throwing — same best-effort behavior as the WinRT
///         engine this replaces, since the UI already falls back to typing the quote.
///     </para>
///     <para>
///         Loading the model parses ~200 MB of ONNX weights and can take several seconds — call sites that run
///         on the UI thread (<see cref="ListenForPhraseAsync" />, <see cref="StopTestListeningAsync" />) always
///         push that first-load cost onto a thread-pool thread instead of blocking synchronously on
///         <see cref="IsModelReady" />. <c>App.axaml.cs</c> also warms the model up in the background right
///         after construction, so by the time either of those runs, loading has usually already finished.
///     </para>
/// </summary>
public sealed class SherpaOnnxSttService : ISpeechRecognitionService, IDisposable
{
    private const string WhisperVariant = "base";
    private const int SampleRate = 16000;

    // ponytail: no voice-activity detection — each in-game attempt just records a fixed window. Add silence
    // detection (stop early once the user goes quiet) if attempts start feeling sluggish in practice.
    private static readonly TimeSpan AttemptRecordDuration = TimeSpan.FromSeconds(4);

    // ponytail: safety cap on the Settings "try it" recording, not a real product limit — just stops a
    // microphone from staying open forever if Stop/navigate-away somehow never calls CancelTestListening.
    private static readonly TimeSpan TestRecordSafetyCap = TimeSpan.FromSeconds(90);

    private static readonly string ModelDir =
        Path.Combine(AppContext.BaseDirectory, "Content", "Speech", "STT");

    private readonly Lock _loadLock = new();
    private readonly Lock _decodeLock = new();
    private OfflineRecognizer? _recognizer;
    private bool _loadAttempted;
    private bool _disposed;

    private WaveIn? _testWaveIn;
    private List<float>? _testSamples;
    private TaskCompletionSource<float[]>? _testCaptureTcs;
    private CancellationTokenSource? _testSafetyCts;

    public bool IsModelReady
    {
        get
        {
            EnsureLoaded();
            return _recognizer is not null;
        }
    }

    public bool DetectMicrophone()
    {
        if (!IsModelReady)
        {
            return false;
        }

        try
        {
            var devices = WinRtAsyncExtensions.ToTask(DeviceInformation.FindAllAsync(DeviceClass.AudioCapture))
                .GetAwaiter().GetResult();
            return devices.Count > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> ListenForPhraseAsync(
        string targetPhrase,
        int maxAttempts,
        TimeSpan timeout,
        double minSimilarity,
        IProgress<int>? attemptsRemaining = null,
        CancellationToken ct = default)
    {
        await Task.Run(EnsureLoaded, ct);
        if (_recognizer is null)
        {
            return false;
        }

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        var attemptsLeft = maxAttempts;
        attemptsRemaining?.Report(attemptsLeft);

        while (attemptsLeft > 0 && !linkedCts.IsCancellationRequested)
        {
            var similarity = await RecordAndMeasureAsync(targetPhrase, linkedCts.Token);
            if (similarity >= minSimilarity)
            {
                return true;
            }

            attemptsLeft--;
            attemptsRemaining?.Report(attemptsLeft);
        }

        return false;
    }

    public void StartTestListening()
    {
        CancelTestListening();

        var samples = new List<float>();
        var tcs = new TaskCompletionSource<float[]>();
        var waveIn = new WaveIn { WaveFormat = new WaveFormat(SampleRate, 16, 1) };

        waveIn.DataAvailable += (_, e) => AppendSamples(samples, e);
        waveIn.RecordingStopped += (_, _) =>
        {
            waveIn.Dispose();
            tcs.TrySetResult(samples.ToArray());
        };

        _testSamples = samples;
        _testCaptureTcs = tcs;
        _testWaveIn = waveIn;
        _testSafetyCts = new CancellationTokenSource(TestRecordSafetyCap);
        _testSafetyCts.Token.Register(CancelTestListening);

        try
        {
            waveIn.StartRecording();
        }
        catch
        {
            CancelTestListening();
        }
    }

    public void CancelTestListening()
    {
        _testSafetyCts?.Dispose();
        _testSafetyCts = null;

        try
        {
            _testWaveIn?.StopRecording();
        }
        catch
        {
            /* best-effort stop */
        }

        _testWaveIn = null;
        _testSamples = null;
        _testCaptureTcs = null;
    }

    public async Task<double> StopTestListeningAsync(string targetPhrase, CancellationToken ct = default)
    {
        var tcs = _testCaptureTcs;
        var waveIn = _testWaveIn;
        _testSafetyCts?.Dispose();
        _testSafetyCts = null;
        _testCaptureTcs = null;
        _testWaveIn = null;
        _testSamples = null;

        if (tcs is null || waveIn is null)
        {
            return 0;
        }

        try
        {
            waveIn.StopRecording();
        }
        catch
        {
            return 0;
        }

        var samples = await tcs.Task;
        if (samples.Length == 0)
        {
            return 0;
        }

        await Task.Run(EnsureLoaded, ct);
        if (_recognizer is null)
        {
            return 0;
        }

        return await DecodeAsync(samples, targetPhrase, ct);
    }

    private async Task<double> RecordAndMeasureAsync(string targetPhrase, CancellationToken ct)
    {
        float[] samples;
        try
        {
            samples = await CaptureAsync(ct);
        }
        catch
        {
            return 0;
        }

        if (samples.Length == 0)
        {
            return 0;
        }

        return await DecodeAsync(samples, targetPhrase, ct);
    }

    private Task<double> DecodeAsync(float[] samples, string targetPhrase, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            lock (_decodeLock)
            {
                using var stream = _recognizer!.CreateStream();
                stream.AcceptWaveform(SampleRate, samples);
                _recognizer.Decode(stream);
                return SpeechTextMatcher.Similarity(targetPhrase, stream.Result.Text);
            }
        }, ct);
    }

    private static void AppendSamples(List<float> samples, WaveInEventArgs e)
    {
        for (var i = 0; i + 1 < e.BytesRecorded; i += 2)
        {
            samples.Add(BitConverter.ToInt16(e.Buffer, i) / 32768f);
        }
    }

    private static Task<float[]> CaptureAsync(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<float[]>();
        var samples = new List<float>();
        var waveIn = new WaveIn { WaveFormat = new WaveFormat(SampleRate, 16, 1) };

        waveIn.DataAvailable += (_, e) => AppendSamples(samples, e);
        waveIn.RecordingStopped += (_, _) =>
        {
            waveIn.Dispose();
            tcs.TrySetResult(samples.ToArray());
        };

        using var durationCts = new CancellationTokenSource(AttemptRecordDuration);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, durationCts.Token);
        using var registration = linkedCts.Token.Register(() => waveIn.StopRecording());

        try
        {
            waveIn.StartRecording();
        }
        catch
        {
            waveIn.Dispose();
            return Task.FromResult(Array.Empty<float>());
        }

        return tcs.Task;
    }

    private void EnsureLoaded()
    {
        // No fast path outside the lock: the model load itself is slow (~seconds), and a flag set
        // true before that finishes would let a concurrent caller read a still-null _recognizer as
        // final. Every caller blocks on the lock until the one in-flight load actually completes.
        lock (_loadLock)
        {
            if (_loadAttempted)
            {
                return;
            }

            _loadAttempted = true;

            try
            {
                var encoder = Path.Combine(ModelDir, $"{WhisperVariant}-encoder.onnx");
                var decoder = Path.Combine(ModelDir, $"{WhisperVariant}-decoder.onnx");
                var tokens = Path.Combine(ModelDir, $"{WhisperVariant}-tokens.txt");

                if (!File.Exists(encoder) || !File.Exists(decoder) || !File.Exists(tokens))
                {
                    return;
                }

                var config = new OfflineRecognizerConfig();
                config.FeatConfig.SampleRate = SampleRate;
                config.FeatConfig.FeatureDim = 80;
                config.ModelConfig.Tokens = tokens;
                config.ModelConfig.Whisper.Encoder = encoder;
                config.ModelConfig.Whisper.Decoder = decoder;
                config.ModelConfig.Whisper.Task = "transcribe";
                config.ModelConfig.Whisper.Language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                config.ModelConfig.NumThreads = 1;
                config.ModelConfig.Provider = "cpu";
                config.DecodingMethod = "greedy_search";

                _recognizer = new OfflineRecognizer(config);
            }
            catch
            {
                _recognizer = null;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelTestListening();
        _recognizer?.Dispose();
    }
}
