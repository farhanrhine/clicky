// Transcription/AssemblyAIStreamingProvider.cs
// Fetches a short-lived token from the Cloudflare Worker, opens an AssemblyAI
// v3 WebSocket, streams PCM16 audio, and delivers finalized turn transcripts.
// Port of AssemblyAIStreamingTranscriptionProvider.swift.

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Linq;

namespace ClickyWindows;

internal class AssemblyAIStreamingProvider : ITranscriptionProvider
{
    public string DisplayName => "AssemblyAI";
    public bool IsConfigured => true;

    // Single long-lived HttpClient shared across all token fetches.
    // Do NOT create per-request — matches macOS shared URLSession pattern.
    private static readonly HttpClient SharedHttpClient = new();

    public async Task<ITranscriptionSession> StartSessionAsync(
        IReadOnlyList<string> keyterms,
        Action<string> onTranscriptUpdate,
        Action<string> onFinalTranscriptReady,
        Action<Exception> onError,
        CancellationToken cancellationToken = default)
    {
        string token = await FetchTemporaryTokenAsync(cancellationToken);

        var session = new AssemblyAIStreamingSession(
            token, keyterms, onTranscriptUpdate, onFinalTranscriptReady, onError);

        await session.OpenAsync(cancellationToken);
        return session;
    }

    private static async Task<string> FetchTemporaryTokenAsync(
        CancellationToken cancellationToken)
    {
        using var response = await SharedHttpClient.PostAsync(
            AppConfiguration.TranscribeToken,
            content: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("AssemblyAI: token missing in response");
    }
}

internal class AssemblyAIStreamingSession : ITranscriptionSession
{
    public TimeSpan FinalTranscriptFallbackDelay =>
        TimeSpan.FromSeconds(AppConfiguration.FinalTranscriptFallbackDelay);

    private readonly string _token;
    private readonly IReadOnlyList<string> _keyterms;
    private readonly Action<string> _onTranscriptUpdate;
    private readonly Action<string> _onFinalTranscriptReady;
    private readonly Action<Exception> _onError;

    // A new ClientWebSocket per session is safe — matches .NET best practices.
    // The macOS "shared URLSession" concern does not apply to ClientWebSocket.
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource _cts = new();

    private readonly object _stateLock = new();
    private readonly Dictionary<int, string> _storedTurnTranscripts = new();
    private string _activeTurnTranscript = "";
    private int? _activeTurnOrder;
    private bool _isAwaitingFinalTranscript = false;
    private bool _hasDeliveredFinalTranscript = false;
    private string _latestTranscriptText = "";
    private Task? _receiveLoopTask;

    public AssemblyAIStreamingSession(
        string token, IReadOnlyList<string> keyterms,
        Action<string> onTranscriptUpdate,
        Action<string> onFinalTranscriptReady,
        Action<Exception> onError)
    {
        _token = token;
        _keyterms = keyterms;
        _onTranscriptUpdate = onTranscriptUpdate;
        _onFinalTranscriptReady = onFinalTranscriptReady;
        _onError = onError;
    }

    public async Task OpenAsync(CancellationToken cancellationToken)
    {
        _webSocket = new ClientWebSocket();
        var wsUrl = BuildWebSocketUrl();

        await _webSocket.ConnectAsync(wsUrl, cancellationToken);

        // Start receive loop on background task — do not await
        _receiveLoopTask = Task.Run(ReceiveLoopAsync);
    }

    public void AppendPCM16AudioChunk(byte[] pcm16AudioBytes)
    {
        if (_webSocket?.State != WebSocketState.Open) return;

        // Fire-and-forget send — WebSocket send is not thread-safe, use lock
        _ = Task.Run(async () =>
        {
            try
            {
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(pcm16AudioBytes),
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    _cts.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"AssemblyAI: audio send error: {ex.Message}");
            }
        });
    }

    public void RequestFinalTranscript()
    {
        lock (_stateLock)
        {
            if (_hasDeliveredFinalTranscript) return;
            _isAwaitingFinalTranscript = true;
        }

        SendJsonMessage(new { type = "ForceEndpoint" });

        // Fallback timer — deliver whatever we have after grace period
        _ = Task.Delay(TimeSpan.FromSeconds(AppConfiguration.FinalTranscriptGracePeriod))
            .ContinueWith(_ =>
            {
                lock (_stateLock)
                {
                    if (!_hasDeliveredFinalTranscript && _isAwaitingFinalTranscript)
                        DeliverFinalTranscript(BestAvailableTranscript());
                }
            });
    }

    public void Cancel()
    {
        SendJsonMessage(new { type = "Terminate" });
        _cts.Cancel();
        _webSocket?.CloseAsync(
            WebSocketCloseStatus.NormalClosure, "cancelled",
            CancellationToken.None);
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[16384];
        try
        {
            while (_webSocket?.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), _cts.Token);

                if (result.MessageType == WebSocketMessageType.Close) break;

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                HandleMessage(json);
            }
        }
        catch (OperationCanceledException) { /* Normal shutdown */ }
        catch (Exception ex) { _onError(ex); }
    }

    private void HandleMessage(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var type = doc.RootElement.GetProperty("type").GetString() ?? "";

            switch (type.ToLower())
            {
                case "begin":
                    // Session ready — nothing to do (OpenAsync continuation handles this)
                    break;
                case "turn":
                    HandleTurnMessage(doc.RootElement);
                    break;
                case "termination":
                    lock (_stateLock)
                    {
                        if (_isAwaitingFinalTranscript && !_hasDeliveredFinalTranscript)
                            DeliverFinalTranscript(BestAvailableTranscript());
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _onError(ex);
        }
    }

    private void HandleTurnMessage(JsonElement root)
    {
        var transcript = root.TryGetProperty("transcript", out var t)
            ? t.GetString()?.Trim() ?? "" : "";
        var turnOrder = root.TryGetProperty("turn_order", out var to)
            ? to.GetInt32() : (_activeTurnOrder ?? 0);
        var endOfTurn = root.TryGetProperty("end_of_turn", out var eot)
            && eot.GetBoolean();
        var isFormatted = root.TryGetProperty("turn_is_formatted", out var tf)
            && tf.GetBoolean();

        lock (_stateLock)
        {
            if (endOfTurn || isFormatted)
            {
                if (!string.IsNullOrEmpty(transcript))
                    _storedTurnTranscripts[turnOrder] = transcript;
                _activeTurnOrder = null;
                _activeTurnTranscript = "";
            }
            else
            {
                _activeTurnOrder = turnOrder;
                _activeTurnTranscript = transcript;
            }

            _latestTranscriptText = ComposeFullTranscript();
            if (!string.IsNullOrEmpty(_latestTranscriptText))
                _onTranscriptUpdate(_latestTranscriptText);

            if (_isAwaitingFinalTranscript && (endOfTurn || isFormatted))
                DeliverFinalTranscript(BestAvailableTranscript());
        }
    }

    private void DeliverFinalTranscript(string transcript)
    {
        if (_hasDeliveredFinalTranscript) return;
        _hasDeliveredFinalTranscript = true;
        _onFinalTranscriptReady(transcript);
        SendJsonMessage(new { type = "Terminate" });
    }

    private string ComposeFullTranscript()
    {
        var parts = _storedTurnTranscripts
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => kvp.Value)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
        if (!string.IsNullOrEmpty(_activeTurnTranscript))
            parts.Add(_activeTurnTranscript);
        return string.Join(" ", parts);
    }

    private string BestAvailableTranscript()
    {
        var composed = ComposeFullTranscript().Trim();
        return !string.IsNullOrEmpty(composed) ? composed : _latestTranscriptText.Trim();
    }

    private void SendJsonMessage(object payload)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            _webSocket?.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None);
        }
        catch { /* ignore send errors during teardown */ }
    }

    private Uri BuildWebSocketUrl()
    {
        var keytermsJson = JsonSerializer.Serialize(_keyterms);
        var query = $"sample_rate=16000&encoding=pcm_s16le" +
                    $"&format_turns=true&speech_model=u3-rt-pro" +
                    $"&token={Uri.EscapeDataString(_token)}";

        if (_keyterms.Count > 0)
            query += $"&keyterms_prompt={Uri.EscapeDataString(keytermsJson)}";

        return new Uri($"{AppConfiguration.AssemblyAIWebSocketUrl}?{query}");
    }

    public void Dispose()
    {
        Cancel();
        _webSocket?.Dispose();
        _cts.Dispose();
    }
}
