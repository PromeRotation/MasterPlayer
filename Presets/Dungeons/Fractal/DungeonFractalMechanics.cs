using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private ushort fractalBrokenTiles;
    private readonly List<Vector3> fractalMines = [];
    private readonly Dictionary<ulong, DateTime> fractalBombs = [];
    private readonly HashSet<ulong> fractalSeenBombs = [];
    private bool IsFractalTrash => territory == FractalCatalog.TERRITORY && Active != null && FractalCatalog.IsTrash(Active.BossOid);

    private bool FractalCanStart(DungeonActor primary, Vector3 player)
    {
        if (territory != FractalCatalog.TERRITORY || !FractalCatalog.IsTrash(primary.Oid)) return primary.InCombat;
        var arena = Definitions.First(d => d.BossOid == primary.Oid).Arena(primary.Position);
        if (primary.Oid is 0x1E990E or 0x1E9910
            && (MathF.Abs(player.X - arena.Position.X) > 70 || MathF.Abs(player.Z - arena.Position.Z) > 70)) return false;
        var group = primary.Oid switch
        {
            0x1020 => FractalCatalog.CLOCKWORK, 0x101D => FractalCatalog.MANIKIN, 0x1023 => FractalCatalog.NAGA,
            0x1E990E => FractalCatalog.EMERALD, 0x1E9910 => FractalCatalog.CRIMSON, _ => [0x102Bu],
        };
        return primary.EventState != 7 && _actors.Values.Any(a => !a.Dead && a.InCombat && group.Contains(a.Oid)
            && (primary.Oid != 0x101D || arena.Contains(a.Position)));
    }

    private bool FractalTrashMember(DungeonActor actor)
    {
        if (!IsFractalTrash) return false;
        return Active!.BossOid switch
        {
            0x1020 => !actor.Dead && FractalCatalog.CLOCKWORK.Contains(actor.Oid) && Arena!.Contains(actor.Position),
            0x101D => !actor.Dead && FractalCatalog.MANIKIN.Contains(actor.Oid),
            0x1023 => actor.Oid is 0x1023 or 0x1021 || !actor.Dead && FractalCatalog.NAGA.Contains(actor.Oid),
            _ => actor.Id == PrimaryId,
        };
    }

    private bool FractalEncounterAlive(Vector3 player)
    {
        if (!IsFractalTrash) return false;
        if (FractalCatalog.IsControl(Active!.BossOid))
            return _actors.TryGetValue(PrimaryId, out var control) && control.EventState != 7
                && (control.Oid == 0x1E990F || MathF.Abs(player.X - Arena!.Position.X) <= 70 && MathF.Abs(player.Z - Arena.Position.Z) <= 70);
        return _actors.Values.Any(FractalTrashMember);
    }

    internal DungeonActor? FractalInteractionTarget
    {
        get
        {
            if (Active?.BossOid == 0x1013 && _casts.Values.Any(c => c.Rule.Action == 3975))
            {
                var position = _actors.GetValueOrDefault(PlayerId)?.Position ?? _actors[PrimaryId].Position;
                return _actors.Values.Where(a => !a.Dead && a.Targetable && a.EventState != 7 && FractalCatalog.IsInteraction(a.Oid)
                    && !FractalCatalog.IsControl(a.Oid)).OrderBy(a => Vector3.DistanceSquared(a.Position, position)).FirstOrDefault();
            }
            if (Active != null && FractalCatalog.IsControl(Active.BossOid) && _actors.GetValueOrDefault(PrimaryId) is { Targetable: true, EventState: not 7 } control)
                return control;
            return null;
        }
    }

    private void FractalCast(DungeonPendingCast cast)
    {
        if (Active?.BossOid != 0x1018) return;
        if (cast.Rule.Action == 3981)
        {
            var index = FractalFloorBoundary.CellIndex(cast.Location);
            for (var i = 0; i < 4; ++i)
                fractalBrokenTiles |= (ushort)((1 << ((index & 3) | i << 2)) | (1 << ((index & 12) | i)));
            Arena = FractalFloorBoundary.Create(cast.Location.Y, fractalBrokenTiles);
        }
        else if (cast.Rule.Action == 3986)
            _casts[cast.Source] = cast with { Location = FractalFloorBoundary.CellCenter(FractalFloorBoundary.CellIndex(cast.Location), cast.Location.Y) };
    }

    public void DirectorUpdate(uint update, uint param1 = 0, uint param2 = 0, uint param3 = 0, uint param4 = 0)
    {
        TacticalDirector(update, param1, param2, param3, param4);
        PalaceGuideDirector(update);
        EurekaDirector(update);
        NecronDirector(update, param1);
        ViperDirector(update);
        SlickshellDirector(update);
        MaxQuestDirector(update);
        HopeDirector(update);
        TimeBetweenDirector(update);
        HeroesPretendersDirector(update);
        IseultalonDirector(update);
        AlphaDirector(update);
        CarnivaleDirector(update);
        BarPassageDirector(update);
        DikaiosyneDirector(update);
        ZoraalDirector(update);
        DoomDirector(update);
        CoilDirector(update);
        GauntletDirector(update);
        TwinDirector(update);
        LabyrinthDirector(update);
        SyrcusDirector(update);
        DarknessDirector(update);
        ArrQuestDirector(update);
        UltimaDirector(update);
        GarudaExDirector(update);
        TitanExDirector(update);
        IfritExDirector(update);
        if (Active?.BossOid != 0x1018 || update == 0x80000004 || fractalBrokenTiles == 0) return;
        fractalBrokenTiles = 0;
        Arena = FractalFloorBoundary.Create(Arena!.Position.Y, 0);
    }

    private void FractalEffect(ulong source, uint action)
    {
        if (Active?.BossOid != 0x1018 || !_actors.TryGetValue(source, out var actor)) return;
        if (action == 3986)
        {
            fractalBrokenTiles |= (ushort)(1 << FractalFloorBoundary.CellIndex(actor.Position));
            Arena = FractalFloorBoundary.Create(actor.Position.Y, fractalBrokenTiles);
        }
        if (action == 3983) fractalMines.Add(actor.Position);
        if (action == 3984) FractalRemoveMine(actor.Position);
    }

    private void FractalRemoveMine(Vector3 position)
    {
        var index = fractalMines.FindIndex(p => p.X == position.X && p.Z == position.Z);
        if (index >= 0) fractalMines.RemoveAt(index);
    }

    private void FractalStatus(ulong target, uint status, bool present, DateTime timestamp)
    {
        if (Active?.BossOid != 0x1018 || status != 723) return;
        if (present)
        {
            fractalSeenBombs.Add(target);
            fractalBombs[target] = timestamp.AddSeconds(6);
        }
        else
        {
            fractalBombs.Clear();
        }
    }

    private void SynchronizeFractal()
    {
        if (Active?.BossOid != 0x1018) return;
        foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && HasStatus(a, 723)))
            if (fractalSeenBombs.Add(member.Id)) fractalBombs.TryAdd(member.Id, _now.AddSeconds(6));
        // A late pre-cleanse snapshot must not recreate spreads cleared by StatusLose.
        fractalSeenBombs.RemoveWhere(id => !_actors.TryGetValue(id, out var a) || a.Statuses?.Contains(723u) != true);
        foreach (var id in fractalBombs.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead || !HasStatus(a, 723)).ToArray())
            fractalBombs.Remove(id);
    }

    internal DungeonActor? FractalCleanseTarget(uint job)
    {
        if (Active?.BossOid != 0x1018 || job is not (6 or 24 or 28 or 33 or 40 or 23)
            || !_actors.TryGetValue(PlayerId, out var player)) return null;
        return _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Targetable && HasStatus(a, 723)
            && Vector3.DistanceSquared(player.Position, a.Position) <= MathF.Pow(30 + a.HitboxRadius, 2))
            .OrderBy(a => a.Id != PlayerId).ThenBy(a => a.PartySlot).FirstOrDefault();
    }

    private IEnumerable<DungeonAmbientAoe> FractalAoes()
    {
        if (Active?.BossOid == 0x1012 && _casts.Values.FirstOrDefault(c => c.Rule.Action == 3965) is { } sever)
            yield return new("fractal/sever/close", "贴近连斩原点", new CircleTemplate(3), sever.Location, Display: false, Safe: true);
        if (Active?.BossOid == 0x1013 && Arena != null && _actors.TryGetValue(PlayerId, out var player))
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 3972))
            {
                if (KnockbackImmune(player, cast.FinishAt)) continue;
                var template = new VaultKnockbackTemplate(Arena, cast.Location, []);
                yield return new("fractal/groan/constraint", "击退20码贴墙停止", template, cast.Location,
                    Display: false, Activation: cast.FinishAt);
                foreach (var aoe in DungeonMovementPreview("fractal/groan", "击退20码", player.Position, template.Landing(player.Position))) yield return aoe;
            }
        if (FractalInteractionTarget is { } target)
        {
            var visible = Active!.BossOid == 0x1013
                ? _actors.Values.Where(a => !a.Dead && a.Targetable && a.EventState != 7
                    && FractalCatalog.IsInteraction(a.Oid) && !FractalCatalog.IsControl(a.Oid))
                : [target];
            foreach (var available in visible)
                yield return new($"fractal/interact/{available.Id}/display", "交互目标", new CircleTemplate(3), available.Position,
                    Navigate: false, Safe: true, OutlineOnly: true);
            yield return new("fractal/interact/goal", "接近交互目标", new CircleTemplate(target.HitboxRadius + (Active!.BossOid == 0x1013 ? 2 : 1)),
                target.Position, Display: false, Safe: true);
        }
        if (Active?.BossOid == 0x1018)
        {
            for (var i = 0; i < fractalMines.Count; ++i)
                yield return new($"fractal/mine/{i}", "以太化学地雷", new CircleTemplate(5), fractalMines[i]);
            foreach (var (id, activation) in fractalBombs)
            {
                if (!_actors.TryGetValue(id, out var member)) continue;
                yield return new($"fractal/bomb/{id}/display", "以太化学炸弹：驱散并分散", new CircleTemplate(8),
                    member.Position, Navigate: false, OutlineOnly: true);
                if (id != PlayerId)
                    yield return new($"fractal/bomb/{id}/avoid", "炸弹分散", new CircleTemplate(8), member.Position,
                        Display: false, Activation: activation);
                else
                    foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                        yield return new($"fractal/bomb/{id}/{buddy.Id}", "炸弹远离队友", new CircleTemplate(8), buddy.Position,
                            Display: false, Activation: activation);
            }
        }
        if (Active?.BossOid is 0x1E990E or 0x1E9910)
            foreach (var reservoir in _actors.Values.Where(a => a.Oid == 0x1027 && a.EventState != 7 && !a.Dead))
                yield return new($"fractal/reservoir/{reservoir.Id}", "蓄水器", new CircleTemplate(3), reservoir.Position);
        if (Active?.BossOid == 0x1E9910)
            foreach (var source in _actors.Values.Where(a => a.Oid == 0x1029 && !a.Dead && a.Targetable))
                if (_actors.TryGetValue(source.TargetId, out var victim))
                {
                    yield return new($"fractal/infrared/{source.Id}/display", "被动红外制导", new CircleTemplate(6), victim.Position,
                        Navigate: false, OutlineOnly: true);
                    if (victim.Id != PlayerId)
                        yield return new($"fractal/infrared/{source.Id}/avoid", "避开红外溅射", new CircleTemplate(6), victim.Position, Display: false);
                    else
                        foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                            yield return new($"fractal/infrared/{source.Id}/{buddy.Id}", "红外远离队友", new CircleTemplate(6), buddy.Position, Display: false);
                }
        if (Active?.BossOid is not (0x1018 or 0x1023)) yield break;
        var casterOid = Active.BossOid == 0x1018 ? 0x1018u : 0x1024u;
        var radius = casterOid == 0x1018 ? 16.5f : 9.7f;
        foreach (var caster in _actors.Values.Where(a => a.Oid == casterOid && !a.Dead && a.Targetable && a.Cast == null && !_casts.ContainsKey(a.Id)))
        {
            if (!_actors.TryGetValue(caster.TargetId, out var victim)) continue;
            var angle = MathF.Atan2(victim.Position.X - caster.Position.X, victim.Position.Z - caster.Position.Z);
            var shape = new FanTemplate(radius, MathF.PI * 2 / 3);
            yield return new($"fractal/cleave/{caster.Id}/display", "顺劈", shape, caster.Position, angle, Navigate: false, OutlineOnly: true);
            if (victim.Id != PlayerId)
                yield return new($"fractal/cleave/{caster.Id}/avoid", "顺劈", shape, caster.Position, angle, Display: false);
            else
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                    yield return new($"fractal/cleave/{caster.Id}/{buddy.Id}", "顺劈避开队友", new FanTemplate(100, MathF.PI * 2 / 3),
                        caster.Position, MathF.Atan2(buddy.Position.X - caster.Position.X, buddy.Position.Z - caster.Position.Z), Display: false);
        }
    }
}
