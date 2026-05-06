// Audio/AudioPowerLevelTracker.cs
// Computes RMS audio power from PCM16 buffers for waveform visualization.
// Port of updateAudioPowerLevel() in BuddyDictationManager.swift.

using System;
using System.Collections.Generic;
using System.Linq;

namespace ClickyWindows;

internal class AudioPowerLevelTracker
{
    // History of 44 samples at 70ms intervals (matching macOS waveform history)
    private const int HistoryLength = 44;
    private const double BaselineLevel = 0.02;
    private const double BoostMultiplier = 10.2;
    private const double SmoothingFactor = 0.72;

    private readonly Queue<double> _powerHistory = new(HistoryLength);
    private double _currentPowerLevel = 0;

    public event Action<double, IReadOnlyList<double>>? OnPowerLevelUpdated;

    public AudioPowerLevelTracker()
    {
        // Pre-fill history with baseline levels
        for (int i = 0; i < HistoryLength; i++)
            _powerHistory.Enqueue(BaselineLevel);
    }

    /// <summary>
    /// Call this with each chunk of 16kHz PCM16 mono bytes from the converter.
    /// Publishes updated power level and history via the event.
    /// </summary>
    public void ProcessPCM16Buffer(byte[] pcm16Buffer, int byteCount)
    {
        if (byteCount < 2) return;

        // Calculate RMS from PCM16 samples (2 bytes per sample, little-endian, signed)
        double sumOfSquares = 0;
        int sampleCount = byteCount / 2;

        for (int i = 0; i < byteCount - 1; i += 2)
        {
            short sample = BitConverter.ToInt16(pcm16Buffer, i);
            double normalized = sample / 32768.0; // Normalize to [-1, 1]
            sumOfSquares += normalized * normalized;
        }

        double rms = Math.Sqrt(sumOfSquares / sampleCount);
        double boosted = Math.Clamp(rms * BoostMultiplier, 0, 1);

        // Smooth: use the higher of new value or decayed previous value
        _currentPowerLevel = Math.Max(boosted, _currentPowerLevel * SmoothingFactor);

        // Append to history, drop oldest sample
        if (_powerHistory.Count >= HistoryLength)
            _powerHistory.Dequeue();
        _powerHistory.Enqueue(Math.Max(boosted, BaselineLevel));

        OnPowerLevelUpdated?.Invoke(_currentPowerLevel, _powerHistory.ToArray());
    }

    public void Reset()
    {
        _currentPowerLevel = 0;
        _powerHistory.Clear();
        for (int i = 0; i < HistoryLength; i++)
            _powerHistory.Enqueue(BaselineLevel);
    }
}
