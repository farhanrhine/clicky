// Transcription/ITranscriptionProvider.cs
// Port of BuddyTranscriptionProvider protocol from macOS.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClickyWindows;

internal interface ITranscriptionProvider
{
    string DisplayName { get; }
    bool IsConfigured { get; }

    /// <summary>
    /// Opens a new streaming session. Returns when the session is ready to
    /// receive audio. Throws on connection failure.
    /// </summary>
    Task<ITranscriptionSession> StartSessionAsync(
        IReadOnlyList<string> keyterms,
        Action<string> onTranscriptUpdate,
        Action<string> onFinalTranscriptReady,
        Action<Exception> onError,
        CancellationToken cancellationToken = default);
}
