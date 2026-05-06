// Screen/ScreenCaptureResult.cs
// Data model for one captured display. Mirrors the CompanionScreenCapture
// struct from CompanionScreenCaptureUtility.swift.

namespace ClickyWindows;

internal record ScreenCaptureResult(
    /// <summary>JPEG image bytes compressed to ~1280px max dimension.</summary>
    byte[] JpegBytes,
    /// <summary>Display label for Claude's context, e.g. "Screen 1 (primary)".</summary>
    string Label,
    /// <summary>Display width in physical pixels (before DPI scaling).</summary>
    int WidthInPixels,
    /// <summary>Display height in physical pixels (before DPI scaling).</summary>
    int HeightInPixels,
    /// <summary>
    /// The display's origin in virtual desktop coordinates (top-left).
    /// Used for mapping [POINT:x,y] coordinates to absolute screen position.
    /// </summary>
    System.Drawing.Point VirtualDesktopOrigin,
    /// <summary>True if this is the display that currently contains the mouse cursor.</summary>
    bool IsCursorScreen
);
