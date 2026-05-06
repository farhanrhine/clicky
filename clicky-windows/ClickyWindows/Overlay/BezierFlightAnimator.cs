// Overlay/BezierFlightAnimator.cs
// Drives the cursor flight animation using a quadratic bezier arc.
// On each 60fps tick:
//   1. Advances Progress by the flight speed
//   2. Computes the bezier position for that progress value
//   3. Applies easing so the cursor accelerates then decelerates
//
// The flight arc is computed once when the animation starts — the control
// point is placed 30% above the midpoint of start→target, creating a
// gentle upward arc that feels natural, not mechanical.
//
// Port of the cursor flight animation in OverlayWindow.swift.

using System;

namespace ClickyWindows;

internal class BezierFlightAnimator
{
    // Time for one-way flight (outbound or return), in seconds.
    // Matches the ~0.6s flight duration in OverlayWindow.swift.
    private const double FlightDurationSeconds = 0.6;

    // How long to pause at the target before starting the return flight (seconds)
    private const double DwellDurationSeconds = 2.0;

    // Speed: progress per frame at 60fps
    private static readonly double FrameProgressIncrement
        = 1.0 / (FlightDurationSeconds * 60);

    private readonly CursorFlightState _flightState = new();
    private double _dwellElapsedSeconds = 0;
    private readonly System.Diagnostics.Stopwatch _dwellTimer = new();

    public CursorFlightState FlightState => _flightState;

    public event Action<System.Drawing.PointF>? OnCursorPositionUpdated;
    public event Action? OnFlightCompleted; // fires when cursor returns to idle

    /// <summary>
    /// Starts a new flight animation from currentPosition to targetPosition.
    /// Cancels any in-progress flight.
    /// </summary>
    public void StartFlight(
        System.Drawing.PointF currentPosition,
        System.Drawing.PointF targetPosition,
        string targetLabel)
    {
        _flightState.Phase = CursorFlightPhase.FlyingToTarget;
        _flightState.StartPosition = currentPosition;
        _flightState.TargetPosition = targetPosition;
        _flightState.TargetLabel = targetLabel;
        _flightState.Progress = 0;
        _flightState.CurrentPosition = currentPosition;

        // Control point: above the midpoint, perpendicular to the flight axis.
        // Arc height scales with distance so short flights have subtle arcs.
        float midX = (currentPosition.X + targetPosition.X) / 2f;
        float midY = (currentPosition.Y + targetPosition.Y) / 2f;
        float distance = MathF.Sqrt(
            MathF.Pow(targetPosition.X - currentPosition.X, 2) +
            MathF.Pow(targetPosition.Y - currentPosition.Y, 2));
        float arcHeight = Math.Min(distance * 0.3f, 120f); // max 120px arc

        _flightState.ControlPoint = new System.Drawing.PointF(midX, midY - arcHeight);
    }

    /// <summary>
    /// Call once per animation frame (60fps). Updates flight state and fires events.
    /// Returns true while animation is running, false when complete.
    /// </summary>
    public bool Tick(double deltaSeconds)
    {
        switch (_flightState.Phase)
        {
            case CursorFlightPhase.FlyingToTarget:
                return TickFlyingToTarget();

            case CursorFlightPhase.PointingAtTarget:
                return TickDwelling(deltaSeconds);

            case CursorFlightPhase.FlyingBack:
                return TickFlyingBack();

            default:
                return false;
        }
    }

    private bool TickFlyingToTarget()
    {
        _flightState.Progress = Math.Min(
            _flightState.Progress + FrameProgressIncrement, 1.0);

        _flightState.CurrentPosition = EvaluateBezier(
            _flightState.StartPosition,
            _flightState.ControlPoint,
            _flightState.TargetPosition,
            EaseInOut(_flightState.Progress));

        OnCursorPositionUpdated?.Invoke(_flightState.CurrentPosition);

        if (_flightState.Progress >= 1.0)
        {
            _flightState.Phase = CursorFlightPhase.PointingAtTarget;
            _dwellElapsedSeconds = 0;
            _dwellTimer.Restart();
        }
        return true;
    }

    private bool TickDwelling(double deltaSeconds)
    {
        _dwellElapsedSeconds += deltaSeconds;
        if (_dwellElapsedSeconds >= DwellDurationSeconds)
        {
            // Start return flight (swap start and target)
            _flightState.Phase = CursorFlightPhase.FlyingBack;
            _flightState.Progress = 0;
        }
        return true;
    }

    private bool TickFlyingBack()
    {
        _flightState.Progress = Math.Min(
            _flightState.Progress + FrameProgressIncrement, 1.0);

        // Fly back along the same arc (control point mirrors the same curve)
        _flightState.CurrentPosition = EvaluateBezier(
            _flightState.TargetPosition,
            _flightState.ControlPoint,
            _flightState.StartPosition,
            EaseInOut(_flightState.Progress));

        OnCursorPositionUpdated?.Invoke(_flightState.CurrentPosition);

        if (_flightState.Progress >= 1.0)
        {
            _flightState.Phase = CursorFlightPhase.Idle;
            OnFlightCompleted?.Invoke();
            return false;
        }
        return true;
    }

    /// <summary>Quadratic bezier: B(t) = (1-t)²P0 + 2(1-t)tP1 + t²P2</summary>
    private static System.Drawing.PointF EvaluateBezier(
        System.Drawing.PointF p0,
        System.Drawing.PointF p1,
        System.Drawing.PointF p2,
        double t)
    {
        float mt = (float)(1 - t);
        float x = mt * mt * p0.X + 2 * mt * (float)t * p1.X + (float)(t * t) * p2.X;
        float y = mt * mt * p0.Y + 2 * mt * (float)t * p1.Y + (float)(t * t) * p2.Y;
        return new System.Drawing.PointF(x, y);
    }

    /// <summary>Smoothstep easing: slow start, fast middle, slow end.</summary>
    private static double EaseInOut(double t)
        => t * t * (3 - 2 * t);

    public void Cancel()
    {
        _flightState.Phase = CursorFlightPhase.Idle;
    }
}
