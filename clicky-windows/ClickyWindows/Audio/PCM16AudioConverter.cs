// Audio/PCM16AudioConverter.cs
// Converts WaveInEvent buffers (typically 44.1kHz or 48kHz float stereo)
// to 16kHz PCM16 mono — the exact format AssemblyAI's WebSocket expects.
// Port of BuddyPCM16AudioConverter from BuddyAudioConversionSupport.swift.

using System;
using System.IO;
using NAudio.Wave;

namespace ClickyWindows;

internal class PCM16AudioConverter : IDisposable
{
    private readonly WaveFormat _targetFormat;
    private MediaFoundationResampler? _resampler;
    private WaveFormat? _currentInputFormat;

    // AssemblyAI requires 16kHz, 16-bit, mono
    private const int TargetSampleRate = 16_000;
    private const int TargetBitsPerSample = 16;
    private const int TargetChannels = 1;

    public PCM16AudioConverter()
    {
        _targetFormat = new WaveFormat(TargetSampleRate, TargetBitsPerSample, TargetChannels);
    }

    /// <summary>
    /// Converts a raw byte buffer from WaveInEvent to 16kHz PCM16 mono bytes.
    /// Returns null if conversion fails.
    /// </summary>
    public byte[]? ConvertToPCM16(byte[] inputBuffer, int bytesRecorded, WaveFormat inputFormat)
    {
        if (bytesRecorded == 0) return null;

        // Recreate resampler if input format changed (e.g. device switch)
        if (_currentInputFormat == null || !_currentInputFormat.Equals(inputFormat))
        {
            _resampler?.Dispose();
            var inputProvider = new RawSourceWaveStream(
                new MemoryStream(inputBuffer, 0, bytesRecorded), inputFormat);
            _resampler = new MediaFoundationResampler(inputProvider, _targetFormat)
            {
                ResamplerQuality = 60
            };
            _currentInputFormat = inputFormat;
        }

        // Re-provide data to the existing resampler
        // (MediaFoundationResampler wraps a stream, so we create a new one each call)
        var stream = new RawSourceWaveStream(
            new MemoryStream(inputBuffer, 0, bytesRecorded), inputFormat);
        var resampler = new MediaFoundationResampler(stream, _targetFormat)
        {
            ResamplerQuality = 60
        };

        using var outputStream = new MemoryStream();
        var readBuffer = new byte[4096];
        int bytesRead;
        try
        {
            while ((bytesRead = resampler.Read(readBuffer, 0, readBuffer.Length)) > 0)
            {
                outputStream.Write(readBuffer, 0, bytesRead);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PCM16AudioConverter: Error during resample: {ex.Message}");
            return null;
        }
        finally
        {
            resampler.Dispose();
        }

        var result = outputStream.ToArray();
        return result.Length > 0 ? result : null;
    }

    public void Dispose()
    {
        _resampler?.Dispose();
    }
}
