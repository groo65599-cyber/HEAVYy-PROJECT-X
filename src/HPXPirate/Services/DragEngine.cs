using HPXPirate.Models;

namespace HPXPirate.Services;

/// <summary>Shared response-curve maths for Drag Assist and Easy Drag.</summary>
public static class Curves
{
    public static double Evaluate(ResponseCurve curve, double x, double[]? custom = null)
    {
        x = Math.Clamp(x, 0, 1);
        return curve switch
        {
            ResponseCurve.Linear => x,
            ResponseCurve.Smooth => x * x * (3 - 2 * x),
            ResponseCurve.FastStart => 1 - (1 - x) * (1 - x),
            ResponseCurve.SlowStart => x * x,
            ResponseCurve.Precision => Math.Pow(x, 1.5),
            ResponseCurve.Custom => Piecewise(custom, x),
            _ => x
        };
    }

    private static double Piecewise(double[]? pts, double x)
    {
        if (pts is null || pts.Length < 2) return x;
        int n = pts.Length;
        double step = 1.0 / (n - 1);
        int i = Math.Min(n - 2, Math.Max(0, (int)(x / step)));
        double t = (x - i * step) / step;
        return pts[i] * (1 - t) + pts[i + 1] * t;
    }
}

public readonly record struct DragSample(
    double RawDx, double RawDy,
    double OutDx, double OutDy,
    double RawMagnitude, double OutMagnitude,
    double Speed,
    double StabilityScore, double SmoothnessScore);

/// <summary>
/// Stateful mouse-input processor. It only reshapes the user's own mouse movement
/// (curve, smoothing, stabilizer, deadzone, multipliers). It never reads the game,
/// never targets anything, and never moves the cursor without user input.
/// </summary>
public sealed class DragProcessor
{
    private double _prevOutX, _prevOutY;
    private double _prevRawX, _prevRawY;
    private double _smoothedX, _smoothedY;
    private double _stabilityAccum = 1.0;
    private double _smoothAccum = 1.0;
    private int _samples;

    public double StabilityScore { get; private set; } = 100;
    public double SmoothnessScore { get; private set; } = 100;
    public double TotalDistance { get; private set; }
    public double LastSpeed { get; private set; }

    public void Reset()
    {
        _prevOutX = _prevOutY = _prevRawX = _prevRawY = 0;
        _smoothedX = _smoothedY = 0;
        _stabilityAccum = 1.0;
        _smoothAccum = 1.0;
        _samples = 0;
        StabilityScore = SmoothnessScore = 100;
        TotalDistance = 0;
        LastSpeed = 0;
    }

    public DragSample Process(double dx, double dy, EasyDragSettings s, double dtSeconds = 0.016)
    {
        double rawMag = Math.Sqrt(dx * dx + dy * dy);

        // 1. Deadzone — ignore micro jitter below threshold
        double deadzone = s.InputDeadzone;
        if (rawMag < deadzone)
            return new DragSample(dx, dy, 0, 0, rawMag, 0, 0, StabilityScore, SmoothnessScore);

        // 2. Normalise magnitude and apply response curve
        double maxSpeed = Math.Max(1, s.MaxDragSpeed);
        double norm = Math.Min(1.0, rawMag / maxSpeed);
        double curved = Curves.Evaluate(s.DragResponseCurve, norm, s.CustomCurvePoints);
        double gain = rawMag > 0 ? curved / norm : 1.0;
        if (double.IsNaN(gain) || double.IsInfinity(gain)) gain = 1.0;

        double outX = dx * gain;
        double outY = dy * gain;

        // 3. Sensitivity + weight + axis multipliers
        double hMul = s.HorizontalDragMultiplier * s.DragSensitivity / Math.Max(0.1, s.DragWeight);
        double vMul = s.VerticalDragMultiplier * s.DragSensitivity / Math.Max(0.1, s.DragWeight);
        outX *= hMul;
        outY *= vMul;

        // 4. Initial drag response (boost the first movement after rest)
        if (rawMag > 0 && Math.Sqrt(_prevRawX * _prevRawX + _prevRawY * _prevRawY) < 0.5)
        {
            outX *= s.InitialDragResponse;
            outY *= s.InitialDragResponse;
        }

        // 5. Acceleration (velocity-dependent gain)
        if (s.DragAcceleration > 0)
        {
            double accelGain = 1.0 + s.DragAcceleration * norm;
            outX *= accelGain;
            outY *= accelGain;
        }

        // 6. Smoothing (EMA). Higher smoothing = smoother but slightly delayed.
        double smooth = Math.Clamp(s.DragSmoothness + s.InputSmoothing, 0, 0.95);
        _smoothedX = _smoothedX * smooth + outX * (1 - smooth);
        _smoothedY = _smoothedY * smooth + outY * (1 - smooth);
        outX = _smoothedX;
        outY = _smoothedY;

        // 7. Micro-movement stabilizer
        double stab = s.MicroMovementStabilizer switch
        {
            StabilizerLevel.Off => 0.0,
            StabilizerLevel.Low => 0.15,
            StabilizerLevel.Medium => 0.30,
            StabilizerLevel.High => 0.50,
            _ => 0.0
        };
        if (stab > 0 && rawMag < maxSpeed * 0.15)
        {
            outX *= (1 - stab);
            outY *= (1 - stab);
        }

        double outMag = Math.Sqrt(outX * outX + outY * outY);

        // ---- Metrics (measured, not invented) ----
        double speed = dtSeconds > 0 ? rawMag / dtSeconds : rawMag;
        LastSpeed = speed;
        TotalDistance += outMag;

        // Stability: how consistent the direction is (low jitter => high score)
        double jitter = Math.Abs(outMag - Math.Sqrt(_prevOutX * _prevOutX + _prevOutY * _prevOutY));
        double jitterNorm = Math.Min(1.0, jitter / Math.Max(1.0, maxSpeed * 0.5));
        _stabilityAccum = _stabilityAccum * 0.9 + (1 - jitterNorm) * 0.1;

        // Smoothness: how small the frame-to-frame change is
        double delta = Math.Sqrt((outX - _prevOutX) * (outX - _prevOutX) + (outY - _prevOutY) * (outY - _prevOutY));
        double deltaNorm = Math.Min(1.0, delta / Math.Max(1.0, maxSpeed * 0.25));
        _smoothAccum = _smoothAccum * 0.9 + (1 - deltaNorm) * 0.1;

        _samples++;
        StabilityScore = Math.Clamp(_stabilityAccum * 100, 0, 100);
        SmoothnessScore = Math.Clamp(_smoothAccum * 100, 0, 100);

        _prevOutX = outX; _prevOutY = outY;
        _prevRawX = dx; _prevRawY = dy;

        return new DragSample(dx, dy, outX, outY, rawMag, outMag, speed, StabilityScore, SmoothnessScore);
    }
}

/// <summary>HPX EASY DRAG — makes mouse drag feel lighter, smoother and more consistent.</summary>
public sealed class EasyDragEngine
{
    private readonly DragProcessor _processor = new();
    public DragProcessor Processor => _processor;

    public static EasyDragSettings ApplyPreset(EasyDragSettings s, EasyDragPreset preset)
    {
        s.Preset = preset;
        switch (preset)
        {
            case EasyDragPreset.Light:
                s.DragWeight = 0.8; s.DragSensitivity = 1.15; s.DragStability = 0.3;
                s.DragSmoothness = 0.2; s.DragAcceleration = 0.15; s.InitialDragResponse = 1.2;
                s.DragResponseCurve = ResponseCurve.FastStart; s.MicroMovementStabilizer = StabilizerLevel.Off;
                s.InputSmoothing = 0.15; s.InputDeadzone = 0; s.MaxDragSpeed = 2500;
                break;
            case EasyDragPreset.Balanced:
                s.DragWeight = 1.0; s.DragSensitivity = 1.0; s.DragStability = 0.5;
                s.DragSmoothness = 0.35; s.DragAcceleration = 0.0; s.InitialDragResponse = 1.0;
                s.DragResponseCurve = ResponseCurve.Smooth; s.MicroMovementStabilizer = StabilizerLevel.Low;
                s.InputSmoothing = 0.3; s.InputDeadzone = 0; s.MaxDragSpeed = 2000;
                break;
            case EasyDragPreset.Stable:
                s.DragWeight = 1.15; s.DragSensitivity = 0.95; s.DragStability = 0.8;
                s.DragSmoothness = 0.5; s.DragAcceleration = 0.0; s.InitialDragResponse = 0.95;
                s.DragResponseCurve = ResponseCurve.SlowStart; s.MicroMovementStabilizer = StabilizerLevel.Medium;
                s.InputSmoothing = 0.45; s.InputDeadzone = 0.5; s.MaxDragSpeed = 1800;
                break;
            case EasyDragPreset.FastDrag:
                s.DragWeight = 0.9; s.DragSensitivity = 1.1; s.DragStability = 0.4;
                s.DragSmoothness = 0.25; s.DragAcceleration = 0.3; s.InitialDragResponse = 1.35;
                s.DragResponseCurve = ResponseCurve.FastStart; s.MicroMovementStabilizer = StabilizerLevel.Low;
                s.InputSmoothing = 0.2; s.InputDeadzone = 0; s.MaxDragSpeed = 3000;
                break;
            case EasyDragPreset.Precision:
                s.DragWeight = 1.25; s.DragSensitivity = 0.85; s.DragStability = 0.85;
                s.DragSmoothness = 0.55; s.DragAcceleration = 0.0; s.InitialDragResponse = 0.9;
                s.DragResponseCurve = ResponseCurve.Precision; s.MicroMovementStabilizer = StabilizerLevel.High;
                s.InputSmoothing = 0.5; s.InputDeadzone = 0.75; s.MaxDragSpeed = 1500;
                break;
            case EasyDragPreset.Custom:
            default:
                break;
        }
        return s;
    }

    public static EasyDragSettings ApplyAimFeel(EasyDragSettings s, AimFeel feel)
    {
        s.AimFeel = feel;
        switch (feel)
        {
            case AimFeel.Light: s.DragWeight = 0.8; s.DragSmoothness = 0.2; s.DragResponseCurve = ResponseCurve.FastStart; break;
            case AimFeel.Smooth: s.DragWeight = 1.0; s.DragSmoothness = 0.4; s.DragResponseCurve = ResponseCurve.Smooth; break;
            case AimFeel.Stable: s.DragWeight = 1.15; s.DragSmoothness = 0.5; s.MicroMovementStabilizer = StabilizerLevel.Medium; break;
            case AimFeel.Responsive: s.DragWeight = 0.95; s.DragSmoothness = 0.25; s.DragAcceleration = 0.2; s.InitialDragResponse = 1.2; break;
            case AimFeel.Precision: s.DragWeight = 1.25; s.DragSmoothness = 0.55; s.MicroMovementStabilizer = StabilizerLevel.High; s.DragResponseCurve = ResponseCurve.Precision; break;
        }
        return s;
    }

    public DragSample Process(double dx, double dy, EasyDragSettings s, double dt = 0.016)
        => _processor.Process(dx, dy, s, dt);

    public void Reset() => _processor.Reset();
}

/// <summary>HPX DRAG ASSIST — lighter, more consistent drag for the user's own mouse.</summary>
public sealed class DragAssistEngine
{
    private readonly DragProcessor _processor = new();
    public DragProcessor Processor => _processor;

    public DragSample Process(double dx, double dy, DragAssistSettings s, double dt = 0.016)
    {
        // Map DragAssistSettings onto the shared processor.
        var mapped = new EasyDragSettings
        {
            DragSensitivity = s.DragSensitivity,
            DragSmoothness = s.DragSmoothing,
            DragAcceleration = s.DragAcceleration,
            VerticalDragMultiplier = s.VerticalMultiplier,
            HorizontalDragMultiplier = s.HorizontalMultiplier,
            InitialDragResponse = s.StartZoneSensitivity,
            DragResponseCurve = s.ResponseCurve,
            MicroMovementStabilizer = StabilizerLevel.Low,
            InputSmoothing = s.DragSmoothing,
            InputDeadzone = 0,
            MaxDragSpeed = Math.Max(1, s.DragSpeed * 2000),
            CustomCurvePoints = s.CustomCurvePoints
        };
        return _processor.Process(dx, dy, mapped, dt);
    }

    public void Reset() => _processor.Reset();
}
