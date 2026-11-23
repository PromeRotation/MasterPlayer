using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly HashSet<(ulong Source, uint Action, DateTime Start)> ghimlytSeenCasts = [];
    private readonly Dictionary<ulong, uint> ghimlytAnimations = [];
    private readonly Dictionary<ulong, DateTime> ghimlytAnimationTimes = [];
    private readonly Dictionary<ulong, byte> ghimlytStates = [];
    private readonly Dictionary<ulong, float> ghimlytFire = [];
    private readonly Dictionary<ulong, DateTime> ghimlytBirths = [];
    private readonly HashSet<ulong> ghimlytSeenTanks = [];
    private readonly List<(ulong Source, DungeonAmbientAoe Aoe)> ghimlytCrosshatches = [];
    private readonly List<DungeonAmbientAoe> ghimlytTanks = [];
    private DungeonAmbientAoe? ghimlytHeat;
    private DungeonAmbientAoe? ghimlytNitro;
    private Vector3? ghimlytShield;
    private float? ghimlytSlashRotation;
    private float? ghimlytSlashIncrement;
    private DateTime ghimlytSlashActivation;
    private int ghimlytSlashRemaining;
    private int ghimlytHeatHits;
    private bool ghimlytShrunk;
    private ulong ghimlytHeatSource;

    private bool IsGhimlytGroup => territory == GhimlytCatalog.TERRITORY && Active?.BossOid is 0x2528 or 0x25B0;
    private bool GhimlytGroupMember(DungeonActor actor) => IsGhimlytGroup && !actor.Dead
        && (Active!.BossOid == actor.Oid || Active.PrimaryAliases?.Contains(actor.Oid) == true);
    private void GhimlytSelectPrimary()
    {
        if (!IsGhimlytGroup) return;
        var current = _actors.GetValueOrDefault(PrimaryId);
        var replacement = _actors.Values.Where(GhimlytGroupMember)
            .OrderByDescending(a => a.InCombat).ThenByDescending(a => a.Targetable).FirstOrDefault();
        if (replacement != null && (current == null || current.Dead || !current.InCombat && replacement.InCombat))
            PrimaryId = replacement.Id;
    }

    private void GhimlytCast(DungeonPendingCast cast)
    {
        if (territory != GhimlytCatalog.TERRITORY || !ghimlytSeenCasts.Add((cast.Source, cast.Rule.Action, cast.StartedAt))) return;
        if (cast.Rule.Action == 14196)
        {
            ghimlytSlashRotation = cast.Rotation;
            ghimlytSlashActivation = cast.FinishAt.AddSeconds(2.2);
            StartGhimlytSlash();
        }
        if (cast.Rule.Action == 13397 && !ghimlytShrunk)
            ghimlytNitro = new("ghimlyt/nitro", "场地外围即将燃烧", new DungeonShapeTemplate(GhimlytArenas.Nitro(cast.Location.Y)),
                cast.Location, Activation: cast.FinishAt.AddSeconds(.8));
        if (GhimlytCatalog.IsCrosshatch(cast.Rule.Action))
        {
            var activation = ghimlytCrosshatches.Count == 0 ? cast.FinishAt.AddSeconds(2.1)
                : ghimlytCrosshatches[0].Aoe.Activation!.Value.AddSeconds(ghimlytCrosshatches.Count * .5);
            ghimlytCrosshatches.Add((cast.Source, new($"ghimlyt/crosshatch/{cast.Source}", "连续交叉冲锋",
                RayRectangleTemplate.Symmetric(4, 40.5f), cast.Location, cast.Rotation, Activation: activation)));
        }
    }

    private void GhimlytFinish(DungeonPendingCast cast, bool completed)
    {
        if (territory != GhimlytCatalog.TERRITORY || completed) return;
        if (cast.Rule.Action == 14196)
        {
            ghimlytSlashRemaining = 0; ghimlytSlashRotation = null; ghimlytSlashIncrement = null;
        }
        if (cast.Rule.Action == 13397) ghimlytNitro = null;
        if (GhimlytCatalog.IsCrosshatch(cast.Rule.Action)) ghimlytCrosshatches.RemoveAll(a => a.Source == cast.Source);
    }

    private void GhimlytIcon(uint icon)
    {
        if (Active?.BossOid != 0x25DA || icon is not (167 or 168)) return;
        ghimlytSlashIncrement = (icon == 167 ? -1 : 1) * MathF.PI / 3;
        StartGhimlytSlash();
    }

    private void StartGhimlytSlash()
    {
        // Explicit readiness accepts a legitimate zero heading from the packet.
        if (ghimlytSlashRotation != null && ghimlytSlashIncrement != null && ghimlytSlashRemaining == 0)
            ghimlytSlashRemaining = 6;
    }

    private void GhimlytEffect(ulong source, uint action, DateTime time, Vector3? position)
    {
        if (territory != GhimlytCatalog.TERRITORY) return;
        if (action == 14472) ghimlytShield = _actors.GetValueOrDefault(source)?.Position ?? position;
        if (action == 14470) ghimlytShield = null;
        if (action is 14196 or 14671 && ghimlytSlashRemaining > 0)
        {
            --ghimlytSlashRemaining;
            if (ghimlytSlashRemaining == 0) { ghimlytSlashRotation = null; ghimlytSlashIncrement = null; }
            else { ghimlytSlashRotation += ghimlytSlashIncrement; ghimlytSlashActivation = time.AddSeconds(1.1); }
        }
        if (action == 13400 && ++ghimlytHeatHits == 5) { ghimlytHeat = null; ghimlytHeatHits = 0; }
        if (action == 14114 && ghimlytCrosshatches.Count > 0) ghimlytCrosshatches.RemoveAt(0);
        if (action is 14104 or 14106 && ghimlytTanks.Count > 0) ghimlytTanks.RemoveAt(0);
    }

    private void GhimlytState(ulong source, byte state, bool fromSnapshot = false)
    {
        if (territory != GhimlytCatalog.TERRITORY) return;
        if (!fromSnapshot) ghimlytStates[source] = state;
        else if (ghimlytStates.TryGetValue(source, out var latest))
        {
            if (state != latest) return;
            ghimlytStates.Remove(source);
        }
        if (Active?.BossOid == 0x2515 && state == 1 && !ghimlytShrunk && Arena != null)
        {
            ghimlytShrunk = true; ghimlytNitro = null; Arena = GhimlytArenas.Prometheus(Arena.Position.Y);
        }
        if (Active?.BossOid == 0x25DA && _actors.GetValueOrDefault(source) is { Oid: 0x1EA1A1 } actor)
        {
            if (state == 1) ghimlytFire.TryAdd(source, actor.Rotation);
            if (state == 4) ghimlytFire.Remove(source);
        }
    }

    private void GhimlytAnimation(ulong source, uint state, DateTime time)
    {
        if (Active?.BossOid != 0x2515 || !_actors.TryGetValue(source, out var actor) || actor.Oid != 0x1EA1A1) return;
        if (ghimlytAnimations.GetValueOrDefault(source) == state)
        {
            if (time < ghimlytAnimationTimes.GetValueOrDefault(source))
            {
                ghimlytAnimationTimes[source] = time;
                if (ghimlytHeatSource == source && ghimlytHeat != null)
                    ghimlytHeat = ghimlytHeat with { Activation = time.AddSeconds(6.7) };
            }
            return;
        }
        ghimlytAnimations[source] = state;
        ghimlytAnimationTimes[source] = time;
        if (state != 0x00040008) return;
        (float Heading, float X, float Z, float Rotation)[] sources =
        [
            (68, 79.481f, -57.598f, 67.482f), (-113, 188.498f, -12.441f, -112.488f),
            (113, 79.481f, -12.441f, 112.477f), (23, 111.411f, -89.528f, 22.498f),
            (157, 111.411f, 19.489f, 157.483f), (-157, 156.568f, 19.489f, -157.505f),
            (-23, 156.568f, -89.528f, -22.487f), (-68, 188.498f, -57.598f, -67.482f),
        ];
        foreach (var entry in sources)
            if (MathF.Abs(MathF.Atan2(MathF.Sin(actor.Rotation - entry.Heading * MathF.PI / 180),
                MathF.Cos(actor.Rotation - entry.Heading * MathF.PI / 180))) <= MathF.PI / 180)
            {
                ghimlytHeat = new("ghimlyt/heat", "连续五次热风", RayRectangleTemplate.Symmetric(16, 107.8f),
                    new(entry.X, actor.Position.Y, entry.Z), entry.Rotation * MathF.PI / 180, Activation: time.AddSeconds(6.7));
                ghimlytHeatSource = source;
                break;
            }
    }

    private void GhimlytCreated(ulong source, DateTime time)
    {
        if (territory == GhimlytCatalog.TERRITORY) ghimlytBirths.TryAdd(source, time);
    }

    private void SynchronizeGhimlyt()
    {
        if (territory != GhimlytCatalog.TERRITORY) return;
        foreach (var actor in _actors.Values.Where(a => a.EventState is 1 or 4)) GhimlytState(actor.Id, actor.EventState, fromSnapshot: true);
        if (Active?.BossOid != 0x25B0 || Arena == null) return;
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0x25B2 && !a.Dead && ghimlytSeenTanks.Add(a.Id)))
        {
            if (ghimlytTanks.Count != 0) continue;
            var birth = ghimlytBirths.GetValueOrDefault(actor.Id, _now);
            ghimlytTanks.Add(new("ghimlyt/tank/roundhouse", "油桶踢击", new CircleTemplate(6.6f),
                QuantizedDungeonPosition(new(370.992f, Arena.Position.Y, -265.034f)), Activation: birth.AddSeconds(3.1)));
            Vector2[] origins = [new(370.992f, -277.028f), new(362.514f, -273.49f), new(379.485f, -273.49f),
                new(358.999f, -265.034f), new(382.986f, -265.034f), new(379.485f, -256.52f),
                new(362.514f, -256.52f), new(370.992f, -253.01f)];
            double[] timings = [5.8, 8.3, 8.3, 10.7, 10.7, 13.2, 13.2, 15.7];
            for (var i = 0; i < origins.Length; ++i)
                ghimlytTanks.Add(new($"ghimlyt/tank/{i}", "油桶连爆", new CircleTemplate(11),
                    QuantizedDungeonPosition(new(origins[i].X, Arena.Position.Y, origins[i].Y)), Activation: birth.AddSeconds(timings[i])));
        }
    }

    private void GhimlytRemoved(ulong source)
    {
        ghimlytFire.Remove(source);
        ghimlytStates.Remove(source);
        ghimlytBirths.Remove(source);
        ghimlytCrosshatches.RemoveAll(a => a.Source == source);
    }

    private void ResetGhimlyt()
    {
        ghimlytSeenCasts.Clear(); ghimlytAnimations.Clear(); ghimlytAnimationTimes.Clear(); ghimlytStates.Clear();
        ghimlytFire.Clear(); ghimlytBirths.Clear();
        ghimlytSeenTanks.Clear(); ghimlytCrosshatches.Clear(); ghimlytTanks.Clear();
        ghimlytHeat = ghimlytNitro = null; ghimlytShield = null;
        ghimlytSlashRotation = ghimlytSlashIncrement = null; ghimlytSlashRemaining = 0;
        ghimlytSlashActivation = default; ghimlytHeatHits = 0; ghimlytShrunk = false; ghimlytHeatSource = 0;
    }
}
