using NAudio.CoreAudioApi;

namespace MiyaIsland.Services;

public sealed class VolumeService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private MMDevice? _device;

    private MMDevice? GetDevice()
    {
        try
        {
            return _device ??= _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch
        {
            return null;
        }
    }

    public float GetVolume() => GetDevice()?.AudioEndpointVolume.MasterVolumeLevelScalar ?? 0;

    public void SetVolume(float value)
    {
        var device = GetDevice();
        if (device is not null)
            device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0, 1);
    }

    public void Dispose()
    {
        _device?.Dispose();
        _enumerator.Dispose();
    }
}
