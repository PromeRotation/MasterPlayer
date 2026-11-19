using System.Numerics;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

internal sealed class Pt60Encounter() : PtBossEncounter(60, 0x49DE)
{
    public override uint[] Actions { get; } = [44866, 44872, 44867, 44868, 44870, 44871, 44869, 44909, 44857, 44865, 44859, 44861, 44864, 44858, 44860, 44862, 44863];
    private readonly Dictionary<ulong, Cast> marchCasters = [];
    private readonly List<RotationSequence> rotations = [];
    private int distance;
    private bool? clockwise;
    private DateTime marchHit;
    private static readonly (int X, int Z)[] Inner = [(-1, -1), (1, -1), (1, 1), (-1, 1)];
    private static readonly (int X, int Z)[] Outer = [(-3, -3), (-1, -3), (1, -3), (3, -3), (3, -1), (3, 1), (3, 3), (1, 3), (-1, 3), (-3, 3), (-3, 1), (-3, -1)];

    protected override void OnCast(Cast cast)
    {
        switch (cast.Action)
        {
            case 44866 when cast.Rotation != null:
                Hit(cast, new FanTemplate(60, Rad(60)), "Spineshot");
                AddHit($"spineshot/{cast.Source}", "Spineshot 反向", new FanTemplate(60, Rad(60)), cast.Position,
                    cast.Rotation.Value + MathF.PI, cast.Hit, cast.Source);
                break;
            case 44867 or 44868 when cast.Rotation != null:
                rotations.Clear();
                // ARR 首发在包内读条结束后 0.35–0.40 秒；Hit 已含宿主的 0.3 秒。
                for (var i = 0; i < 2; ++i)
                    rotations.Add(new(cast.Source, cast.Position, cast.Rotation.Value + i * MathF.PI,
                        Rad(cast.Action == 44868 ? -60 : 60), cast.Hit.AddSeconds(.1)));
                break;
            case 44862 or 44863:
                distance = cast.Action == 44862 ? 1 : 2;
                InitMarch();
                break;
            case 44864 or 44865:
                marchCasters[cast.Source] = cast;
                InitMarch();
                break;
            case 44858 or 44859:
                Hit(cast, cast.Action == 44858 ? RayRectangleTemplate.Symmetric(10, 10) : RayRectangleTemplate.Symmetric(30, 30), "Needles");
                break;
            case 44860 or 44861:
                areas.Remove($"march/{cast.Source}");
                Hit(cast, cast.Action == 44860 ? RayRectangleTemplate.Symmetric(10, 10) : RayRectangleTemplate.Symmetric(30, 30), "March Needles 最终锁定");
                break;
        }
    }

    protected override void OnMap(uint index, uint state, DateTime now)
    {
        if (index != 0 || state is not (0x00020001 or 0x00200010)) return;
        clockwise = state == 0x00020001;
        marchHit = now.AddSeconds(16.2);
        InitMarch();
    }

    private void InitMarch()
    {
        if (marchCasters.Count != 8 || clockwise is not { } cw || distance == 0) return;
        foreach (var cast in marchCasters.Values)
        {
            var position = PredictMarch(cast.Position, cw, distance, Center);
            var small = actors.GetValueOrDefault(cast.Source)?.Oid == 0x49DF || cast.Action == 44864;
            position.Z -= small ? 5 : 15;
            AddHit($"march/{cast.Source}", "One/Two-stone March 预判", RayRectangleTemplate.Symmetric(small ? 10 : 30, small ? 10 : 30),
                position, cast.Rotation ?? 0, marchHit, cast.Source);
        }
        marchCasters.Clear();
        clockwise = null;
    }

    internal static Vector3 PredictMarch(Vector3 position, bool cw, int distance, Vector3 center)
    {
        var x = (int)MathF.Round((position.X - center.X) / 5);
        var z = (int)MathF.Round((position.Z - center.Z) / 5);
        var ring = Math.Abs(x) == 1 && Math.Abs(z) == 1 ? Inner : Outer;
        var index = Array.IndexOf(ring, (x, z));
        if (index < 0) return position;
        var next = ((index + (cw ? distance : -distance)) % ring.Length + ring.Length) % ring.Length;
        return center + new Vector3(ring[next].X * 5, 0, ring[next].Z * 5);
    }

    protected override void OnEffect(LogSystemActionEffectEvent effect, DateTime now)
    {
        if (effect.ActionId == 44872) { RemoveGroup("spineshot/"); RemoveGroup("cast/" + Boss?.Id + "/44866"); }
        if (effect.ActionId == 44861) RemoveGroup("march/");
        if (effect.ActionId == 44909 && effect.SourceRotation is { } rotation)
        {
            var sequence = rotations.Where(r => r.Remaining > 0).MinBy(r => MathF.Abs(MathF.IEEERemainder(r.Rotation - rotation, MathF.Tau)));
            if (sequence == null || MathF.Abs(MathF.IEEERemainder(sequence.Rotation - rotation, MathF.Tau)) > .1f) return;
            sequence.Rotation = rotation + sequence.Increment;
            sequence.Next = now.AddSeconds(1.1);
            --sequence.Remaining;
        }
    }

    protected override void AddPersonalAreas(List<Area> result, DateTime now, ulong player)
    {
        if (result.Any(a => a.Key.StartsWith("march/", StringComparison.Ordinal) || a.Action is 44860 or 44861)) return;
        for (var side = 0; side < rotations.Count; ++side)
        {
            var sequence = rotations[side];
            if (sequence.Next.AddSeconds(sequence.Remaining * 1.1 + 1) < now) continue;
            for (var i = 0; i < sequence.Remaining; ++i)
            {
                var hit = sequence.Next.AddSeconds(i * 1.1);
                if (hit.AddSeconds(.15) < now) continue;
                result.Add(new($"rotate/{side}/{i}", "Spinning Needles", new FanTemplate(60, Rad(60)), sequence.Origin,
                    sequence.Rotation + i * sequence.Increment, hit.AddSeconds(-.2), hit.AddSeconds(.15), sequence.Owner,
                    NavigationOnly: i > 0));
            }
        }
        if (result.Any(a => a.Key.StartsWith("rotate/", StringComparison.Ordinal)))
            result.Add(new("rotate/goal", "旋转针靠近中心", new CircleTemplate(4), Center, 0, now, now.AddSeconds(1), Safe: true));
    }

    public override void Cancel(ulong source) { base.Cancel(source); marchCasters.Remove(source); rotations.RemoveAll(r => r.Owner == source); }
    protected override void ResetMechanics() { distance = 0; clockwise = null; marchCasters.Clear(); rotations.Clear(); }
    private sealed class RotationSequence(ulong owner, Vector3 origin, float rotation, float increment, DateTime next)
    {
        public ulong Owner { get; } = owner;
        public Vector3 Origin { get; } = origin;
        public float Rotation = rotation;
        public float Increment { get; } = increment;
        public DateTime Next = next;
        public int Remaining = 10;
    }
}
