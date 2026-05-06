// Core/CompanionVoiceState.cs
// The four voice states of the companion. Mirrors VoiceState in CompanionManager.swift.
// All UI and overlay behavior is driven by this state.

namespace ClickyWindows;

internal enum CompanionVoiceState
{
    /// <summary>Waiting for push-to-talk. Cursor is visible but idle.</summary>
    Idle,

    /// <summary>Ctrl+Alt is held. Microphone is open, audio is streaming.</summary>
    Listening,

    /// <summary>Key released. Waiting for Claude's first response token.</summary>
    Processing,

    /// <summary>Streaming Claude's response text and TTS audio.</summary>
    Responding
}
