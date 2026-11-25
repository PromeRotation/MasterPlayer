using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsWhorleater => territory == WhorleaterCatalog.TERRITORY;
    private float whorlPreviousZ;
    private (Vector3 Position, float Rotation, DateTime At)? whorlBodyAoe;
    private (float Direction, DateTime At)? whorlBodyKnockback;
    private (Vector3 Position, float Rotation, DateTime At)? whorlDiveAoe;
    private (Vector3 Position, float Rotation, DateTime At)? whorlDiveKnockback;
    private readonly HashSet<ulong> whorlSeen = [];
    private readonly Dictionary<ulong, DateTime> whorlBirths = [];
    private ulong whorlDiveSource;
    private DateTime whorlDiveAoeResolved;
    private DateTime whorlDiveKnockbackResolved;
    private readonly Dictionary<(ulong Actor, uint Status), (bool Present, DateTime At, DateTime Expiry)> whorlImmunity = [];

    private void WhorlCreated(ulong source, DateTime timestamp)
    {
        if (!IsWhorleater || !whorlBirths.TryAdd(source, timestamp) || source != whorlDiveSource) return;
        // Creation can arrive after the first object snapshot; only correct a still-live prediction.
        if (whorlDiveAoe is { } aoe) whorlDiveAoe = aoe with { At = timestamp.AddSeconds(.6) };
        if (whorlDiveKnockback is { } knockback) whorlDiveKnockback = knockback with { At = timestamp.AddSeconds(1.4) };
    }

    private void WhorlObserve()
    {
        if (!IsWhorleater || Active == null) return;
        if (_actors.GetValueOrDefault(PrimaryId) is { } primary)
        {
            var z = primary.Position.Z;
            if (whorlPreviousZ == 0) whorlPreviousZ = z;
            if (z != whorlPreviousZ && z != 0)
            {
                whorlPreviousZ = z;
                whorlBodyAoe = (primary.Position, primary.Rotation, _now.AddSeconds(2.6));
                whorlBodyKnockback = (z <= 0 ? MathF.PI : 0, _now.AddSeconds(4.8));
            }
        }
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0xA87))
        {
            if (!whorlSeen.Add(actor.Id)) continue;
            var position = QuantizedDungeonPosition(actor.Position);
            var birth = whorlBirths.GetValueOrDefault(actor.Id, _now);
            whorlDiveSource = actor.Id;
            whorlDiveAoe = birth > whorlDiveAoeResolved ? (position, actor.Rotation, birth.AddSeconds(.6)) : null;
            whorlDiveKnockback = birth > whorlDiveKnockbackResolved ? (position, actor.Rotation, birth.AddSeconds(1.4)) : null;
        }
    }

    private void WhorlEffect(uint action, DateTime timestamp)
    {
        if (!IsWhorleater) return;
        if (action == 1860) whorlBodyAoe = null;
        if (action is 1938 or 1937) whorlBodyKnockback = null;
        if (action == 1869)
        {
            whorlDiveAoe = null;
            whorlDiveAoeResolved = timestamp;
        }
        if (action == 1861)
        {
            whorlDiveKnockback = null;
            whorlDiveKnockbackResolved = timestamp;
        }
    }

    private bool WhorlCaster(DungeonActor actor) => actor.ClassJob is 6 or 7 or 24 or 25 or 26 or 27 or 28 or 33 or 40 or 42
        || actor.ClassJob == 36 && !HasStatus(actor, 2124);
    private static bool WhorlRanged(DungeonActor actor) => actor.ClassJob is 5 or 23 or 31 or 38;
    private int WhorlPriority(DungeonActor actor)
    {
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) return 0;
        var add = actor.Oid switch { 0xA83 => 4, 0xA85 => 3, 0xA84 => 2, _ => 0 };
        if (add > 0) return add;
        if (WhorlCaster(player)) return actor.Oid == 0xA67 ? 1 : 0;
        if (WhorlRanged(player)) return actor.Oid == 0xA86 ? 1 : 0;
        if (player.ClassJob is 1 or 2 or 3 or 4 or 19 or 20 or 21 or 22 or 29 or 30 or 32 or 34 or 37 or 39 or 41
            || player.ClassJob == 36 && HasStatus(player, 2124)) return actor.Oid is 0xA67 or 0xA86 ? 1 : 0;
        return 0;
    }

    private IEnumerable<DungeonAmbientAoe> WhorlAoes()
    {
        if (!IsWhorleater || Active == null || Arena == null) yield break;
        foreach (var actor in _actors.Values.Where(a => a.Oid is 0xA67 or 0xA86 or 0xA85 or 0xA84 or 0xA83 or 0x1E922A))
            yield return new($"whorl/enemy/{actor.Id}", "利维亚桑及目标", new CircleTemplate(actor.HitboxRadius), actor.Position,
                Navigate: false, OutlineOnly: true, Color: actor.Oid switch
                {
                    0xA85 => new Vector4(1, 0, 1, 1),
                    0x1E922A => new Vector4(1, 128f / 255, 0, 1),
                    _ => null,
                });
        if (whorlBodyAoe is { } body)
            yield return new("whorl/body/aoe", "冲撞伤害", RayRectangleTemplate.Symmetric(10, 34.5f),
                body.Position, body.Rotation, Activation: body.At);
        if (whorlDiveAoe is { } dive)
            yield return new("whorl/dive/aoe", "旋转冲锋伤害", RayRectangleTemplate.Symmetric(16, 50.5f),
                dive.Position, dive.Rotation, Activation: dive.At);
        var converter = _actors.Values.FirstOrDefault(a => a.Oid == 0x1E922A);
        if (converter?.Targetable == true)
            yield return new("whorl/converter", "激活以太转换器", new CircleTemplate(1.4f), converter.Position,
                Navigate: false, OutlineOnly: true, Safe: true);
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        foreach (var (id, start, landing, unsafeLanding) in WhorlKnockbackLandings(player))
            foreach (var aoe in DungeonMovementPreview($"whorl/{id}/knockback", unsafeLanding ? "击退落点在毒区" : "撞墙停止的击退落点",
                start, landing))
                yield return aoe;
    }

    private IEnumerable<(string Id, Vector3 Start, Vector3 Landing, bool Unsafe)> WhorlKnockbackLandings(DungeonActor player)
    {
        if (Arena == null) yield break;
        var dangers = TrialVoidzoneAoes().Select(a => a.Shape.Resolve(a.Position, a.Rotation, null)).ToArray();
        // Independent components each project from the current player, rather than chaining unrelated knockbacks.
        if (whorlBodyKnockback is { } body && !WhorlImmune(player, body.At))
        {
            var direction = new Vector3(0, 0, body.Direction == 0 ? 1 : -1);
            var landing = WhorlLanding(player, direction, 25);
            if (landing != player.Position) yield return ("body", player.Position, landing, dangers.Any(d => d.Contains(landing)));
        }
        if (whorlDiveKnockback is { } dive && !WhorlImmune(player, dive.At)
            && RayRectangleTemplate.Symmetric(16, 50.5f).Resolve(dive.Position, dive.Rotation, null).Contains(player.Position))
        {
            var delta = player.Position - dive.Position;
            delta.Y = 0;
            if (delta.LengthSquared() == 0) yield break;
            var landing = WhorlLanding(player, Vector3.Normalize(delta), 10);
            if (landing != player.Position) yield return ("dive", player.Position, landing, dangers.Any(d => d.Contains(landing)));
        }
    }

    private Vector3 WhorlLanding(DungeonActor player, Vector3 direction, float distance)
    {
        var from = player.Position;
        var ray = float.PositiveInfinity;
        if (direction.X > 0) ray = MathF.Min(ray, (14.5f - from.X) / direction.X);
        if (direction.X < 0) ray = MathF.Min(ray, (-14.5f - from.X) / direction.X);
        if (direction.Z > 0) ray = MathF.Min(ray, (19.5f - from.Z) / direction.Z);
        if (direction.Z < 0) ray = MathF.Min(ray, (-19.5f - from.Z) / direction.Z);
        distance = MathF.Min(distance, ray - MathF.Max(.001f, player.HitboxRadius - .499f));
        return from + direction * distance;
    }

    private void WhorlStatus(ulong actor, uint status, bool present, DateTime at, DateTime? expiry)
    {
        if ((IsWhorleater || IsSusano || IsLakshmi || IsShinryu || IsSuzaku || IsSeiryu || IsTitania || IsValigarmanda || IsQueenEternal || IsDoomtrain || IsGamboling || IsRaisingSword || IsRhalgr || IsDikaiosyne || IsBarPassage || IsNativeTreasure || IsTactical || territory is MainScenarioQuestCatalog.ORACLE_OF_LIGHT or MainScenarioQuestCatalog.FEAST_OF_LIES) && status is 160 or 1209 or 2663 or 3054 or 1722 or 2345)
            whorlImmunity[(actor, status)] = (present, at, expiry ?? DateTime.MaxValue);
    }

    private bool WhorlImmune(DungeonActor actor, DateTime activation)
    {
        foreach (var status in new uint[] { 160, 1209, 2663, 3054, 1722, 2345 })
        {
            if (whorlImmunity.TryGetValue((actor.Id, status), out var change) && _now < change.At.AddSeconds(.5))
            {
                if (change.Present && change.Expiry > activation) return true;
            }
            else if (actor.StatusExpirations?.GetValueOrDefault(status) > activation) return true;
        }
        return false;
    }

    private IEnumerable<string> WhorlHints()
    {
        if (!IsWhorleater || Active == null) yield break;
        if (_actors.Values.FirstOrDefault(a => a.Oid == 0x1E922A)?.Targetable == true) yield return "激活以太转换器，否则将团灭";
        if (_actors.Values.Any(a => a.Oid == 0xA83 && a.Targetable && !a.Dead)) yield return "优先击杀鱼人，避免失控";
        if (_actors.Values.Any(a => a.Oid == 0xA85 && a.Targetable && !a.Dead)) yield return "击破水泡";
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (WhorlKnockbackLandings(player).Any(p => p.Unsafe)) yield return "避开击退后的毒区落点";
        var tail = _actors.Values.FirstOrDefault(a => a.Oid == 0xA86);
        if (tail != null && !HasStatus(tail, 775) && HasStatus(tail, 477))
        {
            if (WhorlCaster(player) && player.TargetId == tail.Id) yield return "攻击头部：尾部将反射法术伤害";
            if (WhorlRanged(player) && player.TargetId == PrimaryId) yield return "攻击尾部：头部将反射远程物理伤害";
        }
    }

    private void ResetWhorl()
    {
        whorlPreviousZ = 0; whorlBodyAoe = null; whorlBodyKnockback = null;
        whorlDiveAoe = null; whorlDiveKnockback = null; whorlSeen.Clear();
        whorlBirths.Clear(); whorlDiveSource = 0;
        whorlDiveAoeResolved = default; whorlDiveKnockbackResolved = default;
        whorlImmunity.Clear();
    }
}
