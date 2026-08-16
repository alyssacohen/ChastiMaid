using ChastiMaid.Models;

namespace ChastiMaid.Services;

/// <summary>Aggregates the optional-hardware probes already exposed by the individual services.</summary>
public static class HardwareAvailabilityChecker
{
    public static HardwareAvailability Check(
        IReadOnlyList<int> availableCameras, int cameraIndex, ISpeechRecognitionService stt)
    {
        return new HardwareAvailability(
            availableCameras.Contains(cameraIndex),
            stt.IsModelReady,
            stt.DetectMicrophone());
    }
}
