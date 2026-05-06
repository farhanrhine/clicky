// Overlay/OverlayCanvas.cs
// The WinUI Canvas element that renders the cursor, waveform, and text bubble.
// Uses DispatcherTimer at 60fps to animate the cursor following the mouse.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using System;
using System.Linq;

namespace ClickyWindows;

internal class OverlayCanvas : Canvas
{
    private readonly DispatcherTimer _animationTimer;
    private readonly Ellipse _cursorGlow;
    private readonly Ellipse _cursorDot;
    private readonly TextBlock _responseTextBlock;
    private readonly StackPanel _waveformPanel;
    private readonly ProgressRing _spinner;

    private System.Drawing.Point _targetCursorPosition;
    private CompanionVoiceState _voiceState = CompanionVoiceState.Idle;

    // The cursor-following dot size (matching macOS 24px diameter)
    private const double CursorDotSize = 24;
    private const double CursorGlowSize = 48;

    public OverlayCanvas()
    {
        Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)); // Fully transparent

        // Blue glow ring behind the cursor dot
        _cursorGlow = new Ellipse
        {
            Width = CursorGlowSize,
            Height = CursorGlowSize,
            Fill = new SolidColorBrush(Color.FromArgb(60, 51, 128, 255)), // DS.OverlayCursorBlue 23% opacity
            IsHitTestVisible = false
        };

        // The blue cursor dot
        _cursorDot = new Ellipse
        {
            Width = CursorDotSize,
            Height = CursorDotSize,
            Fill = new SolidColorBrush(Color.FromArgb(255, 51, 128, 255)), // DS.OverlayCursorBlue
            IsHitTestVisible = false
        };

        // Response text bubble
        _responseTextBlock = new TextBlock
        {
            MaxWidth = 400,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 16,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };

        // Waveform (5 vertical bars)
        _waveformPanel = BuildWaveformPanel();

        // Processing spinner
        _spinner = new ProgressRing
        {
            Width = 32, Height = 32,
            IsActive = false,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 51, 128, 255)),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };

        Children.Add(_cursorGlow);
        Children.Add(_cursorDot);
        Children.Add(_waveformPanel);
        Children.Add(_spinner);
        Children.Add(_responseTextBlock);

        // 60fps animation loop — updates cursor position and animations
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps
        };
        _animationTimer.Tick += OnAnimationFrame;
        _animationTimer.Start();
    }

    private void OnAnimationFrame(object? sender, object e)
    {
        // Move cursor elements to match the tracked mouse position
        // The overlay window covers the full screen, so the canvas position
        // equals the screen position directly.
        double x = _targetCursorPosition.X;
        double y = _targetCursorPosition.Y;

        Canvas.SetLeft(_cursorDot, x - CursorDotSize / 2);
        Canvas.SetTop(_cursorDot, y - CursorDotSize / 2);
        Canvas.SetLeft(_cursorGlow, x - CursorGlowSize / 2);
        Canvas.SetTop(_cursorGlow, y - CursorGlowSize / 2);

        // Offset text bubble to the right of the cursor
        Canvas.SetLeft(_responseTextBlock, x + 32);
        Canvas.SetTop(_responseTextBlock, y - 40);

        Canvas.SetLeft(_waveformPanel, x + 32);
        Canvas.SetTop(_waveformPanel, y - 20);

        Canvas.SetLeft(_spinner, x + 32);
        Canvas.SetTop(_spinner, y - 16);
    }

    public void Refresh(CompanionVoiceState state, string text,
        double audioPower, System.Drawing.Point cursorPos)
    {
        _voiceState = state;
        _targetCursorPosition = cursorPos;

        // Show/hide elements based on voice state
        _responseTextBlock.Visibility =
            state == CompanionVoiceState.Responding && !string.IsNullOrEmpty(text)
            ? Visibility.Visible : Visibility.Collapsed;

        _waveformPanel.Visibility =
            state == CompanionVoiceState.Listening
            ? Visibility.Visible : Visibility.Collapsed;

        _spinner.IsActive = state == CompanionVoiceState.Processing;
        _spinner.Visibility =
            state == CompanionVoiceState.Processing
            ? Visibility.Visible : Visibility.Collapsed;
    }

    public void UpdateCursorPosition(System.Drawing.Point pos)
        => _targetCursorPosition = pos;

    public void UpdateResponseText(string text)
        => _responseTextBlock.Text = text;

    public void UpdateWaveform(double powerLevel)
    {
        // Update waveform bar heights based on current power level
        foreach (var child in _waveformPanel.Children.OfType<Rectangle>())
        {
            child.Height = Math.Max(4, powerLevel * 40 + Random.Shared.NextDouble() * 8);
        }
    }

    private static StackPanel BuildWaveformPanel()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 3,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };

        for (int i = 0; i < 5; i++)
        {
            panel.Children.Add(new Rectangle
            {
                Width = 4,
                Height = 4,
                RadiusX = 2, RadiusY = 2,
                Fill = new SolidColorBrush(Color.FromArgb(255, 51, 128, 255)),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        return panel;
    }
}
