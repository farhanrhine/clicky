// AI/ClaudeConversationMessage.cs
// Represents one turn in the Claude conversation history.
// Mirrors the message structure in ClaudeAPI.swift.

using System.Collections.Generic;

namespace ClickyWindows;

internal enum ClaudeMessageRole { User, Assistant }

internal record ClaudeConversationMessage(
    ClaudeMessageRole Role,
    string Text,
    // Optional screenshots attached to user messages
    IReadOnlyList<ClaudeImageAttachment>? Images = null
);

internal record ClaudeImageAttachment(
    string Base64Data,
    string MediaType  // "image/jpeg" or "image/png"
);
