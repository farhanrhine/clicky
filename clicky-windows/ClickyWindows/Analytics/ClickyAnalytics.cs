// Analytics/ClickyAnalytics.cs
// PostHog analytics wrapper. Same event names as ClickyAnalytics.swift
// so both Mac and Windows events appear on the same PostHog dashboard.

using PostHog;

namespace ClickyWindows;

internal static class ClickyAnalytics
{
    private const string PostHogApiKey = "phc_xcQPygmhTMzzYh8wNW92CCwoXmnzqyChAixh8zgpqC3C";
    private const string PostHogHost = "https://us.i.posthog.com";

    private static PostHogClient? _client;
    private static readonly string AnonymousDistinctId = GetOrCreateDistinctId();

    public static void Configure()
    {
        _client = new PostHogClient(PostHogApiKey, new PostHogOptions
        {
            Host = new Uri(PostHogHost)
        });
    }

    // ── App Lifecycle ────────────────────────────────────────────────────────

    public static void TrackAppOpened()
    {
        var version = System.Reflection.Assembly
            .GetExecutingAssembly()
            .GetName()
            .Version?.ToString() ?? "unknown";

        Capture("app_opened", new Dictionary<string, object>
        {
            ["app_version"] = version,
            ["platform"] = "windows"
        });
    }

    // ── Permissions ──────────────────────────────────────────────────────────

    public static void TrackAllPermissionsGranted()
        => Capture("all_permissions_granted");

    public static void TrackPermissionGranted(string permission)
        => Capture("permission_granted", new Dictionary<string, object>
           { ["permission"] = permission });

    // ── Voice Interaction ────────────────────────────────────────────────────

    public static void TrackPushToTalkStarted()
        => Capture("push_to_talk_started");

    public static void TrackPushToTalkReleased()
        => Capture("push_to_talk_released");

    public static void TrackUserMessageSent(string transcript)
        => Capture("user_message_sent", new Dictionary<string, object>
        {
            ["transcript"] = transcript,
            ["character_count"] = transcript.Length
        });

    public static void TrackAIResponseReceived(string response)
        => Capture("ai_response_received", new Dictionary<string, object>
        {
            ["response"] = response,
            ["character_count"] = response.Length
        });

    public static void TrackElementPointed(string? label)
        => Capture("element_pointed", new Dictionary<string, object>
           { ["element_label"] = label ?? "unknown" });

    // ── Onboarding ───────────────────────────────────────────────────────────

    public static void TrackOnboardingStarted()  => Capture("onboarding_started");
    public static void TrackOnboardingCompleted() => Capture("onboarding_completed");

    // ── Errors ───────────────────────────────────────────────────────────────

    public static void TrackResponseError(string error)
        => Capture("response_error", new Dictionary<string, object>
           { ["error"] = error });

    public static void TrackTtsError(string error)
        => Capture("tts_error", new Dictionary<string, object>
           { ["error"] = error });

    // ── Internal ─────────────────────────────────────────────────────────────

    private static void Capture(string eventName,
        Dictionary<string, object>? properties = null)
    {
        _client?.Capture(AnonymousDistinctId, eventName, properties);
    }

    /// <summary>
    /// Gets or creates a stable anonymous identifier for this installation.
    /// Stored in ApplicationData so it persists across updates.
    /// </summary>
    private static string GetOrCreateDistinctId()
    {
        const string key = "ClickyDistinctId";
        var settings = Windows.Storage.ApplicationData.Current.LocalSettings;

        if (settings.Values.TryGetValue(key, out var existing) &&
            existing is string existingId && !string.IsNullOrEmpty(existingId))
        {
            return existingId;
        }

        var newId = Guid.NewGuid().ToString();
        settings.Values[key] = newId;
        return newId;
    }
}
