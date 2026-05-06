// Audio/TtsAudioPlayer.cs
// Sends text to the ElevenLabs endpoint on the Cloudflare Worker and plays
// back the resulting MP3 audio. Port of ElevenLabsTTSClient.swift.
//
// Key design decisions vs macOS:
//   - Uses NAudio WaveOutEvent + Mp3FileReader instead of AVAudioPlayer.
//   - Downloads the full MP3 before playing (matches macOS behavior —
//     the Worker returns the full audio in one response, not streamed).
//   - IsPlaying property mirrors the macOS version for transient cursor logic.

using NAudio.Wave;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClickyWindows;

internal class TtsAudioPlayer : IDisposable
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private WaveOutEvent? _waveOut;
    private Mp3FileReader? _mp3Reader;
    private bool _isDisposed = false;

    /// <summary>Whether TTS audio is currently playing back.</summary>
    public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;

    /// <summary>
    /// Fires when TTS playback finishes naturally (not when stopped manually).
    /// </summary>
    public event Action? OnPlaybackCompleted;

    /// <summary>
    /// Sends text to ElevenLabs via the Worker and plays the audio.
    /// Returns when playback has started (does not block until audio ends).
    /// </summary>
    public async Task SpeakAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        // Stop any currently playing audio immediately
        StopPlayback();

        byte[] audioBytes = await FetchAudioFromWorkerAsync(text, cancellationToken);
        if (audioBytes.Length == 0)
        {
            // ElevenLabs unavailable — use Windows SAPI as fallback
            SpeakWithSapi(text);
            return;
        }

        PlayAudioBytes(audioBytes);
    }

    private static async Task<byte[]> FetchAudioFromWorkerAsync(
        string text,
        CancellationToken cancellationToken)
    {
        // Request body matches ElevenLabsTTSClient.swift
        var requestBody = new
        {
            text,
            model_id = "eleven_flash_v2_5",
            voice_settings = new
            {
                stability = 0.5,
                similarity_boost = 0.75
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post,
            AppConfiguration.TtsEndpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("audio/mpeg"));

        try
        {
            using var response = await SharedHttpClient.SendAsync(
                request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                System.Diagnostics.Debug.WriteLine(
                    $"TTS error {(int)response.StatusCode}: {error}");
                return Array.Empty<byte>();
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TTS network error: {ex.Message}");
            return Array.Empty<byte>();
        }
    }

    private void PlayAudioBytes(byte[] mp3Bytes)
    {
        try
        {
            var audioStream = new MemoryStream(mp3Bytes);
            _mp3Reader = new Mp3FileReader(audioStream);

            _waveOut = new WaveOutEvent
            {
                DesiredLatency = 100 // milliseconds
            };

            _waveOut.PlaybackStopped += (_, args) =>
            {
                // Only fire completed event for natural end, not manual stop
                if (args.Exception == null)
                    OnPlaybackCompleted?.Invoke();

                CleanupPlayback();
            };

            _waveOut.Init(_mp3Reader);
            _waveOut.Play();

            System.Diagnostics.Debug.WriteLine(
                $"TTS: playing {mp3Bytes.Length / 1024}KB audio");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TTS playback error: {ex.Message}");
            CleanupPlayback();
        }
    }

    // Fallback TTS using Windows Speech API (System.Speech)
    // This is the Windows equivalent of NSSpeechSynthesizer on macOS.
    private static void SpeakWithSapi(string text)
    {
        try
        {
            // System.Speech is in the System.Speech NuGet package
            using var synth = new System.Speech.Synthesis.SpeechSynthesizer();
            synth.SetOutputToDefaultAudioDevice();
            synth.Rate = 1;   // -10 to 10, 0 = normal
            synth.Volume = 90; // 0–100
            synth.Speak(text);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SAPI fallback error: {ex.Message}");
        }
    }

    /// <summary>Stops any in-progress playback immediately.</summary>
    public void StopPlayback()
    {
        _waveOut?.Stop();
        CleanupPlayback();
    }

    private void CleanupPlayback()
    {
        _waveOut?.Dispose();
        _waveOut = null;
        _mp3Reader?.Dispose();
        _mp3Reader = null;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        StopPlayback();
    }
}
