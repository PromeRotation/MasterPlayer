using BossMod;

namespace MasterPlayer.BmrRuntime;

public sealed record BmrDrawCaptureOptions
{
    public bool ActorMarkers { get; init; } = true;
    public bool CardinalNames { get; init; } = true;
    public bool SignMarkers { get; init; } = true;
}

public abstract record BmrDrawCommand(uint Color);

public sealed record BmrDrawZoneCircle(WPos Center, float Radius, uint Color, bool Outline = false, float Thickness = 1f) : BmrDrawCommand(Color);
public sealed record BmrDrawZoneDonut(WPos Center, float InnerRadius, float OuterRadius, uint Color, bool Outline = false, float Thickness = 1f) : BmrDrawCommand(Color);
public sealed record BmrDrawZoneCone(WPos Center, float InnerRadius, float OuterRadius, Angle Direction, Angle HalfAngle, uint Color, bool Outline = false, float Thickness = 1f) : BmrDrawCommand(Color);
public sealed record BmrDrawZoneRect(WPos Origin, WDir Direction, float LenFront, float LenBack, float HalfWidth, uint Color, bool Outline = false, float Thickness = 1f) : BmrDrawCommand(Color);
public sealed record BmrDrawZoneCapsule(WPos Start, WDir Direction, float Radius, float Length, uint Color, bool Outline = false, float Thickness = 1f) : BmrDrawCommand(Color);
public sealed record BmrDrawZoneArcCapsule(WPos Start, WPos OrbitCenter, Angle AngularLength, float Radius, uint Color, bool Outline = false, float Thickness = 1f) : BmrDrawCommand(Color);
public sealed record BmrDrawZoneCross(WPos Origin, Angle Rotation, float Range, float HalfWidth, uint Color, bool Outline = false, float Thickness = 1f) : BmrDrawCommand(Color);
public sealed record BmrDrawZoneTriangles(WPos[] Vertices, uint Color) : BmrDrawCommand(Color);
public sealed record BmrDrawPolygonPart(WPos[] Exterior, WPos[][] Holes);
public sealed record BmrDrawComplexPoly(int Key, BmrDrawPolygonPart[] Parts, uint Color, bool Outline = false, float Thickness = 1f) : BmrDrawCommand(Color);
public sealed record BmrDrawLine(WPos A, WPos B, uint Color, float Thickness = 1f) : BmrDrawCommand(Color);
public sealed record BmrDrawPolyline(WPos[] Vertices, bool Closed, uint Color, float Thickness = 1f) : BmrDrawCommand(Color);
public sealed record BmrDrawTriangle(WPos A, WPos B, WPos C, uint Color, bool Filled = false, float Thickness = 1f) : BmrDrawCommand(Color);
public sealed record BmrDrawQuad(WPos A, WPos B, WPos C, WPos D, uint Color, float Thickness = 1f) : BmrDrawCommand(Color);
public sealed record BmrDrawTextWorld(WPos Position, string Text, uint Color, float FontSize) : BmrDrawCommand(Color);
public sealed record BmrDrawIconWorld(WPos Position, int Icon, uint Color, float FontSize) : BmrDrawCommand(Color);
public sealed record BmrDrawCardinalNames(uint ColorN, uint ColorE, uint ColorS, uint ColorW, float FontSize) : BmrDrawCommand(ColorN);
public sealed record BmrDrawEye(WPos Position, bool Danger, uint Color) : BmrDrawCommand(Color);
public sealed record BmrDrawSignMarker(WPos Position, uint IconId, Sign Sign) : BmrDrawCommand(0);
public sealed record BmrDrawActorMarker(WPos Position, Angle Rotation, uint Color, bool InsideBounds) : BmrDrawCommand(Color);

public sealed class BmrDrawSink(BmrDrawCaptureOptions options)
{
    public static BmrDrawSink? Current { get; private set; }
    public static IDisposable Install(BmrDrawSink sink)
    {
        var previous = Current;
        Current = sink;
        return new Subscription(() => Current = previous);
    }

    public BmrDrawCaptureOptions Options { get; } = options;
    public List<BmrDrawCommand> Commands { get; } = [];
    public bool Capturing => true;

    private readonly List<WPos> _path = [];

    public void Add(BmrDrawCommand command) => Commands.Add(command);

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
            Add(new BmrDrawPolyline([.. _path], closed, color, thickness));
        _path.Clear();
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        public void Dispose() => unsubscribe();
    }
}

public sealed record BmrDrawCaptureError(BossComponent? Component, string Stage, Exception Exception);
public sealed record BmrDrawCaptureResult(IReadOnlyList<BmrDrawCommand> Commands, IReadOnlyList<BmrDrawCaptureError> Errors);

public static class BmrModuleDrawCapture
{
    public static BmrDrawCaptureResult Capture(BossModule module, int pcSlot, Angle cameraAzimuth, BmrDrawCaptureOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(module);
        var pc = module.Raid[pcSlot] ?? throw new ArgumentException($"No player in party slot {pcSlot}.", nameof(pcSlot));
        var sink = new BmrDrawSink(options ?? new());
        var errors = new List<BmrDrawCaptureError>();
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
        using (BmrDrawSink.Install(sink))
        {
            try { module.DrawArenaCaptured(cameraAzimuth, pcSlot, pc, haveRisks, (component, stage, ex) => errors.Add(new(component, stage, ex))); }
            catch (Exception ex) { errors.Add(new(null, "arena", ex)); }
        }
        return new(sink.Commands, errors);
    }
}
