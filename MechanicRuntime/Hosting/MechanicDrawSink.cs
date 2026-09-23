using BossMod;

namespace MasterPlayer.MechanicRuntime;

public sealed record MechanicDrawCaptureOptions
{
    public bool ActorMarkers { get; init; } = true;
    public bool CardinalNames { get; init; } = true;
    public bool SignMarkers { get; init; } = true;
}

public abstract record MechanicDrawCommand(uint Color);

public sealed record MechanicDrawZoneCircle(WPos Center, float Radius, uint Color, bool Outline = false, float Thickness = 1f) : MechanicDrawCommand(Color);
public sealed record MechanicDrawZoneDonut(WPos Center, float InnerRadius, float OuterRadius, uint Color, bool Outline = false, float Thickness = 1f) : MechanicDrawCommand(Color);
public sealed record MechanicDrawZoneCone(WPos Center, float InnerRadius, float OuterRadius, Angle Direction, Angle HalfAngle, uint Color, bool Outline = false, float Thickness = 1f) : MechanicDrawCommand(Color);
public sealed record MechanicDrawZoneRect(WPos Origin, WDir Direction, float LenFront, float LenBack, float HalfWidth, uint Color, bool Outline = false, float Thickness = 1f) : MechanicDrawCommand(Color);
public sealed record MechanicDrawZoneCapsule(WPos Start, WDir Direction, float Radius, float Length, uint Color, bool Outline = false, float Thickness = 1f) : MechanicDrawCommand(Color);
public sealed record MechanicDrawZoneArcCapsule(WPos Start, WPos OrbitCenter, Angle AngularLength, float Radius, uint Color, bool Outline = false, float Thickness = 1f) : MechanicDrawCommand(Color);
public sealed record MechanicDrawZoneCross(WPos Origin, Angle Rotation, float Range, float HalfWidth, uint Color, bool Outline = false, float Thickness = 1f) : MechanicDrawCommand(Color);
public sealed record MechanicDrawZoneTriangles(WPos[] Vertices, uint Color) : MechanicDrawCommand(Color);
public sealed record MechanicDrawPolygonPart(WPos[] Exterior, WPos[][] Holes);
public sealed record MechanicDrawComplexPoly(int Key, MechanicDrawPolygonPart[] Parts, uint Color, bool Outline = false, float Thickness = 1f) : MechanicDrawCommand(Color);
public sealed record MechanicDrawLine(WPos A, WPos B, uint Color, float Thickness = 1f) : MechanicDrawCommand(Color);
public sealed record MechanicDrawPolyline(WPos[] Vertices, bool Closed, uint Color, float Thickness = 1f) : MechanicDrawCommand(Color);
public sealed record MechanicDrawTriangle(WPos A, WPos B, WPos C, uint Color, bool Filled = false, float Thickness = 1f) : MechanicDrawCommand(Color);
public sealed record MechanicDrawQuad(WPos A, WPos B, WPos C, WPos D, uint Color, float Thickness = 1f) : MechanicDrawCommand(Color);
public sealed record MechanicDrawTextWorld(WPos Position, string Text, uint Color, float FontSize) : MechanicDrawCommand(Color);
public sealed record MechanicDrawIconWorld(WPos Position, int Icon, uint Color, float FontSize) : MechanicDrawCommand(Color);
public sealed record MechanicDrawCardinalNames(uint ColorN, uint ColorE, uint ColorS, uint ColorW, float FontSize) : MechanicDrawCommand(ColorN);
public sealed record MechanicDrawEye(WPos Position, bool Danger, uint Color) : MechanicDrawCommand(Color);
public sealed record MechanicDrawSignMarker(WPos Position, uint IconId, Sign Sign) : MechanicDrawCommand(0);
public sealed record MechanicDrawActorMarker(WPos Position, Angle Rotation, uint Color, bool InsideBounds) : MechanicDrawCommand(Color);

public sealed class MechanicDrawSink(MechanicDrawCaptureOptions options)
{
    public static MechanicDrawSink? Current { get; private set; }
    public static IDisposable Install(MechanicDrawSink sink)
    {
        var previous = Current;
        Current = sink;
        return new Subscription(() => Current = previous);
    }

    public MechanicDrawCaptureOptions Options { get; } = options;
    public List<MechanicDrawCommand> Commands { get; } = [];
    public bool Capturing => true;

    private readonly List<WPos> _path = [];

    public void Add(MechanicDrawCommand command) => Commands.Add(command);

    public void PathPoint(WPos p)
    {
        if (_path.Count == 0 || _path[^1] != p)
            _path.Add(p);
    }

    public void PathArc(WPos center, float radius, float amin, float amax)
    {
        var segments = Math.Clamp((int)MathF.Ceiling(MathF.Abs(amax - amin) * 24f / MathF.PI), 2, 64);
        for (var i = 0; i <= segments; ++i)
            PathPoint(center + radius * new Angle(amin + (amax - amin) * i / segments).ToDirection());
    }

    public void StrokePath(bool closed, uint color, float thickness)
    {
        if (_path.Count > 1)
            Add(new MechanicDrawPolyline([.. _path], closed, color, thickness));
        _path.Clear();
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        public void Dispose() => unsubscribe();
    }
}

public sealed record MechanicDrawCaptureError(BossComponent? Component, string Stage, Exception Exception);
public sealed record MechanicDrawCaptureResult(IReadOnlyList<MechanicDrawCommand> Commands, IReadOnlyList<MechanicDrawCaptureError> Errors);

public static class MechanicModuleDrawCapture
{
    public static MechanicDrawCaptureResult Capture(BossModule module, int pcSlot, Angle cameraAzimuth, MechanicDrawCaptureOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(module);
        var pc = module.Raid[pcSlot] ?? throw new ArgumentException($"No player in party slot {pcSlot}.", nameof(pcSlot));
        var sink = new MechanicDrawSink(options ?? new());
        var errors = new List<MechanicDrawCaptureError>();
        var haveRisks = false;
        try
        {
            var hints = module.CalculateHintsForRaidMember(pcSlot, pc);
            var count = hints.Count;
            for (var i = 0; i < count; ++i)
            {
                if (hints[i].Item2)
                {
                    haveRisks = true;
                    break;
                }
            }
        }
        catch (Exception ex) { errors.Add(new(null, "hints", ex)); }
        using (MechanicDrawSink.Install(sink))
        {
            try { module.DrawArenaCaptured(cameraAzimuth, pcSlot, pc, haveRisks, (component, stage, ex) => errors.Add(new(component, stage, ex))); }
            catch (Exception ex) { errors.Add(new(null, "arena", ex)); }
        }
        return new(sink.Commands, errors);
    }
}
