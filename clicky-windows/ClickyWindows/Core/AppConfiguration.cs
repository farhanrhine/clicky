// Core/AppConfiguration.cs
// Centralizes all configurable values. The Cloudflare Worker URL is the
// single value that must be set before the app works — same as the
// tokenProxyURL in AssemblyAIStreamingTranscriptionProvider.swift.

namespace ClickyWindows;

internal static class AppConfiguration
{
    // ── Cloudflare Worker ────────────────────────────────────────────────────
    // Set this to your actual Worker URL. Example:
    // "https://clicky-proxy.youraccount.workers.dev"
    // This is the SAME Worker used by the macOS app. No changes to the Worker needed.
    public const string CloudflareWorkerBaseUrl =
        "https://your-worker-name.your-subdomain.workers.dev";

    public static string ChatEndpoint    => $"{CloudflareWorkerBaseUrl}/chat";
    public static string TtsEndpoint     => $"{CloudflareWorkerBaseUrl}/tts";
    public static string TranscribeToken => $"{CloudflareWorkerBaseUrl}/transcribe-token";

    // ── AI Models ─────────────────────────────────────────────────────────────
    public const string DefaultClaudeModel = "claude-sonnet-4-6";

    // ── AssemblyAI ────────────────────────────────────────────────────────────
    public const string AssemblyAIWebSocketUrl = "wss://streaming.assemblyai.com/v3/ws";
    public const int    AssemblyAITargetSampleRate = 16_000;
    // Grace period before fallback final transcript delivery (seconds)
    public const double FinalTranscriptGracePeriod = 1.4;
    public const double FinalTranscriptFallbackDelay = 2.8;
}
