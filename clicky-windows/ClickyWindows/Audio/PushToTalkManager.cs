// Audio/PushToTalkManager.cs
// Opens the default microphone when push-to-talk starts, streams buffered
// audio to the transcription session, and stops cleanly on release.
// Uses NAudio WaveInEvent (callback-based, no polling).
// Port of BuddyDictationManager's AVAudioEngine section.

using System;
using NAudio.Wave;

namespace ClickyWindows;

internal class PushToTalkManager : IDisposable
{
    private WaveInEvent? _waveIn;
    private readonly PCM16AudioConverter _converter = new();
    private readonly AudioPowerLevelTracker _powerTracker = new();
    private Action<byte[]>? _onAudioChunkReady;
    private bool _isCapturing = false;

    // NAudio WaveIn captures at device native rate; converter resamples to 16kHz.
    private static readonly WaveFormat CaptureFormat
        = new WaveFormat(44100, 16, 1); // mono 44.1kHz

    public AudioPowerLevelTracker PowerTracker => _powerTracker;

    /// <summary>
    /// Opens the microphone and begins streaming PCM16 audio chunks to the callback.
    /// </summary>
    public void StartCapture(Action<byte[]> onAudioChunkReady)
    {
        if (_isCapturing) return;
        _isCapturing = true;
        _onAudioChunkReady = onAudioChunkReady;
        _powerTracker.Reset();

        try
        {
            _waveIn = new WaveInEvent
            {
                WaveFormat = CaptureFormat,
                // 100ms buffer intervals — matches macOS 1024 frame tap at 44.1kHz
                BufferMilliseconds = 100
            };

            _waveIn.DataAvailable += OnMicrophoneDataAvailable;
            _waveIn.StartRecording();
            System.Diagnostics.Debug.WriteLine("PushToTalkManager: Microphone capture started.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PushToTalkManager: Failed to start microphone: {ex.Message}");
            _isCapturing = false;
        }
    }

    /// <summary>
    /// Stops microphone capture. Any pending audio chunks are dropped.
    /// </summary>
    public void StopCapture()
    {
        if (!_isCapturing) return;
        _isCapturing = false;

        try
        {
            _waveIn?.StopRecording();
            _waveIn?.Dispose();
            _waveIn = null;
            _onAudioChunkReady = null;
            System.Diagnostics.Debug.WriteLine("PushToTalkManager: Microphone capture stopped.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PushToTalkManager: Error stopping microphone: {ex.Message}");
        }
    }

    private void OnMicrophoneDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!_isCapturing || e.BytesRecorded == 0) return;

        // Convert to 16kHz PCM16 mono for AssemblyAI
        var pcm16Bytes = _converter.ConvertToPCM16(
            e.Buffer, e.BytesRecorded, CaptureFormat);

        if (pcm16Bytes != null && pcm16Bytes.Length > 0)
        {
            // Update waveform visualization
            _powerTracker.ProcessPCM16Buffer(pcm16Bytes, pcm16Bytes.Length);

            // Forward to the active transcription session
            _onAudioChunkReady?.Invoke(pcm16Bytes);
        }
    }

    public void Dispose()
    {
        StopCapture();
        _converter.Dispose();
    }
}
