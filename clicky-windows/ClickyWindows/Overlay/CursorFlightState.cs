// Overlay/CursorFlightState.cs
// Tracks the state of an in-progress cursor flight animation.
// Used by BezierFlightAnimator to know where the cursor is at each frame.

namespace ClickyWindows;

internal enum CursorFlightPhase
{
    /// <summary>No animation running. Cursor follows mouse.</summary>
    Idle,
    /// <summary>Flying from current position toward target.</summary>
    FlyingToTarget,
    /// <summary>Arrived at target. Cursor is "pointing" at the element.</summary>
    PointingAtTarget,
    /// <summary>Returning along the same arc back to mouse position.</summary>
    FlyingBack
}

internal class CursorFlightState
{
    public CursorFlightPhase Phase { get; set; } = CursorFlightPhase.Idle;

    /// <summary>Start position of the current flight (screen coordinates).</summary>
    public System.Drawing.PointF StartPosition { get; set; }

    /// <summary>Target position (absolute screen coordinates after coordinate mapping).</summary>
    public System.Drawing.PointF TargetPosition { get; set; }

    /// <summary>Bezier control point for the arc. Positioned above the midpoint.</summary>
    public System.Drawing.PointF ControlPoint { get; set; }

    /// <summary>Progress through the current flight phase, 0.0 to 1.0.</summary>
    public double Progress { get; set; } = 0;

    /// <summary>Human-readable label from the POINT tag, shown at the target.</summary>
    public string TargetLabel { get; set; } = "";

    /// <summary>
    /// Current interpolated position along the bezier curve.
    /// Updated each frame by BezierFlightAnimator.
    /// </summary>
    public System.Drawing.PointF CurrentPosition { get; set; }
}
