using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DateTime> ktisisPillarClocks = [];
    private DungeonAmbientAoe? ktisisSlice;
    private int ktisisSliceCasts;
    private readonly List<uint> ktisisHeads = [];
    private readonly HashSet<uint> ktisisHeadSeen = [];
    private readonly List<DungeonAmbientAoe> ktisisBreaths = [];
    private readonly Dictionary<(ulong Source, uint Action, DateTime Start), DungeonPendingCast> ktisisSeenCasts = [];
    private DungeonAmbientAoe? ktisisRing;
    private bool ktisisSmall;
    private readonly List<(ulong Source, DungeonAmbientAoe Shadow)> ktisisLos = [];
    private readonly List<(ulong Target, DateTime Activation)> ktisisAeroBaits = [];
    private readonly List<(ulong Target, DateTime Activation)> ktisisTornadoBaits = [];
    private readonly Dictionary<ulong, long> ktisisWindOrder = [];
    private long ktisisSerial;

    public DungeonActor? KtisisInterruptTarget(uint job)
    {
        if (territory != KtisisCatalog.TERRITORY || Active?.BossOid != 0x348A || KtisisCatalog.InterruptAction(job) is not (7538 or 7551)
            || !_casts.Values.Any(c => c.Rule.Action == 25907) || !_actors.TryGetValue(PrimaryId, out var boss)
            || boss.Dead || !boss.Targetable || !_actors.TryGetValue(PlayerId, out var player)) return null;
        var range = KtisisCatalog.InterruptAction(job) == 7538 ? 3 : 25;
        var delta = player.Position - boss.Position; delta.Y = 0;
        return delta.Length() <= range + player.HitboxRadius + boss.HitboxRadius ? boss : null;
    }

    private void SynchronizeKtisis()
    {
        if (territory != KtisisCatalog.TERRITORY) return;
        if (Active?.BossOid == 0x3425 && _actors.TryGetValue(PrimaryId, out var boss))
        {
            // Ordered status slots restore head order when starting from a world snapshot.
            foreach (var status in boss.Statuses ?? [])
                if (HasStatus(boss, status)) KtisisStatus(boss.Id, status, true);
            ktisisHeadSeen.RemoveWhere(s => !HasStatus(boss, s));
        }
        ktisisTornadoBaits.RemoveAll(b => !_actors.TryGetValue(b.Target, out var target) || target.Dead);
    }

    private void KtisisStatus(ulong target, uint status, bool present)
    {
        if (territory != KtisisCatalog.TERRITORY || Active?.BossOid != 0x3425 || target != PrimaryId || status is < 2812 or > 2814) return;
        if (present) { if (ktisisHeadSeen.Add(status)) ktisisHeads.Add(status); }
        else ktisisHeadSeen.Remove(status);
    }

    private void KtisisCast(DungeonPendingCast cast)
    {
        if (territory != KtisisCatalog.TERRITORY || Arena == null) return;
        if (cast.Rule.Action == 25180)
            foreach (var pillar in _casts.Values.Where(c => c.Rule.Action == 25375)) ktisisPillarClocks[pillar.Source] = cast.FinishAt;
        var key = (cast.Source, cast.Rule.Action, cast.StartedAt);
        var prefix = $"ktisis/breath/{cast.Source}/{cast.StartedAt.Ticks}/";
        var losId = $"ktisis/los/{cast.Source}/{cast.StartedAt.Ticks}";
        if (ktisisSeenCasts.TryGetValue(key, out var previous))
        {
            // A later packet can correct the snapshot already recovered from actor scanning.
            for (var i = 0; i < ktisisBreaths.Count; ++i)
                if (ktisisBreaths[i].Id.StartsWith(prefix, StringComparison.Ordinal))
                    ktisisBreaths[i] = ktisisBreaths[i] with { Position = cast.Location,
                        Rotation = ktisisBreaths[i].Rotation + cast.Rotation - previous.Rotation,
                        Activation = ktisisBreaths[i].Activation + (cast.FinishAt - previous.FinishAt) };
            for (var i = 0; i < ktisisLos.Count; ++i)
                if (ktisisLos[i].Shadow.Id == losId)
                {
                    var shadow = ktisisLos[i].Shadow;
                    var oldDirection = new Vector3(MathF.Sin(previous.Rotation), 0, MathF.Cos(previous.Rotation));
                    var direction = new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation));
                    ktisisLos[i] = (cast.Source, shadow with { Position = shadow.Position + (direction - oldDirection) * .2f,
                        Rotation = cast.Rotation, Activation = cast.FinishAt });
                }
            ktisisSeenCasts[key] = cast;
            return;
        }
        ktisisSeenCasts[key] = cast;
        if (cast.Rule.Action is >= 25734 and <= 25736)
        {
            ktisisBreaths.Add(new(prefix + "first", "头部吐息", cast.Rule.Shape!, cast.Location, cast.Rotation, Activation: cast.FinishAt));
            if (ktisisHeads.Count == 2)
            {
                var first = ktisisHeads[0]; var second = ktisisHeads[1]; var angle = MathF.Tau / 3;
                var offset = second switch { 2814 => first == 2812 ? -angle : -2 * angle,
                    2813 => first == 2812 ? angle : 2 * angle, 2812 => first == 2813 ? -angle : angle, _ => 0 };
                ktisisBreaths.Add(new(prefix + "second", "第二头吐息", cast.Rule.Shape!, cast.Location,
                    cast.Rotation + offset, Activation: cast.FinishAt.AddSeconds(2.1)));
            }
        }
        if (cast.Rule.Action == 25886 && !ktisisSmall)
            ktisisRing = new("ktisis/ring", "场地即将缩小", new DonutTemplate(20, 22), Arena.Position, Activation: cast.FinishAt.AddSeconds(.5));
        if (cast.Rule.Action == 27837) ktisisWindOrder[cast.Source] = ++ktisisSerial;
        if (cast.Rule.Action == 27836 && _actors.Values.FirstOrDefault(a => a.Oid == 0x348C && a.AnimationState2 != 1) is { } meteor)
        {
            var direction = new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation));
            ktisisLos.Add((cast.Source, new(losId, "陨石背后安全直线",
                RayRectangleTemplate.Symmetric(meteor.HitboxRadius * 2, 49.8f), meteor.Position + direction * .2f,
                cast.Rotation, Navigate: false, Safe: true, Activation: cast.FinishAt)));
        }
    }

    private void KtisisFinish(DungeonPendingCast cast)
    {
        if (territory != KtisisCatalog.TERRITORY) return;
        if (cast.Rule.Action == 25177) { ++ktisisSliceCasts; ktisisSlice = null; }
        if (cast.Rule.Action == 25375) ktisisPillarClocks.Remove(cast.Source);
        if (cast.Rule.Action is >= 25734 and <= 25736 && ktisisBreaths.Count > 0 && ktisisHeads.Count > 0)
        {
            ktisisBreaths.RemoveAt(0); ktisisHeads.RemoveAt(0);
        }
        if (cast.Rule.Action == 27836 && ktisisLos.Count > 0) ktisisLos.RemoveAt(0);
        if (cast.Rule.Action == 27837) ktisisWindOrder.Remove(cast.Source);
    }

    private void KtisisMap(uint index, uint state, DateTime time)
    {
        if (territory != KtisisCatalog.TERRITORY || Arena == null) return;
        if (Active?.BossOid == 0x348A && index == 8 && state == 0x00020001)
        {
            ktisisSmall = true; ktisisRing = null; Arena = KtisisArenas.HermesSmall(Arena.Position.Y);
        }
        if (Active?.BossOid != 0x3323) return;
        (float X, float Z, float Degrees)? pair = (index, state) switch
        {
            (0, 0x00200010) => (-154.825f, 42.75f, 60), (0, 0x01000080) => (-154.825f, 55.25f, 119.997f),
            (0, 0x00020001) => (-144, 36.5f, -.003f), (1, 0x00200010) => (-144, 61.5f, -180),
            (1, 0x01000080) => (-133.175f, 55.25f, -120.003f), (1, 0x00020001) => (-133.175f, 42.75f, -60.005f),
            _ => null,
        };
        if (pair is { } p) ktisisSlice = new("ktisis/slice", "循迹寻找首领", RayRectangleTemplate.Symmetric(50, 50),
            QuantizedDungeonPosition(new(p.X, Arena.Position.Y, p.Z)), p.Degrees * MathF.PI / 180,
            Activation: time.AddSeconds(ktisisSliceCasts == 0 ? 13 : 16));
    }

    private void KtisisEffect(uint action, ulong target, DateTime time)
    {
        if (territory != KtisisCatalog.TERRITORY) return;
        if (action is >= 25737 and <= 25739) { ktisisBreaths.Clear(); ktisisHeads.Clear(); }
        if (action == 25887) ktisisAeroBaits.Add((target, time.AddSeconds(5.7)));
        if (action == 25900) ktisisAeroBaits.Clear();
        if (action == 25905 && ktisisTornadoBaits.Count > 0) ktisisTornadoBaits.RemoveAt(0);
    }

    private void KtisisIcon(ulong target, uint icon, DateTime time)
    {
        if (territory == KtisisCatalog.TERRITORY && Active?.BossOid == 0x348A && icon == 218)
            ktisisTornadoBaits.Add((target, time.AddSeconds(5.1)));
    }

    private void ResetKtisis()
    {
        ktisisPillarClocks.Clear(); ktisisSlice = null; ktisisSliceCasts = 0; ktisisHeads.Clear(); ktisisHeadSeen.Clear();
        ktisisBreaths.Clear(); ktisisSeenCasts.Clear(); ktisisRing = null; ktisisSmall = false; ktisisLos.Clear();
        ktisisAeroBaits.Clear(); ktisisTornadoBaits.Clear(); ktisisWindOrder.Clear(); ktisisSerial = 0;
    }
}
