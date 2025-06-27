using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;

namespace RaidsRewritten.Audio;

public class PlaybackChannel : IDisposable
{
    public required MonoToStereoSampleProvider MonoToStereoSampleProvider { get; set; }
    public required BufferedWaveProvider BufferedWaveProvider { get; set; }
    public WaveInEventArgs? LastSampleAdded { get; set; }
    public int LastSampleAddedTimestampMs { get; set; }
    public int BufferClearedEventTimestampMs { get; set; }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
