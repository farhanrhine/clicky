// Transcription/ITranscriptionSession.cs
// Port of BuddyStreamingTranscriptionSession protocol from macOS.

using System;

namespace ClickyWindows;

internal interface ITranscriptionSession : IDisposable
{
    /// <summary>
    /// How long to wait after requesting the final transcript before
    /// delivering whatever transcript we have as a fallback.
    /// </summary>
    TimeSpan FinalTranscriptFallbackDelay { get; }

    /// <summary>
    /// Stream a chunk of 16kHz PCM16 mono audio bytes to the provider.
    /// </summary>
    void AppendPCM16AudioChunk(byte[] pcm16AudioBytes);

    /// <summary>
    /// Signal that the user released the push-to-talk key.
    /// The session should finalize the transcript and call OnFinalTranscriptReady.
    /// </summary>
    void RequestFinalTranscript();

    /// <summary>
    /// Abandon the session. Any in-progress transcript is discarded.
    /// </summary>
    void Cancel();
}
