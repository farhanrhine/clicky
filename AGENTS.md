# Clicky — Agent Instructions

<!-- Single source of truth. CLAUDE.md is a symlink to this file. -->

## What Is Clicky

macOS menu bar AI companion (Swift/SwiftUI). Windows 11 port lives at `clicky-windows/` (C#/.NET 8/WinUI 3). Both share the same Cloudflare Worker backend at `worker/`. **Never modify macOS files when working on Windows, and vice versa.**

---

## ⚠️ ABSOLUTE RULES — Never Violate

1. **No `xcodebuild`** — invalidates macOS TCC permissions (screen recording, accessibility)
2. **No `dotnet` CLI** (`build`, `run`, `publish`, `restore`) — unreliable for WinUI 3; always let the user build from Visual Studio 2022
3. **No scope creep** — only modify files listed in the current phase spec. Never "heroically" fix warnings or errors in previous-phase files
4. **No skipping merges** — never start a new phase branch until the previous PR is merged to `main` and synced locally
5. **Read the ENTIRE phase spec** — especially the bottom UI and Verification Checklist sections before writing any code
6. **No new features** — implement exactly what the spec says, nothing more

---

## Mandatory Context Init (Every Session / Every Phase)

Read in this order before writing any code:

1. `AGENTS.md` (this file)
2. `docs/specs/implementation_plan.md`
3. `docs/specs/index.md`
4. Target phase spec (e.g. `docs/specs/phase-03-*.md`) — read **all of it**

---

## Finlo Git Workflow

```bash
# 1. Pull latest main
git pull origin main

# 2. Create branch
git checkout -b feature/<step_number>-<feature_name>

# 3. Implement — follow spec exactly, build/verify in Visual Studio or Xcode

# 4. Commit (co-author required)
git add .
git commit -m $'Subject (Phase N)\n\n- Detail\n\nCo-Authored-By: Claude <claude@anthropic.com>'

# 5. Push + PR
git push origin feature/<step_number>-<feature_name>
# Open PR on GitHub → Merge → Delete remote branch

# 6. Sync locally
git checkout main && git pull origin main
git branch -D feature/<step_number>-<feature_name>
```

---

## Code Naming & Clarity

- **Clarity over concision** — long descriptive names are required. `originalQuestionLastAnsweredDate` not `answered`
- Pass arguments with the same name as the variable — never shorten or abbreviate
- Comments explain **why**, not what
- More lines = more readable: never sacrifice clarity for brevity

---

## macOS Conventions (leanring-buddy/)

- SwiftUI for all UI; AppKit only where required (NSPanel, NSWindow)
- `@MainActor` for all UI state updates; `async/await` throughout
- All buttons must show pointer cursor on hover
- Known non-blocking warnings (Swift 6 concurrency, deprecated `onChange`): **do not fix**
- Do not rename the project directory — the "leanring" typo is intentional/legacy

### macOS Build
```bash
open leanring-buddy.xcodeproj   # then Cmd+R in Xcode
```

---

## Windows Conventions (clicky-windows/)

- WinUI 3 only — no WPF, WinForms, Electron
- Target Windows 11 build 22000+ minimum; do not support Windows 10
- All UI updates via `DispatcherQueue.TryEnqueue` when called from non-UI thread
- `async`/`await` throughout; no `.Result` or `.Wait()` blocking calls
- Win32 P/Invoke declarations go at the **bottom** of the class that uses them

### Windows Build
```powershell
# Open in Visual Studio 2022 (Windows App SDK workload required)
start clicky-windows\ClickyWindows.sln
# Run: Ctrl+F5 in Visual Studio (Debug | x64)

# Release: Right-click Project → Publish in Visual Studio
# Installer: Open clicky-windows\installer\clicky-setup.iss → Build → Compile in Inno Setup
```

---

## Windows Architecture (Quick Reference)

| Layer | Technology |
|---|---|
| Language | C# / .NET 8 |
| UI | WinUI 3 (Windows App SDK 1.5+) |
| Audio capture | NAudio 2.x `WaveInEvent` |
| Audio playback | NAudio `WaveOutEvent` + MP3 |
| HTTP / SSE | `HttpClient` |
| WebSocket | `System.Net.WebSockets.ClientWebSocket` |
| Screen capture | `Windows.Graphics.Capture` |
| Global hotkey | Win32 `RegisterHotKey` + `SetWindowsHookEx` |
| Overlay window | WinUI 3 + `WS_EX_TRANSPARENT` |
| System tray | `H.NotifyIcon.WinUI` |
| Analytics | PostHog .NET SDK |

### macOS → Windows API Cheat Sheet

| macOS | Windows |
|---|---|
| `NSStatusItem` | `H.NotifyIcon.WinUI` |
| `NSPanel` borderless | WinUI 3 `Window` (no titlebar presenter) |
| `CGEvent.tapCreate` | `RegisterHotKey` + `SetWindowsHookEx` |
| `AVAudioEngine` + tap | `NAudio.WaveInEvent` |
| `AVAudioPlayer` | `NAudio.WaveOutEvent` + `Mp3FileReader` |
| `URLSession.webSocketTask` | `ClientWebSocket` |
| `URLSession.bytes(for:)` SSE | `HttpClient.GetStreamAsync` + `StreamReader` |
| `SCScreenshotManager` | `Windows.Graphics.Capture` |
| `NSWindow` transparent overlay | WinUI 3 + `WS_EX_TRANSPARENT \| WS_EX_LAYERED` |
| `UserDefaults` | `Windows.Storage.ApplicationData.Current.LocalSettings` |
| `SMAppService` (login item) | Registry `HKCU\...\CurrentVersion\Run` |
| `NSSpeechSynthesizer` | `System.Speech.Synthesis.SpeechSynthesizer` |

---

## Windows Phase Roadmap

| Phase | Spec File | Key Files Created |
|---|---|---|
| 00 | phase-00-project-scaffolding.md | `ClickyWindows.sln`, `App.xaml.cs` |
| 01 | phase-01-tray-and-panel.md | `TrayIconManager.cs`, `CompanionPanelWindow.xaml`, `DesignSystem.cs` |
| 02 | phase-02-global-hotkey.md | `GlobalHotkeyMonitor.cs`, `PushToTalkShortcut.cs` |
| 03 | phase-03-microphone-capture.md | `PushToTalkManager.cs`, `PCM16AudioConverter.cs`, `AudioPowerLevelTracker.cs` |
| 04 | phase-04-assemblyai-transcription.md | `AssemblyAIStreamingProvider.cs`, `ITranscriptionProvider.cs` |
| 05 | phase-05-claude-api.md | `ClaudeApiClient.cs`, `PointTagParser.cs`, `ClaudeConversationMessage.cs` |
| 06 | phase-06-tts-playback.md | `TtsAudioPlayer.cs` |
| 07 | phase-07-screen-capture.md | `ScreenCaptureUtility.cs`, `ScreenCaptureResult.cs` |
| 08 | phase-08-state-machine.md | `CompanionManager.cs` (complete), `CompanionVoiceState.cs` |
| 09 | phase-09-overlay-window.md | `OverlayWindow.cs`, `OverlayCanvas.cs` |
| 10 | phase-10-cursor-flight.md | `BezierFlightAnimator.cs` |
| 11 | phase-11-analytics-polish.md | `ClickyAnalytics.cs`, `PermissionsManager.cs` |
| 12 | phase-12-packaging.md | `Package.appxmanifest`, `clicky-setup.iss` |

---

## Cloudflare Worker (Shared — Never Modify)

All API calls go through `worker/src/index.ts`. The Windows app uses the same Worker URL as macOS.

| Route | Upstream |
|---|---|
| `POST /chat` | Anthropic Claude (or Groq in DEV_MODE) |
| `POST /tts` | ElevenLabs (or Groq in DEV_MODE) |
| `POST /transcribe-token` | AssemblyAI temp token (or Groq key in DEV_MODE) |

Set `CloudflareWorkerBaseUrl` in `Core/AppConfiguration.cs` before building.

---

## Self-Update Rule

When a phase adds new files, update the Phase Roadmap table above and the Windows Architecture key files list. Do NOT update for minor bug fixes or line count drift.
