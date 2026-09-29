using ChastiMaid.Services;

namespace ChastiMaid.Models;

/// <summary>Snapshot of which optional hardware/software capabilities are available on this machine.</summary>
public record HardwareAvailability(bool HasWebcam, bool HasSpeechRecognizer, bool HasMicrophone)
{
    public bool HasAnyIssue => !HasWebcam || !HasSpeechRecognizer || !HasMicrophone;

    /// <summary>Aggregates the optional-hardware probes already exposed by the individual services.</summary>
    public static HardwareAvailability Check(
        IReadOnlyList<int> availableCameras, int cameraIndex, SherpaOnnxSttService stt) =>
        new(availableCameras.Contains(cameraIndex), stt.IsModelReady, stt.DetectMicrophone());
}
