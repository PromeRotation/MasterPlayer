using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal ulong[] ForbiddenDotTargets => Active == null ? [] : _actors.Values
        .Where(a => !a.Dead && a.Targetable && (IsCaduceus && a.Oid == 0x7D8
            || IsIfritExtreme && IfritExNails && IfritExNextNail != null && a.Oid == 0xD4))
        .Select(a => a.Id).ToArray();

    internal IReadOnlyList<DungeonEnemyDirective> EnemyDirectives()
    {
        if (Active == null || Arena == null || _actors.GetValueOrDefault(PlayerId) is not { Dead: false } player
            || !(IsUltimaExtreme || IsGarudaExtreme && GarudaExAi || IsTitanExtreme && TitanExCombat
                || IsIfritExtreme && IfritExAi || IsCaduceus || IsTwintania))
            return [];
        var tank = ThornTank(player);
        var assignment = IsCaduceus || IsTwintania ? CoilRole(player) : CurrentAssignment;
        var defaultTank = tank && (assignment == "MT" || assignment == "OT"
            && !_actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId && ThornTank(a)));
        List<DungeonEnemyDirective> result = [];
        foreach (var enemy in _actors.Values.Where(a => a.Hostile && a.PartySlot < 0 && a.Targetable && !a.Dead))
        {
            var directive = new DungeonEnemyDirective(enemy, enemy.Position, enemy.Rotation, defaultTank);
            if (IsUltimaExtreme)
            {
                directive = directive with { StayAtLongRange = true };
                if (enemy.Oid == 0x90F)
                {
                    var assigned = enemy.TargetId == PlayerId ? !UltimaTankSwap
                        : UltimaTankSwap && tank && player.Position.Z < enemy.Position.Z;
                    directive = directive with { Position = new(0, enemy.Position.Y, -10), Rotation = MathF.PI,
                        AttackStrength = .25f, ShouldBeTanked = assigned, PreferProvoking = assigned };
                }
                else if (enemy.Oid == 0x915) directive = directive with { AttackStrength = 0, ShouldBeTanked = false };
            }
            else if (IsGarudaExtreme)
            {
                directive = directive with { StayAtLongRange = true };
                if (enemy.Oid == 0xF5)
                {
                    var rotation = NativeEnemyRadians(garudaExAerialBlasts > 0 ? 135 : 180);
                    directive = directive with { Position = Arena.Position + NativeEnemyDirection(rotation) * (garudaExAerialBlasts > 0 ? 18 : 8),
                        Rotation = rotation, AttackStrength = .2f };
                }
                else if (enemy.Oid == 0x620) directive = directive with { AttackStrength = .15f };
                else if (enemy.Oid == 0x621)
                {
                    var rotation = NativeEnemyRadians(garudaExAerialBlasts > 0 ? -45 : 0);
                    directive = directive with { Position = Arena.Position + NativeEnemyDirection(rotation) * 18,
                        Rotation = rotation, AttackStrength = .15f, ShouldBeTanked = tank && assignment == "OT" };
                }
                else if (enemy.Oid is 0xF4 or 0x601 or 0x600)
                {
                    var assigned = enemy.Oid == 0x600 && GarudaExNeedsProvoke(enemy);
                    directive = directive with { AttackStrength = 0, ShouldBeTanked = assigned, PreferProvoking = assigned };
                }
            }
            else if (IsTitanExtreme)
            {
                directive = directive with { StayAtLongRange = true };
                var radius = (Arena.GetBounds().MaxX - Arena.GetBounds().MinX) * .5f;
                if (enemy.Oid is 0xF8 or 0x5E5)
                {
                    var boss = TitanExBoss!;
                    var currentTank = _actors.GetValueOrDefault(boss.TargetId);
                    var stacks = 0;
                    if (currentTank != null && HasStatus(currentTank, 126))
                    {
                        if (statusChanges.TryGetValue((currentTank.Id, 126), out var change) && _now < change.ChangedAt.AddSeconds(.5))
                            stacks = change.Param;
                        else
                            stacks = currentTank.StatusParams?.GetValueOrDefault(126u) ?? 0;
                    }
                    var swap = !_actors.Values.Any(a => a.Oid == 0x8F2 && a.Targetable && !a.Dead)
                        && titanExBuster != 1464 && stacks >= 2;
                    var assigned = tank && (boss.TargetId == PlayerId) != swap;
                    directive = directive with { Position = Arena.Position - new Vector3(0, 0, radius - 6),
                        Rotation = MathF.PI, TankDistance = 0, AttackStrength = .25f
                            + (enemy.Oid == 0xF8 && titanExBuster == 1464 || enemy.Oid == 0x5E5 && titanExBuster == 1463 ? .25f : 0),
                        ShouldBeTanked = assigned, PreferProvoking = assigned };
                }
                else if (enemy.Oid == 0x8F2)
                    directive = directive with { Position = Arena.Position + NativeEnemyDirection(NativeEnemyRadians(30)) * (radius - 4),
                        ShouldBeTanked = tank && TitanExBoss?.TargetId != PlayerId };
                else if (enemy.Oid == 0x5E0) directive = directive with { ShouldBeTanked = false };
                else if (enemy.Oid == 0x5E2) directive = directive with { AttackStrength = 0, ShouldBeTanked = false };
            }
            else if (IsIfritExtreme)
            {
                var nails = IfritExNails && IfritExNextNail != null;
                if (nails) directive = directive with { StayAtLongRange = true };
                if (enemy.Oid == 0xD3)
                {
                    if (IfritExHellfire)
                    {
                        var rotation = NativeEnemyRadians(150 - 40 * ifritExWave);
                        var assigned = tank && assignment == IfritExTankRole;
                        directive = directive with { Position = Arena.Position + NativeEnemyDirection(rotation) * 13,
                            Rotation = rotation, ShouldBeTanked = assigned, PreferProvoking = assigned, StayAtLongRange = true };
                    }
                    else
                    {
                        var offset = enemy.Position - Arena.Position;
                        offset.Y = 0;
                        var rotation = MathF.Atan2(offset.X, offset.Z);
                        var position = offset.LengthSquared() > 169 ? Arena.Position + NativeEnemyDirection(rotation) * 13 : enemy.Position;
                        var towardsPlayer = MathF.Atan2(player.Position.X - enemy.Position.X, player.Position.Z - enemy.Position.Z);
                        var cleave = new FanTemplate(21, MathF.PI * 2 / 3).Resolve(enemy.Position, towardsPlayer, null);
                        var assigned = tank && (enemy.TargetId == PlayerId || assignment == IfritExTankRole
                            && !_actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && !ThornTank(a) && cleave.Contains(a.Position)));
                        if (nails && IfritExNextNail is { } nail
                            && new FanTemplate(21, MathF.PI * 2 / 3).Resolve(enemy.Position, rotation, null).Contains(nail.Position))
                        {
                            var toNail = MathF.Atan2(nail.Position.X - enemy.Position.X, nail.Position.Z - enemy.Position.Z);
                            rotation = toNail + NativeEnemyRadians(toNail > rotation ? -75 : 75);
                        }
                        directive = directive with { Position = position, Rotation = rotation, AttackStrength = .35f,
                            ShouldBeTanked = assigned, PreferProvoking = assigned && enemy.TargetId != PlayerId, StayAtLongRange = true };
                    }
                }
                else if (nails && enemy.Oid is 0xD4 or 0x91B)
                    directive = directive with { AttackStrength = 0, ShouldBeTanked = false };
            }
            else if (IsCaduceus)
            {
                if (enemy.Oid == 0x7D7)
                {
                    directive = directive with { AttackStrength = CaduceusNextCleave < 3 ? .5f : .2f, StayAtLongRange = true };
                    if (enemy.Id != PrimaryId)
                    {
                        var destination = CaduceusPlatforms.Center(6);
                        var close = caduceusBoss != null && NativeEnemyDistanceSquared(enemy.Position, caduceusBoss.Position) < 625;
                        directive = directive with { ShouldBeTanked = tank && assignment == "OT", PreferProvoking = true,
                            Rotation = -MathF.PI / 2, Position = close ? new(destination.X, enemy.Position.Y, destination.Y) : enemy.Position };
                    }
                }
                else if (enemy.Oid == 0x7D8) directive = directive with { ShouldBeTanked = false };
            }
            else if (IsTwintania)
            {
                if (enemy.Id == PrimaryId && TwinCommonActive)
                {
                    directive = directive with { ShouldBeTanked = tank && assignment == TwinTankRole, PreferProvoking = true,
                        AttackStrength = .05f + ((twinNextSentence - _now).TotalSeconds < 1 ? .3f : 0)
                            + ((twinNextPlummet - _now).TotalSeconds < 3 ? .3f : 0) };
                    if (twinStage == 0) directive = directive with { Position = new(0, enemy.Position.Y, 5), Rotation = -MathF.PI / 2 };
                    else if (TwinP2)
                    {
                        var reposition = TwinMarked(twinFireball) == null && (twinNextFireball - _now).TotalSeconds > 5
                            && TwinMarked(twinConflag) == null && (twinNextConflag - _now).TotalSeconds > 5;
                        directive = directive with { Rotation = -MathF.PI / 2,
                            Position = reposition ? new(-14, enemy.Position.Y, 5) : enemy.Position };
                    }
                    else if (TwinP4) directive = directive with { Position = new(-7, enemy.Position.Y, -15), Rotation = MathF.PI };
                }
                else if (twinStage == 0 && enemy.Oid == 0x7E7 || twinAddsActive && enemy.Oid == 0x7E8)
                    directive = directive with { ShouldBeTanked = tank && assignment == "OT" };
                else if (twinAddsActive && enemy.Oid == 0x7E9)
                    directive = directive with { ShouldBeTanked = tank && assignment == "MT", AttackStrength = .15f };
                else if (TwinP4 && enemy.Oid == 0x7EA) directive = directive with { ShouldBeTanked = false };
                if (enemy.Id == PrimaryId && twinStage == 17) directive = directive with { PreferProvoking = true };
            }
            result.Add(directive);
        }
        return result;
    }

    private static float NativeEnemyRadians(float degrees) => degrees * MathF.PI / 180;
    private static Vector3 NativeEnemyDirection(float rotation) => new(MathF.Sin(rotation), 0, MathF.Cos(rotation));
    private static float NativeEnemyDistanceSquared(Vector3 first, Vector3 second)
        => Vector2.DistanceSquared(new(first.X, first.Z), new(second.X, second.Z));
}
