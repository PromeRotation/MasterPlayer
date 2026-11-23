using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long skySerial;
    private bool skyBubbleCasting;
    private bool skyFirearmsSmall;
    private bool skyHeatActive;
    private Vector3? skyHeatOrigin;
    private DateTime? skyAshlayer;
    private DungeonAmbientAoe? skyRing;
    private DungeonAmbientAoe? skyShrink;
    private readonly List<(DungeonActor Actor, long Serial)> skyBubbles = [];
    private readonly HashSet<long> skyDestroyedBubbles = [];
    private readonly List<(DungeonActor Actor, DateTime At, long Serial)> skyWaves = [];
    private readonly HashSet<(ulong Source, DateTime Start)> skyWaveStarts = [];
    private readonly List<DungeonAmbientAoe> skyBursts = [];
    private readonly List<DungeonAmbientAoe> skyStones = [];
    private readonly List<DungeonAmbientAoe> skyShatters = [];
    private readonly List<DungeonPendingCast> skyImpacts = [];

    private bool SkydeepCastVisible(DungeonPendingCast cast) => territory != SkydeepCatalog.TERRITORY
        || cast.Rule.Action is not (36670 or 36671 or 36696 or 36697);

    private void SkydeepCast(DungeonPendingCast cast, DateTime now)
    {
        if (territory != SkydeepCatalog.TERRITORY || Arena == null) return;
        var action = cast.Rule.Action;
        if (action is 36732 or 38787) skyBubbleCasting = true;
        if (action == 36733)
            skyRing = new("sky/ring", "水环缩场", new DonutTemplate(12, 24), Arena.Position, Activation: cast.FinishAt);
        if (action == 36728 && skyWaveStarts.Add((cast.Source, cast.StartedAt)))
            foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Buddy && !a.Dead))
                skyWaves.Add((a, now.AddSeconds(6.3), ++skySerial));
        if (action is 36736 or 36737)
        {
            var id = $"sky/burst/{cast.Source}/{action}/{cast.StartedAt.Ticks}/";
            skyBursts.RemoveAll(a => a.Id.StartsWith(id));
            foreach (var bubble in _actors.Values.Where(a => a.Oid == 0x41D4 && a.HitboxRadius != 1.1f))
                skyBursts.Add(new(id + bubble.Id, "水流移动后爆炸", new CircleTemplate(6),
                    bubble.Position + Vector3.UnitX * (action == 36736 ? 8 : -8), Activation: cast.FinishAt.AddSeconds(3.4)));
        }
        if (action == 36448 && !skyFirearmsSmall)
        {
            Vector2[][] strips =
            [
                [new(-25, -25), new(25, -25), new(25, -20), new(-25, -20)],
                [new(-25, 20), new(25, 20), new(25, 25), new(-25, 25)],
                [new(-25, -20), new(-20, -20), new(-20, 20), new(-25, 20)],
                [new(20, -20), new(25, -20), new(25, 20), new(20, 20)],
            ];
            var fill = strips.SelectMany(p => new[] { new[] { p[0], p[1], p[2] }, new[] { p[0], p[2], p[3] } }).ToArray();
            skyShrink = new("sky/shrink", "缩场预告", new DungeonShapeTemplate(new PolygonRegionGeometry(Arena.Position, strips, [], fill)),
                Arena.Position, Activation: cast.FinishAt.AddSeconds(.6));
        }
        if (action is 36670 or 36671 or 36696 or 36697)
        {
            var id = $"sky/stone/{cast.Source}/{cast.StartedAt.Ticks}";
            var index = skyStones.FindIndex(a => a.Id == id);
            var aoe = new DungeonAmbientAoe(id, "连续石刻", RayRectangleTemplate.Symmetric(20, 40), cast.Location, cast.Rotation,
                Navigate: false, Activation: cast.FinishAt);
            if (index >= 0) skyStones[index] = aoe with { Color = skyStones[index].Color, Navigate = skyStones[index].Navigate };
            else
            {
                skyStones.Add(aoe);
                if (skyStones.Count == 2) skyStones.Sort((a, b) => a.Activation!.Value.CompareTo(b.Activation));
            }
        }
        if (action is 36677 or 36667 or 36707)
        {
            var index = skyImpacts.FindIndex(c => c.Source == cast.Source && c.StartedAt == cast.StartedAt);
            if (index < 0) skyImpacts.Add(cast); else skyImpacts[index] = cast;
        }
        if (action == 36710) skyHeatActive = true;
        if (action == 36711) skyAshlayer = cast.FinishAt.AddSeconds(2.2);
        SkydeepObserveActors();
    }

    private void SkydeepFinish(DungeonPendingCast cast, DateTime now)
    {
        if (territory != SkydeepCatalog.TERRITORY) return;
        var action = cast.Rule.Action;
        if (action is 36732 or 38787) skyBubbleCasting = false;
        if (action == 36738) skyBursts.Clear();
        if (action is 36670 or 36671 or 36696 or 36697 && skyStones.Count > 0) skyStones.RemoveAt(0);
        if (action is 36684 or 36685 or 36686) skyShatters.Clear();
        // NPC elapsed time is capped at the action's five-second duration.
        // CastFinishAt therefore retains 0.3 seconds at a delayed finish callback.
        var shatterAt = (now > cast.FinishAt.AddSeconds(-.3) ? now : cast.FinishAt.AddSeconds(-.3)).AddSeconds(15.4);
        if (action is 36679 or 36680)
            skyShatters.Add(new($"sky/shatter/{++skySerial}", "延迟中间碎裂", RayRectangleTemplate.Symmetric(20, 40),
                cast.Location, cast.Rotation, Activation: shatterAt));
        if (action is 36681 or 36682 && Arena != null)
        {
            skyShatters.Add(new($"sky/shatter/{++skySerial}", "延迟两侧碎裂", RayRectangleTemplate.Symmetric(22, 45),
                new(91.539f, Arena.Position.Y, -453.025f), -17.004f * MathF.PI / 180, Activation: shatterAt));
            skyShatters.Add(new($"sky/shatter/{++skySerial}", "延迟两侧碎裂", RayRectangleTemplate.Symmetric(22, 45),
                new(108.415f, Arena.Position.Y, -453.025f), 16.999f * MathF.PI / 180, Activation: shatterAt));
        }
        if (action == 36710) skyHeatActive = false;
        skyImpacts.RemoveAll(c => c.Source == cast.Source);
    }

    private void SkydeepEffect(uint action)
    {
        if (territory != SkydeepCatalog.TERRITORY) return;
        if (action == 36729) skyWaves.Clear();
        if (action == 36712) skyAshlayer = null;
    }

    private void SkydeepTimeline(ulong source, uint timeline)
    {
        if (territory != SkydeepCatalog.TERRITORY || Active?.BossOid != 0x41D3
            || !_actors.TryGetValue(source, out var actor) || actor.HitboxRadius != 1.1f) return;
        if (timeline == 0x1E46) skyBubbles.Add((actor, ++skySerial));
        if (timeline == 0x1E3C)
        {
            var index = skyBubbles.FindIndex(a => a.Actor.Id == source && !skyDestroyedBubbles.Contains(a.Serial));
            if (index >= 0) skyBubbles.RemoveAt(index);
        }
    }

    private void SkydeepMap(uint index, uint state)
    {
        if (territory != SkydeepCatalog.TERRITORY || Arena == null) return;
        if (Active?.BossOid == 0x41D3 && index == 0x13)
        {
            if (state == 0x00020001) { Arena = SkydeepArenas.FeatherSmall(Arena.Position.Y); skyRing = null; }
            if (state == 0x00080004) Arena = new RectangleGeometry(Arena.Position, 0, 31, 31);
        }
        if (Active?.BossOid == 0x4184 && index == 0x14 && state == 0x00020001)
        {
            skyFirearmsSmall = true; Arena = new RectangleGeometry(Arena.Position, 0, 40, 40); skyShrink = null;
        }
    }

    private void SkydeepObserveActors()
    {
        if (territory != SkydeepCatalog.TERRITORY) return;
        for (var i = 0; i < skyBubbles.Count; ++i)
            if (!skyDestroyedBubbles.Contains(skyBubbles[i].Serial))
            {
                if (_actors.TryGetValue(skyBubbles[i].Actor.Id, out var actor))
                    skyBubbles[i] = (actor, skyBubbles[i].Serial);
                else
                    skyDestroyedBubbles.Add(skyBubbles[i].Serial);
            }
        for (var i = 0; i < skyWaves.Count; ++i)
            skyWaves[i] = (_actors.GetValueOrDefault(skyWaves[i].Actor.Id) ?? skyWaves[i].Actor, skyWaves[i].At, skyWaves[i].Serial);
        if (!_casts.Values.Any(c => c.Rule.Action == 36709)) return;
        if (skyImpacts.Any(c => c.Rule.Action == 36677))
            skyHeatOrigin = Arena is { } arena ? new(100, arena.Position.Y, -400) : null;
        else if (skyImpacts.FirstOrDefault(c => c.Rule.Action == 36667) is { } second) skyHeatOrigin = second.Location;
        else if (skyImpacts.FirstOrDefault(c => c.Rule.Action == 36707) is { } third) skyHeatOrigin = third.Location;
    }

    private void SkydeepRemoved(ulong source)
    {
        if (territory != SkydeepCatalog.TERRITORY) return;
        foreach (var bubble in skyBubbles.Where(a => a.Actor.Id == source))
            skyDestroyedBubbles.Add(bubble.Serial);
    }

    private float? SkydeepFacing(Vector3 player, float rotation)
    {
        var own = skyWaves.FirstOrDefault(w => w.Actor.Id == PlayerId && !w.Actor.Dead);
        if (own.Actor == null || own.At > _now.AddSeconds(.75)) return null;
        var directions = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Buddy && !a.Dead)
            .Select(a => MathF.Atan2(a.Position.X - player.X, a.Position.Z - player.Z)).ToArray();
        var candidates = new[] { rotation }.Concat(directions.SelectMany(a => new[] { a + MathF.PI / 12 + .02f, a - MathF.PI / 12 - .02f }));
        foreach (var candidate in candidates)
            if (directions.All(a => MathF.Abs(MathF.Atan2(MathF.Sin(candidate - a), MathF.Cos(candidate - a))) > MathF.PI / 12 + .001f))
                return candidate;
        return null;
    }

    private void ResetSkydeep()
    {
        skySerial = 0; skyBubbleCasting = false; skyFirearmsSmall = false; skyHeatActive = false; skyHeatOrigin = null;
        skyAshlayer = null; skyRing = null; skyShrink = null; skyBubbles.Clear(); skyWaves.Clear(); skyWaveStarts.Clear();
        skyBursts.Clear(); skyStones.Clear(); skyShatters.Clear(); skyImpacts.Clear(); skyDestroyedBubbles.Clear();
    }
}
