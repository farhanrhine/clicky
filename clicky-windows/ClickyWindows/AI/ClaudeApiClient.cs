// AI/ClaudeApiClient.cs
// Sends messages to Claude via the Cloudflare Worker's /chat endpoint.
// Streams the response using Server-Sent Events (SSE), yielding text chunks
// as they arrive. Port of ClaudeAPI.swift.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClickyWindows;

internal class ClaudeApiClient
{
    // Single long-lived HttpClient — HttpClient is designed to be reused.
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(120)
    };

    private const int MaxConversationHistoryMessages = 20; // 10 exchanges

    /// <summary>
    /// Streams a Claude response as an async enumerable of text chunks.
    /// Each yielded string is a partial token (SSE delta).
    /// </summary>
    public static async IAsyncEnumerable<string> StreamResponseAsync(
        string userTranscript,
        IReadOnlyList<ClaudeConversationMessage> conversationHistory,
        IReadOnlyList<ClaudeImageAttachment>? screenshots,
        string modelId = AppConfiguration.DefaultClaudeModel,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = BuildMessages(userTranscript, conversationHistory, screenshots);
        var requestBody = BuildRequestBody(messages, modelId, streaming: true);

        using var request = new HttpRequestMessage(HttpMethod.Post,
            AppConfiguration.ChatEndpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json")
        };

        using var response = await SharedHttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        // SSE format: each event is "data: {...}\n\n"
        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line.Substring(6).Trim();
            if (data == "[DONE]") break;

            string? textChunk = ExtractTextDelta(data);
            if (textChunk != null)
                yield return textChunk;
        }
    }

    private static string? ExtractTextDelta(string sseData)
    {
        try
        {
            var doc = JsonDocument.Parse(sseData);
            var root = doc.RootElement;

            // Check for content_block_delta events
            if (root.TryGetProperty("type", out var typeEl) &&
                typeEl.GetString() == "content_block_delta")
            {
                if (root.TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("text", out var text))
                {
                    return text.GetString();
                }
            }
        }
        catch { /* skip malformed SSE lines */ }
        return null;
    }

    private static List<object> BuildMessages(
        string userTranscript,
        IReadOnlyList<ClaudeConversationMessage> history,
        IReadOnlyList<ClaudeImageAttachment>? screenshots)
    {
        var messages = new List<object>();

        // Include conversation history (truncated to max)
        var recentHistory = history
            .TakeLast(MaxConversationHistoryMessages)
            .ToList();

        foreach (var msg in recentHistory)
        {
            messages.Add(new
            {
                role = msg.Role == ClaudeMessageRole.User ? "user" : "assistant",
                content = msg.Text
            });
        }

        // Build current user message content blocks
        var contentBlocks = new List<object>();

        // Attach screenshots first (same order as macOS)
        if (screenshots != null)
        {
            foreach (var img in screenshots)
            {
                contentBlocks.Add(new
                {
                    type = "image",
                    source = new
                    {
                        type = "base64",
                        media_type = img.MediaType,
                        data = img.Base64Data
                    }
                });
            }
        }

        contentBlocks.Add(new { type = "text", text = userTranscript });

        messages.Add(new
        {
            role = "user",
            content = contentBlocks
        });

        return messages;
    }

    private static object BuildRequestBody(
        List<object> messages, string modelId, bool streaming)
    {
        return new
        {
            model = modelId,
            max_tokens = 1024,
            stream = streaming,
            system = BuildSystemPrompt(),
            messages
        };
    }

    private static string BuildSystemPrompt() => """
        You are Clicky, a helpful AI companion that lives in the Windows system tray.
        The user activates you by holding Ctrl+Alt to talk. You receive their voice
        transcript and a screenshot of their current screen(s).

        If the user is asking about a specific UI element visible in the screenshot,
        include a [POINT:x,y:label:screen0] tag in your response where x and y are
        fractions (0.0–1.0) of the display dimensions indicating the element's location.

        Keep responses concise and conversational — you're speaking out loud via TTS.
        Avoid markdown formatting in your response.
        """;
}
