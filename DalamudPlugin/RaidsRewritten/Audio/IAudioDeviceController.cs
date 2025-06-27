using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RaidsRewritten.Audio;

public interface IAudioDeviceController
{
    public bool IsAudioPlaybackSourceActive { get; }

    public bool AudioPlaybackIsRequested { get; set; }

    public int AudioPlaybackDeviceIndex { get; set; }

    public event EventHandler<WaveInEventArgs>? OnAudioRecordingSourceDataAvailable;

    IEnumerable<string> GetAudioPlaybackDevices();

    void CreateAudioPlaybackChannel(string channelName);
    void RemoveAudioPlaybackChannel(string channelName);

    void AddPlaybackSample(string channelName, WaveInEventArgs sample);

    void ResetAllChannelsVolume(float volume);
    void SetChannelVolume(string channelName, float leftVolume, float rightVolume);

    bool ChannelHasActivity(string channelName);

    Task PlaySfx(CachedSound sound);
}
