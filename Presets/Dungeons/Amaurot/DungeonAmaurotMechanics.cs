using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DateTime> amaurotMeteors = [];
    private readonly HashSet<ulong> amaurotSeenStars = [];
    private readonly Dictionary<ulong, (DungeonLosTemplate Safe, DungeonLosTemplate Danger)> amaurotLos = [];
    private readonly List<(DungeonAmbientAoe Aoe, int Hits)> amaurotFaces = [];
    private readonly Dictionary<ulong, (uint State, DateTime Time)> amaurotAnimations = [];
    private readonly HashSet<int> amaurotBreaking = [];
    private int amaurotRemovedMask;
    private int amaurotApocalypseHits;
    private int amaurotRayHits;
    private DungeonAmbientAoe? amaurotApocalypse;
    private DungeonAmbientAoe? amaurotRay;
    internal static readonly Vector2[] AmaurotPlatforms =
    [
        new(-12, -71), new(12, -71), new(-12, -51), new(12, -51),
        new(-12, -31), new(12, -31), new(-12, -17), new(12, -17),
    ];

    private void AmaurotIcon(ulong target, uint icon, DateTime time)
    {
        if (Active?.BossOid == 0x27B6 && icon == 57) amaurotMeteors[target] = time.AddSeconds(8.1);
    }

    private void SynchronizeAmaurot()
    {
        if (Active?.BossOid != 0x27B6) return;
        foreach (var star in _actors.Values.Where(a => a.Oid == 0x29DC))
            if (amaurotSeenStars.Add(star.Id)) amaurotMeteors.Clear();
        foreach (var id in amaurotMeteors.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray()) amaurotMeteors.Remove(id);
    }

    private void AmaurotCreated(ulong source)
    {
        if (Active?.BossOid != 0x27B6 || _actors.GetValueOrDefault(source)?.Oid != 0x29DC) return;
        amaurotSeenStars.Add(source);
        amaurotMeteors.Clear();
    }

    private void AmaurotCast(DungeonPendingCast cast)
    {
        if (territory != AmaurotCatalog.TERRITORY) return;
        if (cast.Rule.Action == 15563)
        {
            var blockers = _actors.Values.Where(a => a.Oid == 0x29DC).ToArray();
            amaurotLos[cast.Source] = (new(new(cast.Location, blockers, true, 70, true, 0)),
                new(new(cast.Location, blockers, false, 70, true, 0)));
        }
        if (cast.Rule.Action == 15575 && 9 - System.Numerics.BitOperations.PopCount((uint)amaurotRemovedMask) - amaurotBreaking.Count >= 1)
        {
            amaurotApocalypseHits = 0;
            amaurotApocalypse = new("amaurot/apocalypse", "末日光束持续五击", RayRectangleTemplate.Symmetric(20, 76),
                cast.Location, cast.Rotation, Activation: cast.FinishAt);
        }
        if (cast.Rule.Action == 17107)
        {
            amaurotRayHits = 0;
            amaurotRay = new("amaurot/ray", "首领光束持续五击", RayRectangleTemplate.Symmetric(6, 60),
                cast.Location, cast.Rotation, Activation: cast.FinishAt);
        }
    }

    private void AmaurotFinish(DungeonPendingCast cast, bool completed)
    {
        if (territory != AmaurotCatalog.TERRITORY) return;
        amaurotLos.Remove(cast.Source);
        if (completed) return;
        if (cast.Rule.Action == 15575) { amaurotApocalypse = null; amaurotApocalypseHits = 0; }
        if (cast.Rule.Action == 17107) { amaurotRay = null; amaurotRayHits = 0; }
    }

    private void AmaurotEffect(ulong source, uint action, Vector3? location, float? rotation, DateTime time)
    {
        if (Active?.BossOid != 0x27C1) return;
        if (action == 15577 && ++amaurotApocalypseHits == 4) { amaurotApocalypse = null; amaurotApocalypseHits = 0; }
        if (action == 15585 && ++amaurotRayHits == 4) { amaurotRay = null; amaurotRayHits = 0; }
        if (action is not (15580 or 15581)) return;
        var actor = _actors.GetValueOrDefault(source);
        if ((actor?.Position ?? location) is not { } origin || (rotation ?? actor?.Rotation) is not { } heading) return;
        if (amaurotFaces.Count == 0)
        {
            foreach (var face in _actors.Values.Where(a => a.Oid == 0x27C3 && !a.Dead))
                amaurotFaces.Add((new($"amaurot/face/{face.Id}", "人面连续光束", RayRectangleTemplate.Symmetric(6, 60),
                    QuantizedDungeonPosition(face.Position), face.Rotation,
                    Activation: face.Rotation == heading ? time : time.AddSeconds(8.5)), 0));
            amaurotFaces.Sort((a, b) => Nullable.Compare(a.Aoe.Activation, b.Aoe.Activation));
        }
        for (var i = 0; i < amaurotFaces.Count; ++i)
        {
            var entry = amaurotFaces[i];
            if (Vector2.DistanceSquared(new(entry.Aoe.Position.X, entry.Aoe.Position.Z), new(origin.X, origin.Z)) > .01f) continue;
            if (++entry.Hits == 5) amaurotFaces.RemoveAt(i);
            else amaurotFaces[i] = entry;
            return;
        }
    }

    private void AmaurotAnimation(ulong source, uint state, DateTime time, bool packet)
    {
        if (Active?.BossOid != 0x27C1 || !_actors.TryGetValue(source, out var actor) || state is not (0x00040008 or 0x00100020)) return;
        if (amaurotAnimations.TryGetValue(source, out var previous) && previous.State == state && (!packet || time <= previous.Time)) return;
        amaurotAnimations[source] = (state, time);
        var mask = amaurotRemovedMask;
        for (var i = 0; i < AmaurotPlatforms.Length; ++i)
        {
            if (Vector2.DistanceSquared(new(actor.Position.X, actor.Position.Z), AmaurotPlatforms[i]) > 25) continue;
            if (state == 0x00100020)
            {
                if ((mask & (1 << i)) == 0) amaurotBreaking.Add(i);
            }
            else { mask |= 1 << i; amaurotBreaking.Remove(i); }
        }
        if (mask == amaurotRemovedMask) return;
        amaurotRemovedMask = mask;
        Arena = AmaurotArenas.Therion(mask, Arena!.Position.Y);
    }

    private void AmaurotRemoved(ulong source)
    {
        amaurotMeteors.Remove(source); amaurotLos.Remove(source);
        amaurotFaces.RemoveAll(a => a.Aoe.Id == $"amaurot/face/{source}");
    }

    private void ResetAmaurot()
    {
        amaurotMeteors.Clear(); amaurotSeenStars.Clear(); amaurotLos.Clear(); amaurotFaces.Clear(); amaurotAnimations.Clear();
        amaurotBreaking.Clear(); amaurotRemovedMask = 0; amaurotApocalypseHits = 0; amaurotRayHits = 0;
        amaurotApocalypse = null; amaurotRay = null;
    }
}
