using Windows.Devices.Enumeration;
using Windows.Media.SpeechRecognition;

namespace ChastiMaid.Services;

/// <summary>
///     Verifies that the user reads a specific phrase aloud, using a grammar constrained to the
///     exact target text (not free dictation + fuzzy matching). Built on
///     <see cref="Windows.Media.SpeechRecognition" />, the on-device WinRT speech recognizer built
///     into Windows itself — unlike the legacy SAPI5 desktop engine (<c>System.Speech</c>), it does
///     not depend on a separately-installed recognizer and is present on Windows 10 (1607+) and
///     Windows 11 by default. Stateless: each call opens and disposes its own
///     <see cref="SpeechRecognizer" /> — there is no continuous listening state to keep alive
///     between popups, unlike the webcam preview.
/// </summary>
public static class SpeechRecognitionService
{
    /// <summary>
    ///     Checks whether an on-device speech recognizer is available for the system's configured
    ///     language. "Installed" here means the Windows Speech Recognition language pack is present
    ///     and constraint compilation succeeds — not that a separate SAPI engine is present.
    /// </summary>
    public static bool IsSpeechRecognizerInstalled()
    {
        try
        {
            using var recognizer = new SpeechRecognizer();
            recognizer.Constraints.Add(new SpeechRecognitionListConstraint(["test"]));
            var result = WinRtAsyncExtensions.ToTask(recognizer.CompileConstraintsAsync()).GetAwaiter().GetResult();
            return result.Status == SpeechRecognitionResultStatus.Success;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    ///     Checks whether a default input audio device is available. Requires a speech recognizer
    ///     to be installed for the configured language (returns false if not, matching
    ///     <see cref="IsSpeechRecognizerInstalled" />), and that at least one audio capture device is
    ///     enumerable — this also covers microphone privacy access being denied to desktop apps,
    ///     since a denied app sees zero devices from <see cref="DeviceInformation" />.
    /// </summary>
    public static bool DetectMicrophone()
    {
        if (!IsSpeechRecognizerInstalled())
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

    /// <summary>
    ///     Listens for up to <paramref name="maxAttempts" /> tries within <paramref name="timeout" />
    ///     total, accepting only recognitions of <paramref name="targetPhrase" /> (case-insensitive,
    ///     trimmed) whose confidence meets <paramref name="minConfidence" />. Returns true on the
    ///     first accepted match, false if attempts or the timeout are exhausted first.
    /// </summary>
    /// <param name="minConfidence">
    ///     A 0..1 sensitivity value (matching the existing Settings slider) mapped to WinRT's
    ///     <see cref="SpeechRecognitionConfidence" /> enum: &gt;= 0.66 requires High, &gt;= 0.33
    ///     requires at least Medium, otherwise Low is also accepted.
    /// </param>
    public static async Task<bool> ListenForPhraseAsync(
        string targetPhrase,
        int maxAttempts,
        TimeSpan timeout,
        double minConfidence,
        IProgress<int>? attemptsRemaining = null,
        CancellationToken ct = default)
    {
        SpeechRecognizer recognizer;
        try
        {
            recognizer = new SpeechRecognizer();
            recognizer.Constraints.Add(new SpeechRecognitionListConstraint([targetPhrase.Trim()]));
            var compileResult = await WinRtAsyncExtensions.ToTask(recognizer.CompileConstraintsAsync());
            if (compileResult.Status != SpeechRecognitionResultStatus.Success)
            {
                recognizer.Dispose();
                return false;
            }
        }
        catch (Exception)
        {
            return false;
        }

        using var _ = recognizer;

        var requiredConfidence = minConfidence switch
        {
            >= 0.66 => SpeechRecognitionConfidence.High,
            >= 0.33 => SpeechRecognitionConfidence.Medium,
            _ => SpeechRecognitionConfidence.Low
        };

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        var attemptsLeft = maxAttempts;
        attemptsRemaining?.Report(attemptsLeft);

        while (attemptsLeft > 0 && !linkedCts.IsCancellationRequested)
        {
            SpeechRecognitionResult? result;
            try
            {
                var recognizeTask = WinRtAsyncExtensions.ToTask(recognizer.RecognizeAsync());
                var cancelWait = Task.Delay(Timeout.Infinite, linkedCts.Token);
                var completed = await Task.WhenAny(recognizeTask, cancelWait);
                if (completed != recognizeTask)
                {
                    await WinRtAsyncExtensions.ToTask(recognizer.StopRecognitionAsync());
                    return false;
                }

                result = await recognizeTask;
            }
            catch (Exception)
            {
                return false;
            }

            if (result.Status == SpeechRecognitionResultStatus.Success && result.Confidence <= requiredConfidence)
            {
                return true;
            }

            attemptsLeft--;
            attemptsRemaining?.Report(attemptsLeft);
        }

        return false;
    }
}
