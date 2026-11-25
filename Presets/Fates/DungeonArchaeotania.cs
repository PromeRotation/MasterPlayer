using System.Numerics;
using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsNativeFate => NativeFateCatalog.IsTerritory(territory);
    internal bool UsesFateNativeEffect(uint action) => IsNativeFate && action is 17483 or 26518
        || IsFormidable && action is 17395 or 18002 or 17401 or 18003 or 17404 or 17405
        || IsChi && action is 25955 or 25956 or 25957 or 25958 or 25960 or 25961 or 25963 or 25964
        || IsMica && action is 38687 or 38688
        || IsTtokrrone && action is 39814 or 39843 or 39816 or 39813 or >= 38642 and <= 38646;
    private readonly HashSet<ulong> fateCompleted = [];
    private readonly List<(DungeonActor Target, DateTime At, bool Removed)> fateStacks = [];
    private readonly List<ArchaeotaniaTidalWave> fateKnockbacks = [];
    private readonly Dictionary<ulong, DungeonCastObservation> fateObservedCasts = [];
    private readonly HashSet<(ulong Source, uint Action, uint Sequence, ulong Target, DateTime At)> fateEffects = [];
    private readonly Dictionary<ulong, DungeonActor> fateCreatedActors = [];
    private readonly HashSet<(ulong Id, uint Oid)> fateCreatedReplacedOids = [];
    private bool fateEnabled;

    private void FateCreated(ulong id, uint oid, Vector3 position, float rotation)
    {
        if (!IsChi && !IsMica) return;
        var old = fateCreatedActors.GetValueOrDefault(id) ?? _actors.GetValueOrDefault(id);
        if (old != null && old.Oid != oid) fateCreatedReplacedOids.Add((id, old.Oid));
        var actor = new DungeonActor(id, oid, position, Rotation: rotation);
        fateCreatedActors[id] = actor;
        ChiCreated(actor, observed: false);
        MicaCreated(actor);
    }

    private DungeonActor FateCreatedActor(DungeonActor raw)
    {
        if (!fateCreatedActors.TryGetValue(raw.Id, out var created)) return raw;
        // A create packet already identifies the new object; a lagging sample of its old OID cannot replace it.
        if (created.Oid != raw.Oid && fateCreatedReplacedOids.Contains((raw.Id, raw.Oid))) return created;
        fateCreatedActors.Remove(raw.Id);
        fateCreatedReplacedOids.RemoveWhere(a => a.Id == raw.Id);
        return raw;
    }

    private void ObserveFate(Func<string, bool>? enabled, Vector3? player)
    {
        if (!IsNativeFate) return;
        DaivadipaObserveActors();
        MicaObserveActors();
        ChiObserveMissing();
        fateEnabled = enabled?.Invoke(Definitions[0].Name) != false;
        fateCompleted.RemoveWhere(id => !_actors.ContainsKey(id));
        foreach (var actor in _actors.Values.Where(a => NativeFateCatalog.Owns(territory, a.Oid)))
            if ((Active != null && actor.Id == PrimaryId || fateEnabled && player is { } position && FatePull(actor, position))
                && (actor.Dead || actor.Hp == 0))
                fateCompleted.Add(actor.Id);
    }

    private bool FatePull(DungeonActor actor, Vector3 player)
    {
        var origin = _actors.Values.FirstOrDefault(a => a.PartySlot == 0)?.Position ?? player;
        var center = NativeFateCatalog.Center(territory);
        var radiusSquared = NativeFateCatalog.PullDistanceSquared(territory);
        return actor.Targetable && actor.InCombat
            && (IsFormidable || Vector2.DistanceSquared(new(origin.X, origin.Z), new(center.X, center.Z)) < radiusSquared);
    }

    private void SynchronizeFate()
    {
        if (!IsNativeFate || Active == null) return;
        FormidableObserve();
        FateBaitObserve();
        if (IsDaivadipa)
        {
            daivadipaStatuses.Observe(_actors.ToDictionary(p => p.Key, p => TreasureStatusActor(p.Value)), _now);
            return;
        }
        for (var i = 0; i < fateStacks.Count; ++i)
        {
            var mark = fateStacks[i];
            if (!mark.Removed && _actors.TryGetValue(mark.Target.Id, out var actor))
                fateStacks[i] = (actor, mark.At, false);
            else if (!mark.Removed)
                fateStacks[i] = (mark.Target, mark.At, true);
        }
        // This fallback is conditional on any primary cast, not on the Megaflare action alone.
        if (territory == NativeFateCatalog.ARCHAEOTANIA && _actors.GetValueOrDefault(PrimaryId)?.Cast == null && !_casts.ContainsKey(PrimaryId))
            fateStacks.RemoveAll(s => !_actors.Values.Any(a => a.Oid == 0x2AA0 && !a.Dead && a.TargetId == s.Target.Id));
    }

    private void FateIcon(ulong actorId, uint icon, DateTime at)
    {
        if (territory is not (NativeFateCatalog.ARCHAEOTANIA or NativeFateCatalog.FORMIDABLE) || Active == null || icon != 62
            || !_actors.TryGetValue(actorId, out var actor)) return;
        fateStacks.Add((actor, at.AddSeconds(5), false));
    }

    private void FateCast(DungeonPendingCast cast, bool correction, DungeonPendingCast? previous)
    {
        DaivadipaCast(cast, correction);
        FormidableCast(cast, correction);
        ChiCast(cast, correction, previous);
        MicaCast(cast, correction, previous);
        TtokrroneCast(cast, correction, previous);
        FateBaitCast(cast, correction);
        if (!IsNativeFate || cast.Rule.Action != 16452) return;
        var index = fateKnockbacks.FindIndex(c => c.Cast.Source == cast.Source);
        if (index >= 0)
            fateKnockbacks[index] = new(cast);
        else
            fateKnockbacks.Add(new(cast));
    }

    private void FateFinish(DungeonPendingCast cast, bool correction = false)
    {
        if (correction) return;
        DaivadipaFinish(cast);
        FormidableFinish(cast, correction);
        ChiFinish(cast);
        MicaFinish(cast);
        TtokrroneFinish(cast);
        FateBaitFinish(cast);
        if (!IsNativeFate || cast.Rule.Action != 16452) return;
        var index = fateKnockbacks.FindIndex(c => c.Cast.Source == cast.Source);
        if (index >= 0) fateKnockbacks.RemoveAt(index);
    }

    private void FateRemoved(ulong id)
    {
        if (!IsNativeFate) return;
        if (IsFormidable) FormidableRemoved(id);
        fateCreatedActors.Remove(id);
        fateCreatedReplacedOids.RemoveWhere(a => a.Id == id);
        chiCreated.Remove(id);
        chiObservedCreated.Remove(id);
        micaActorOrder.RemoveAll(a => a.Id == id);
        micaObservedActors.Remove(id);
        FateBaitRemoved(id);
        if (IsDaivadipa)
        {
            daivadipaStatuses.Remove(id, _now);
            foreach (var order in daivadipaOrbOrder.Values) order.Remove(id);
        }
        fateCompleted.Remove(id);
        for (var i = 0; i < fateStacks.Count; ++i)
            if (fateStacks[i].Target.Id == id)
                fateStacks[i] = (fateStacks[i].Target, fateStacks[i].At, true);
    }

    private void FateEffect(ulong source, uint action, uint sequence, ulong target, DateTime at)
    {
        if (!UsesFateNativeEffect(action) || Active == null
            || !fateEffects.Add((source, action, sequence, target, sequence == 0 ? at : default))) return;
        if (territory == NativeFateCatalog.ARCHAEOTANIA && action == 17483)
            fateStacks.RemoveAll(s => s.Target.Id == target);
        if (IsDaivadipa && action == 26518) daivadipaRaidwide = null;
        FormidableEffect(source, action, target);
        ChiEffect(action);
        MicaEffect(action);
        TtokrroneEffect(action);
    }

    private IEnumerable<DungeonAmbientAoe> FateAoes()
    {
        if (!IsNativeFate || !fateEnabled) yield break;
        var primary = Active != null ? _actors.GetValueOrDefault(PrimaryId)
            : _actors.Values.FirstOrDefault(a => NativeFateCatalog.Owns(territory, a.Oid) && !fateCompleted.Contains(a.Id));
        if (primary == null || primary.Dead) yield break;
        if (IsFormidable)
        {
            foreach (var aoe in FormidableAoes(primary)) yield return aoe;
            yield break;
        }
        if (IsTtokrrone)
        {
            foreach (var aoe in TtokrroneArenaAoes(primary)) yield return aoe;
            foreach (var aoe in TtokrroneAoes()) yield return aoe;
            yield break;
        }
        var center = NativeFateCatalog.Center(territory) with { Y = primary.Position.Y };
        var halfSize = NativeFateCatalog.HalfSize(territory);
        var halfX = halfSize.X;
        var halfZ = halfSize.Y;
        if (Active == null)
            yield return new("fate/precombat/arena", "场地", new RectangleTemplate(halfX * 2, halfZ * 2), center,
                Navigate: false, OutlineOnly: true);
        var enemies = Active == null || territory != NativeFateCatalog.ARCHAEOTANIA ? new[] { primary }
            : _actors.Values.Where(a => a.Id == PrimaryId || a.Oid is 0x2A8E or 0x2A8F);
        var marker = new LocalPolygonTemplate([new(0, -.7f), new(.433f, .35f), new(-.433f, .35f)]);
        foreach (var enemy in enemies.Where(a => !a.Dead && a.Targetable))
        {
            var offset = enemy.Position - center;
            var outside = MathF.Abs(offset.X) > halfX || MathF.Abs(offset.Z) > halfZ;
            var position = new Vector3(center.X + Math.Clamp(offset.X, -halfX, halfX), enemy.Position.Y,
                center.Z + Math.Clamp(offset.Z, -halfZ, halfZ));
            yield return new($"fate/enemy/{enemy.Id}", "敌人及朝向", marker, position, enemy.Rotation,
                Navigate: false, OutlineOnly: outside, Unclipped: true, Color: new Vector4(1, 0, 0, 1));
        }
        if (Active == null || Arena == null) yield break;
        if (IsChi || IsMica)
        {
            foreach (var aoe in IsChi ? ChiAoes() : MicaAoes()) yield return aoe;
            foreach (var aoe in FateBaitAoes()) yield return aoe;
            yield break;
        }
        if (IsDaivadipa)
        {
            foreach (var aoe in DaivadipaAoes()) yield return aoe;
            yield break;
        }
        foreach (var twister in _actors.Values.Where(a => a.Oid == 0x27B2 && !a.Dead))
            yield return new($"fate/twister/{twister.Id}", "Windwinder", new CircleTemplate(5),
                twister.Position, Obstacle: true);
        foreach (var aoe in FateStackAoes()) yield return aoe;
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var from = player.Position;
        foreach (var wave in fateKnockbacks)
        {
            var cast = wave.Cast;
            if (KnockbackImmune(player, cast.FinishAt)) continue;
            var to = wave.Destination(from);
            foreach (var aoe in TrialKnockbackDrawing($"fate/tidal/{cast.Source}", "海啸48码", from, to))
                yield return aoe with { Navigate = false };
            from = to;
        }
    }

    private DungeonActor[] FateParty => _actors.Values.Where(a => a.PartySlot is >= 0 and < 8 && !a.Dead).ToArray();

    private IEnumerable<DungeonAmbientAoe> FateStackAoes()
    {
        if (Arena == null) yield break;
        var stacks = fateStacks.Where(s => !s.Target.Dead).ToArray();
        var party = FateParty;
        var marked = fateStacks.Any(s => !s.Removed && s.Target.Id == PlayerId);
        var joins = new List<IGeometryShape>();
        foreach (var (stack, index) in stacks.Select((s, i) => (s, i)))
        {
            var position = QuantizedDungeonPosition(stack.Target.Position);
            var self = !stack.Removed && stack.Target.Id == PlayerId;
            yield return new($"fate/stack/{index}/display", IsFormidable ? "Drill Shot / 至少2人分摊" : "Megaflare / 至少8人分摊", new CircleTemplate(6),
                position, Navigate: false, OutlineOnly: true, Safe: self || !marked, Activation: stack.At);
            if (party.Length < 2) continue;
            if (self)
            {
                var buddies = party.Where(a => a.Id != PlayerId && !fateStacks.Any(s => !s.Removed && s.Target.Id == a.Id))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
                if (buddies.Length > 0)
                    yield return new($"fate/stack/{index}/buddy", "分摊目标靠拢队友",
                        new SohrKhaiWallHintTemplate(Arena, buddies), Arena.Position, Display: false, Activation: stack.At);
            }
            else
                joins.Add(new CircleGeometry(position, 6));
        }
        if (joins.Count > 0)
            yield return new("fate/stack/join", "进入任一分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: stacks[0].At);
    }

    private IEnumerable<string> FateHints(Vector3? position)
    {
        if (!IsNativeFate || Active == null || position is not { } player) yield break;
        if (IsTtokrrone)
        {
            foreach (var hint in TtokrroneHints(player)) yield return hint;
            yield break;
        }
        if (IsChi || IsMica)
        {
            foreach (var hint in IsChi ? ChiHints(player) : MicaHints(player)) yield return hint;
            foreach (var hint in FateBaitHints(player)) yield return hint;
            yield break;
        }
        if (IsDaivadipa)
        {
            foreach (var hint in DaivadipaHints(player)) yield return hint;
            yield break;
        }
        if (IsFormidable)
            foreach (var hint in FormidableHints(player)) yield return hint;
        if (!IsFormidable && _actors.Values.Any(a => a.Oid == 0x27B2 && !a.Dead
            && new CircleGeometry(a.Position, 5).Contains(player, 0)))
            yield return "GTFO from voidzone!";
        var stacks = fateStacks.Where(s => !s.Target.Dead).ToArray();
        var party = FateParty;
        var self = stacks.FirstOrDefault(s => !s.Removed && s.Target.Id == PlayerId);
        if (self.Target != null)
        {
            var circle = new CircleGeometry(QuantizedDungeonPosition(self.Target.Position), 6);
            var count = 1 + party.Count(a => a.Id != PlayerId && circle.Contains(a.Position, 0));
            var overlap = party.Any(a => a.Id != PlayerId && circle.Contains(a.Position, 0)
                && fateStacks.Any(s => !s.Removed && s.Target.Id == a.Id));
            var minimum = IsFormidable ? 2 : 8;
            yield return overlap || count < minimum ? $"Stack! ({count}/{minimum}+)" : $"Stack ({count}/{minimum}+)";
        }
        else
        {
            var inside = stacks.Count(s => new CircleGeometry(QuantizedDungeonPosition(s.Target.Position), 6).Contains(player, 0));
            var unsatisfied = stacks.Any(s => 1 + party.Count(a => (s.Removed || a.Id != s.Target.Id)
                && new CircleGeometry(QuantizedDungeonPosition(s.Target.Position), 6).Contains(a.Position, 0)) < (IsFormidable ? 2 : 8));
            if (inside > 1 || inside == 0 && unsatisfied) yield return "Stack!";
            else if (inside == 1) yield return "Stack";
        }
        if (_actors.GetValueOrDefault(PlayerId) is not { } viewer) yield break;
        var from = player;
        foreach (var wave in fateKnockbacks)
        {
            var cast = wave.Cast;
            if (KnockbackImmune(viewer, cast.FinishAt)) continue;
            from = wave.Destination(from);
            if (Arena?.Contains(from, 0) == false)
            {
                yield return "Knockback into danger!";
                break;
            }
        }
    }

    private ulong FatePriorityTarget(ulong current, Vector3 position)
    {
        if (Active == null) return 0;
        var actors = _actors.Values.Select(a => new ReplayActorState
        {
            Id = a.Id, Oid = a.Oid, Position = a.Position, Kind = a.Player ? (byte)1 : (byte)2,
            IsEnemy = a.IsEnemy, ObjectIndex = a.ObjectIndex, Targetable = a.Targetable, Dead = a.Dead,
            Hp = a.Hp ?? uint.MaxValue, NameId = a.NameId, TargetId = a.TargetId, AggroPlayer = a.AggroPlayer,
            InCombat = a.InCombat, FateId = a.FateId, IncomingEffects = a.IncomingEffects?.ToArray() ?? []
        });
        return QuestTargetPriority.Select(territory, actors, current, position,
            _actors.Values.Where(a => a.PartySlot >= 0).Select(a => a.Id).ToHashSet(), AllowedFateId);
    }
}
