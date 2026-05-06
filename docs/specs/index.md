# Clicky Windows Port — Spec Index

> **For AI agents**: Read this file first. Each phase is a self-contained spec.
> Execute one phase at a time. Do not start a phase until the previous one passes
> its verification checklist. Never run `xcodebuild` — it breaks macOS TCC permissions.

---

## What Is This?

Clicky is a macOS AI companion that lives in the menu bar. This spec set describes
how to build a **native Windows 11 port** of Clicky in **C# / .NET 8 / WinUI 3**.

The Windows code lives at `c:\clicky\clicky-windows\` alongside the existing macOS
code at `c:\clicky\leanring-buddy\`. The Cloudflare Worker at `c:\clicky\worker\`
is **shared** — no changes needed to the backend.

---

## Background Documents

Read these before the phase specs if you want full context on **why** decisions
were made. They are not required to execute the phases, but they explain the
architecture reasoning, tech stack choices, and macOS comparison.

| File | What It Contains |
|---|---|
| [clicky_windows_port_analysis.md](clicky_windows_port_analysis.md) | Deep analysis of the macOS codebase — every file examined, what needs to be ported, what can be reused, and why Windows 11 only. Written before any code was planned. |
| [implementation_plan.md](implementation_plan.md) | High-level architecture plan — tech stack decision rationale (why C# over Rust, why WinUI 3 over WPF/Electron), full phase overview, macOS→Windows API mapping table, and open questions that were resolved before specs were written. |

---

## Tech Stack (Quick Reference)

| Layer | Technology |
|---|---|
| Language | C# / .NET 8 |
| UI | WinUI 3 (Windows App SDK 1.5+) |
| Audio capture | NAudio 2.x `WaveInEvent` |
| Audio playback | NAudio `WaveOutEvent` + MP3 |
| HTTP / SSE | `System.Net.Http.HttpClient` |
| WebSocket | `System.Net.WebSockets.ClientWebSocket` |
| Screen capture | `Windows.Graphics.Capture` |
| Global hotkey | Win32 `RegisterHotKey` + `SetWindowsHookEx` |
| Overlay window | WinUI 3 + `WS_EX_TRANSPARENT` |
| System tray | `H.NotifyIcon.WinUI` |
| Analytics | PostHog .NET SDK |
| Target OS | Windows 11 (build 22000+) only |

---

## The 13 Phases

Each phase has its own spec file with: goal, prerequisites, full code, and a
verification checklist. Complete them **in order**.

| Phase | File | What It Builds | Key Files Created |
|---|---|---|---|
| **00** | [phase-00-project-scaffolding.md](phase-00-project-scaffolding.md) | WinUI 3 solution, folder structure, NuGet packages | `ClickyWindows.sln`, `App.xaml.cs`, all empty folders |
| **01** | [phase-01-tray-and-panel.md](phase-01-tray-and-panel.md) | System tray icon, floating dark panel | `TrayIconManager.cs`, `CompanionPanelWindow.xaml`, `DesignSystem.cs` |
| **02** | [phase-02-global-hotkey.md](phase-02-global-hotkey.md) | Ctrl+Alt push-to-talk (system-wide) | `GlobalHotkeyMonitor.cs`, `PushToTalkShortcut.cs` |
| **03** | [phase-03-microphone-capture.md](phase-03-microphone-capture.md) | Mic capture → 16kHz PCM16 mono | `PushToTalkManager.cs`, `PCM16AudioConverter.cs`, `AudioPowerLevelTracker.cs` |
| **04** | [phase-04-assemblyai-transcription.md](phase-04-assemblyai-transcription.md) | AssemblyAI WebSocket streaming ASR | `AssemblyAIStreamingProvider.cs`, `ITranscriptionProvider.cs`, `AppConfiguration.cs` |
| **05** | [phase-05-claude-api.md](phase-05-claude-api.md) | Claude API + SSE streaming + `[POINT]` parser | `ClaudeApiClient.cs`, `PointTagParser.cs`, `ClaudeConversationMessage.cs` |
| **06** | [phase-06-tts-playback.md](phase-06-tts-playback.md) | ElevenLabs TTS + NAudio playback + SAPI fallback | `TtsAudioPlayer.cs` |
| **07** | [phase-07-screen-capture.md](phase-07-screen-capture.md) | Multi-monitor JPEG capture, cursor-screen-first | `ScreenCaptureUtility.cs`, `ScreenCaptureResult.cs` |
| **08** | [phase-08-state-machine.md](phase-08-state-machine.md) | Full `CompanionManager` state machine wiring everything together | `CompanionManager.cs` (complete), `CompanionVoiceState.cs` |
| **09** | [phase-09-overlay-window.md](phase-09-overlay-window.md) | Transparent click-through overlay, blue cursor, waveform, spinner | `OverlayWindow.cs`, `OverlayCanvas.cs`, `OverlayManager.cs` |
| **10** | [phase-10-cursor-flight.md](phase-10-cursor-flight.md) | Bezier arc cursor flight to pointed UI elements | `BezierFlightAnimator.cs`, `CursorFlightState.cs` |
| **11** | [phase-11-analytics-polish.md](phase-11-analytics-polish.md) | PostHog analytics, mic permissions, startup registration, onboarding | `ClickyAnalytics.cs`, `PermissionsManager.cs`, `StartupRegistration.cs` |
| **12** | [phase-12-packaging.md](phase-12-packaging.md) | MSIX + Inno Setup installer, README + AGENTS.md update | `Package.appxmanifest`, `clicky-setup.iss` |

---

## macOS → Windows API Cheat Sheet

| macOS | Windows |
|---|---|
| `NSStatusItem` | `H.NotifyIcon.WinUI` |
| `NSPanel` (borderless) | WinUI 3 `Window` (no titlebar presenter) |
| `CGEvent.tapCreate` | `RegisterHotKey` + `SetWindowsHookEx` |
| `AVAudioEngine` + tap | `NAudio.WaveInEvent` |
| `AVAudioPlayer` | `NAudio.WaveOutEvent` + `Mp3FileReader` |
| `URLSession.webSocketTask` | `System.Net.WebSockets.ClientWebSocket` |
| `URLSession.bytes(for:)` SSE | `HttpClient.GetStreamAsync` + `StreamReader` |
| `SCScreenshotManager` | `Windows.Graphics.Capture` |
| `NSWindow` transparent overlay | WinUI 3 + `WS_EX_TRANSPARENT \| WS_EX_LAYERED` |
| `UserDefaults` | `Windows.Storage.ApplicationData.Current.LocalSettings` |
| `SMAppService` (login item) | Registry `HKCU\...\CurrentVersion\Run` |
| `NSSpeechSynthesizer` | `System.Speech.Synthesis.SpeechSynthesizer` |
| `NSWorkspace.shared.open(url)` | `Windows.System.Launcher.LaunchUriAsync(uri)` |

---

## Config Before You Start

### 1 — Set the Worker URL (required)

Open `Core/AppConfiguration.cs` and set your deployed Worker URL:

```csharp
public const string CloudflareWorkerBaseUrl =
    "https://your-worker-name.your-subdomain.workers.dev";
```

This is the **same Worker** the macOS app uses. The three routes are:
`/chat`, `/tts`, `/transcribe-token`.

### 2 — Choose: Dev Mode (free) or Production (paid)

The Worker has a `DEV_MODE` switch. Flip it in one command — **no app code changes needed**.

#### Dev Mode — Groq (free, for building & testing)

```bash
cd worker
npx wrangler secret put GROQ_API_KEY   # paste your key from console.groq.com
npx wrangler secret put DEV_MODE       # type: true
npx wrangler deploy
```

| What gets replaced | Groq model used |
|---|---|
| Claude (chat + vision) | `meta-llama/llama-4-scout-17b-16e-instruct` — vision ✅ |
| ElevenLabs (TTS) | `canopylabs/orpheus-v1-english` |
| AssemblyAI (STT) | `whisper-large-v3-turbo` (upload on key release) |

> In dev mode, live partial transcripts while holding Ctrl+Alt are not shown.
> The final transcript still arrives correctly after key release.

#### Production Mode — Claude / ElevenLabs / AssemblyAI (paid)

```bash
npx wrangler secret put ANTHROPIC_API_KEY
npx wrangler secret put ELEVENLABS_API_KEY
npx wrangler secret put ASSEMBLYAI_API_KEY
npx wrangler secret put DEV_MODE       # type: false
npx wrangler deploy
```

#### Local dev (wrangler dev)

Create `worker/.dev.vars` (never commit this file):
```
ANTHROPIC_API_KEY=sk-ant-...
ASSEMBLYAI_API_KEY=...
ELEVENLABS_API_KEY=...
GROQ_API_KEY=gsk_...
DEV_MODE=true
```

---

## Voice State Machine (At a Glance)

```
Idle ──[Ctrl+Alt held]──▶ Listening ──[released]──▶ Processing ──[first token]──▶ Responding
 ▲                                                                                       │
 └───────────────────────────────────[TTS ends]──────────────────────────────────────────┘
```

Any new Ctrl+Alt press from any state cancels the current pipeline and returns to Listening.

---

## Repo Structure After All Phases

```
c:\clicky\
├── leanring-buddy/           macOS Swift source (untouched)
├── leanring-buddy.xcodeproj/
├── worker/                   Cloudflare Worker (shared, untouched)
├── docs/
│   └── specs/                ← you are here
│       ├── index.md
│       ├── phase-00-project-scaffolding.md
│       ├── phase-01-tray-and-panel.md
│       ├── phase-02-global-hotkey.md
│       ├── phase-03-microphone-capture.md
│       ├── phase-04-assemblyai-transcription.md
│       ├── phase-05-claude-api.md
│       ├── phase-06-tts-playback.md
│       ├── phase-07-screen-capture.md
│       ├── phase-08-state-machine.md
│       ├── phase-09-overlay-window.md
│       ├── phase-10-cursor-flight.md
│       ├── phase-11-analytics-polish.md
│       └── phase-12-packaging.md
├── clicky-windows/           Windows C# source (built across all phases)
│   ├── ClickyWindows.sln
│   └── ClickyWindows/
│       ├── Core/             CompanionManager, AppConfiguration, PermissionsManager
│       ├── Audio/            PushToTalkManager, TtsAudioPlayer, PCM16AudioConverter
│       ├── Transcription/    AssemblyAIStreamingProvider, interfaces
│       ├── AI/               ClaudeApiClient, ElevenLabsTtsClient, PointTagParser
│       ├── Screen/           ScreenCaptureUtility, ScreenCaptureResult
│       ├── Input/            GlobalHotkeyMonitor, PushToTalkShortcut
│       ├── Overlay/          OverlayWindow, BezierFlightAnimator, OverlayCanvas
│       ├── Panel/            TrayIconManager, CompanionPanelWindow
│       ├── Styles/           DesignSystem
│       └── Analytics/        ClickyAnalytics
├── AGENTS.md
└── README.md
```

---

## For AI Agents — Rules of Engagement

> [!IMPORTANT]
> **Mandatory Context Initialization**: Before starting any phase, you MUST read the following files in order to ensure you have the full context of the architecture and workflow:
> 1. `AGENTS.md` (root)
> 2. `docs/specs/implementation_plan.md`
> 3. `docs/specs/index.md` (this file)
> 4. The specific Phase spec file for the current task.

1. **Read the spec file for the target phase completely before writing any code.**
2. **Do not skip phases.** Each phase's prerequisite list must be satisfied.
3. **Do not modify macOS files** (`leanring-buddy/`, `.xcodeproj/`, `worker/`).
4. **Do not run `xcodebuild`** — it breaks macOS TCC permissions.
5. **Verify the checklist** at the bottom of each spec before marking a phase done.
6. **If a phase fails to build**, fix it before moving to the next phase.
7. **Token limit**: if a response would exceed limits, complete the current file
   and stop — do not truncate code mid-function.
