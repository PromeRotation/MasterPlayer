using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DateTime> sohrBirths = [];
    private readonly HashSet<ulong> sohrSeen = [];
    private DungeonPendingCast? sohrCross;
    private bool sohrCleave = true;
    private readonly Dictionary<ulong, Vector3> sohrPraise = [];
    private DungeonPendingCast? sohrCloud;
    private readonly Dictionary<ulong, DateTime> sohrCloudBaits = [];
    private DungeonPendingCast? sohrBright;
    private readonly List<DungeonDamagePrediction> sohrBusters = [];
    private bool sohrPoqStarted;
    private bool sohrFirstKnockback;
    private int sohrRemovedWalls;
    private readonly List<(Vector3 Position, float Rotation, DateTime Activation)> sohrKnockbackGoals = [];
    private readonly Dictionary<ulong, DungeonPendingCast> sohrKnockbacks = [];
    private DungeonPendingCast? sohrStack;
    private int sohrStackHits;
    private bool sohrFirstStack = true;
    private readonly Dictionary<ulong, DateTime> sohrSpreads = [];
    private readonly Dictionary<ulong, (Vector3 Next, Vector3 Step, DateTime Activation, int Remaining)> sohrOrbs = [];
    private DungeonPendingCast? sohrTouchdown;
    private bool sohrBridge;
    private bool IsSohrTrash => territory == SohrKhaiCatalog.TERRITORY && SohrKhaiCatalog.IsTrash(Active?.BossOid ?? 0);

    private bool SohrCanStart(DungeonActor actor, Vector3 player)
    {
        if (!SohrKhaiCatalog.IsTrash(actor.Oid)) return actor.InCombat;
        var arena = Definitions.First(d => d.BossOid == actor.Oid).Arena(actor.Position);
        return actor.Oid switch
        {
            0x1604 => arena.Contains(player) && _actors.Values.Any(a => !a.Dead
                && SohrKhaiCatalog.InGroup(0x1604, a.Oid) && arena.Contains(a.Position)),
            0x160E => _actors.Values.Any(a => !a.Dead && a.InCombat
                && SohrKhaiCatalog.InGroup(0x160E, a.Oid) && arena.Contains(a.Position)),
            0x1612 => _actors.Values.Any(a => !a.Dead && a.InCombat && a.Oid is 0x1611 or 0x1613),
            _ => false,
        };
    }

    private bool SohrTrashAlive()
    {
        if (!IsSohrTrash) return false;
        return Active!.BossOid switch
        {
            0x1604 => _actors.Values.Any(a => !a.Dead && SohrKhaiCatalog.InGroup(0x1604, a.Oid) && Arena!.Contains(a.Position)),
            0x160E => _actors.Values.Any(a => !a.Dead && SohrKhaiCatalog.InGroup(0x160E, a.Oid)),
            // The hidden primary must be destroyed, not merely dead, before this phase ends.
            0x1612 => _actors.ContainsKey(PrimaryId) || _actors.Values.Any(a => a.Oid == 0x1611 && !a.Dead),
            _ => false,
        };
    }

    private bool SohrTrashMember(DungeonActor actor) => IsSohrTrash && Active!.BossOid != 0x1612 && !actor.Dead
        && SohrKhaiCatalog.InGroup(Active.BossOid, actor.Oid) && (Active.BossOid != 0x1604 || Arena!.Contains(actor.Position));

    private void SohrCreated(ulong id, DateTime timestamp)
    {
        if (territory != SohrKhaiCatalog.TERRITORY) return;
        if (!sohrBirths.TryGetValue(id, out var old) || timestamp < old) sohrBirths[id] = timestamp;
        if (sohrCross?.Source == id) sohrCross = sohrCross with { StartedAt = sohrBirths[id], FinishAt = sohrBirths[id].AddSeconds(6.3) };
        if (sohrCloud?.Source == id)
        {
            var predictedBuster = sohrCloud.StartedAt.AddSeconds(4);
            for (var i = 0; i < sohrBusters.Count; ++i)
                if (sohrBusters[i].Activation == predictedBuster)
                    sohrBusters[i] = sohrBusters[i] with { Activation = sohrBirths[id].AddSeconds(4) };
            sohrCloud = sohrCloud with { StartedAt = sohrBirths[id], FinishAt = sohrBirths[id].AddSeconds(7.8) };
        }
    }

    private void SohrCast(DungeonPendingCast cast)
    {
        switch (cast.Rule.Action)
        {
            case 6017: sohrCleave = true; break;
            case 6016: sohrPraise[cast.Source] = cast.Location; break;
            case 6011: sohrBright = cast; break;
            case 5823:
                sohrKnockbacks[cast.Source] = cast;
                if (sohrKnockbackGoals.Count == 0 && sohrRemovedWalls is > 0 and < 255)
                    for (var i = 0; i < 8; ++i)
                    {
                        if ((sohrRemovedWalls & 1 << i) != 0) continue;
                        var rotation = (i < 4 ? -89.982f : 89.977f) * MathF.PI / 180;
                        var dir = new Vector3(MathF.Sin(rotation), 0, MathF.Cos(rotation));
                        var wall = new Vector3(i < 4 ? 395.5f : 404.5f, cast.Location.Y, 89.161f + 10 * (i % 4));
                        sohrKnockbackGoals.Add((wall - dir * 1.25f, rotation, cast.FinishAt));
                    }
                break;
            case 32132: sohrStack = cast; sohrStackHits = 0; break;
            case 32129:
                if (_actors.TryGetValue(cast.Source, out var orb))
                    sohrOrbs[cast.Source] = (orb.Position,
                        new Vector3(MathF.Sin(orb.Rotation), 0, MathF.Cos(orb.Rotation)) * 6.8f, cast.FinishAt, 5);
                break;
            case 6241:
                sohrTouchdown = cast with
                {
                    Location = new(364.523f, cast.Location.Y, -225.727f),
                    Rule = new(564, 0x1611, "Touchdown", new CircleTemplate(10)), FinishAt = cast.FinishAt.AddSeconds(6.7),
                };
                break;
        }
    }

    private void SohrFinish(DungeonPendingCast cast, bool completed, DateTime timestamp)
    {
        switch (cast.Rule.Action)
        {
            case 6015: sohrPraise.Clear(); break;
            case 6010: sohrCloud = null; break;
            case 5823:
                sohrKnockbacks.Clear();
                sohrKnockbackGoals.Clear();
                if (completed && !sohrFirstKnockback) { SohrAddBuster(timestamp.AddSeconds(7)); sohrFirstKnockback = true; }
                break;
            case 6011 when !completed: sohrBright = null; break;
            case 32132 when !completed: sohrStack = null; sohrStackHits = 0; break;
            case 32129 when !completed: sohrOrbs.Remove(cast.Source); break;
            case 6241 when !completed: sohrTouchdown = null; break;
        }
    }

    private void SohrAddBuster(DateTime activation)
    {
        if (_actors.TryGetValue(PrimaryId, out var boss) && _actors.TryGetValue(boss.TargetId, out var target) && !target.Dead)
            sohrBusters.Add(new(target.Id, target.PartySlot, target.PartyRole, target.Name, activation, "Tankbuster"));
    }

    private void SohrIcon(ulong target, uint icon, DateTime timestamp)
    {
        if (!_actors.TryGetValue(target, out var actor) || actor.Dead) return;
        if (Active?.BossOid == 0x155C && icon == 24) sohrCloudBaits[target] = timestamp.AddSeconds(4.9);
        if (Active?.BossOid == 0x3D17 && icon == 311) sohrSpreads[target] = timestamp.AddSeconds(6);
    }

    private void SohrEffect(ulong source, uint action, ulong target, DateTime timestamp)
    {
        if (Active?.BossOid == 0x15FB)
        {
            if (action == 6212) sohrCross = null;
            if (action == 6019) sohrCleave = false;
        }
        if (Active?.BossOid == 0x155C)
        {
            if (action == 6011) sohrBright = null;
            if (action == 6013) sohrBusters.Clear();
        }
        if (Active?.BossOid == 0x1612 && action == 564) sohrTouchdown = null;
        if (Active?.BossOid != 0x3D17) return;
        if (action == 32139) sohrSpreads.Remove(target);
        if (action is 32132 or 32133 && sohrStack != null && ++sohrStackHits == (sohrFirstStack ? 5 : 6))
        {
            sohrStack = null; sohrStackHits = 0; sohrFirstStack = false;
        }
        if (action is 32129 or 32130 && _actors.TryGetValue(source, out var orb))
            foreach (var (id, line) in sohrOrbs.ToArray())
                if (Vector2.DistanceSquared(new(line.Next.X, line.Next.Z), new(orb.Position.X, orb.Position.Z)) <= 1)
                {
                    if (line.Remaining == 1) sohrOrbs.Remove(id);
                    else sohrOrbs[id] = (orb.Position + line.Step, line.Step, timestamp.AddSeconds(1), line.Remaining - 1);
                    break;
                }
    }

    private void SohrAnimation(ulong source, uint state)
    {
        if (territory != SohrKhaiCatalog.TERRITORY || !_actors.TryGetValue(source, out var actor)) return;
        if (actor.Oid == 0x1EA032 && state == 0x00040008 && !sohrBridge)
        {
            sohrBridge = true;
            if (Active?.BossOid == 0x1612) Arena = SohrKhaiArenas.BlizzardBridge(actor.Position.Y);
        }
        if (Active?.BossOid != 0x155C || state != 0x00100020) return;
        var index = actor.Oid switch
        {
            >= 0x1EA07A and <= 0x1EA07D => (int)(actor.Oid - 0x1EA07A),
            >= 0x1EA076 and <= 0x1EA079 => 4 + (int)(actor.Oid - 0x1EA076),
            _ => -1,
        };
        if (index < 0 || (sohrRemovedWalls & 1 << index) != 0) return;
        sohrRemovedWalls |= 1 << index;
        Arena = SohrKhaiPoqhirajArena.Create(sohrRemovedWalls, actor.Position.Y);
    }

    private void SynchronizeSohr()
    {
        if (territory != SohrKhaiCatalog.TERRITORY) return;
        if (Active?.BossOid == 0x155C && !sohrPoqStarted)
        {
            sohrPoqStarted = true;
            SohrAddBuster(_now.AddSeconds(6.1));
        }
        foreach (var actor in _actors.Values.Where(a => !a.Dead && a.Oid is 0x1603 or 0x155D))
        {
            if (!sohrSeen.Add(actor.Id)) continue;
            var birth = sohrBirths.GetValueOrDefault(actor.Id, _now);
            if (Active?.BossOid == 0x15FB && actor.Oid == 0x1603)
                sohrCross = new(actor.Id, new(6212, 0x1603, "PomBom", new DungeonCrossTemplate(40.5f, 2)),
                    QuantizedDungeonPosition(actor.Position), 0, birth, birth.AddSeconds(6.3));
            if (Active?.BossOid == 0x155C && actor.Oid == 0x155D)
            {
                sohrCloud = new(actor.Id, new(6010, 0x155D, "LightningBolt", new CircleTemplate(8)),
                    actor.Position, 0, birth, birth.AddSeconds(7.8));
                sohrCloudBaits.Clear();
                SohrAddBuster(birth.AddSeconds(4));
            }
        }
        foreach (var id in sohrSeen.Where(id => !_actors.ContainsKey(id)).ToArray()) { sohrSeen.Remove(id); sohrBirths.Remove(id); }
        foreach (var id in sohrCloudBaits.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray()) sohrCloudBaits.Remove(id);
        foreach (var id in sohrSpreads.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray()) sohrSpreads.Remove(id);
        if (sohrBright != null && (!_actors.TryGetValue(sohrBright.TargetId, out var target) || target.Dead)) sohrBright = null;
        if (sohrStack != null && (!_actors.TryGetValue(sohrStack.TargetId, out var stackTarget) || stackTarget.Dead)) sohrStack = null;
        sohrBusters.RemoveAll(b => !_actors.TryGetValue(b.TargetId, out var t) || t.Dead);
    }

    private IEnumerable<DungeonActor> SohrRelevantMoogles => Active?.BossOid == 0x15FB
        ? _actors.Values.Where(a => SohrKhaiCatalog.IsMoogle(a.Oid) && HasStatus(a, 1064)
            && sohrPraise.Values.Any(p => Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(p.X, p.Z)) <= 4.9f * 4.9f)) : [];

    private int SohrPriority(DungeonActor actor)
    {
        if (HasStatus(actor, 325)) return 0;
        if (SohrRelevantMoogles.Any()) return SohrRelevantMoogles.Any(a => a.Id == actor.Id) ? 3 : 0;
        if (actor.Oid == 0x15FC) return 2;
        if (SohrKhaiCatalog.IsMoogle(actor.Oid) || actor.Oid == 0x15FB) return 1;
        return 0;
    }

    private IEnumerable<string> SohrHints()
    {
        if (SohrRelevantMoogles.Any()) yield return "将倒地莫古力推出复活圈";
        if (sohrStack != null) yield return $"死亡轮回：保持四人分摊，剩余 {(sohrFirstStack ? 5 : 6) - sohrStackHits} 次";
        if (sohrCloudBaits.ContainsKey(PlayerId)) yield return "将云诱导至边墙，避开墙体碰撞范围及队友";
        if (Active?.BossOid == 0x3D17 && _actors.TryGetValue(PlayerId, out var player) && HasStatus(player, 911))
        {
            yield return "薄冰：移动将滑行11码，只在原地或滑行可达环站位";
            if (Arena != null && (!LegacyMovement || CameraAzimuth != null))
            {
                var rotation = LegacyMovement ? CameraAzimuth!.Value + MathF.PI : player.Rotation;
                var landing = DungeonWallLanding(Arena, player.Position, new Vector3(MathF.Sin(rotation), 0, MathF.Cos(rotation)) * 11);
                if (_casts.Values.Any(c => c.Rule.Action is 32134 or 32135 && c.Rule.Shape!.Resolve(c.Location, c.Rotation, null).Contains(landing)))
                    yield return "薄冰预测落点处于危险区";
            }
        }
    }

    private void ResetSohr()
    {
        sohrBirths.Clear(); sohrSeen.Clear(); sohrCross = null; sohrCleave = true; sohrPraise.Clear();
        sohrCloud = null; sohrCloudBaits.Clear(); sohrBright = null; sohrBusters.Clear();
        sohrPoqStarted = false; sohrFirstKnockback = false; sohrRemovedWalls = 0; sohrKnockbackGoals.Clear(); sohrKnockbacks.Clear();
        sohrStack = null; sohrStackHits = 0; sohrFirstStack = true; sohrSpreads.Clear(); sohrOrbs.Clear(); sohrTouchdown = null;
    }
}
