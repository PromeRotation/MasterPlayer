using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool arbAntFirstOpen;
    private bool arbAntSecondOpen;
    private bool arbNarbrooiOpen;
    private bool arbAntInSecond;
    private bool arbNarbrooiInSecond;
    private int arbLayout = -1;
    private bool IsArboretumTrash => territory == ArboretumCatalog.TERRITORY && Active != null && ArboretumCatalog.IsTrash(Active.BossOid);

    private bool ArboretumCanStart(DungeonActor actor, Vector3 player)
    {
        if (ArboretumCatalog.MALBORO.Contains(actor.Oid))
            return !malboroComplete && _actors.Values.Any(a => ArboretumCatalog.MALBORO.Contains(a.Oid) && a.InCombat);
        if (ArboretumCatalog.ANTS.Contains(actor.Oid))
            return !ArboretumCatalog.QueenArena.Contains(player)
                && _actors.Values.Any(a => ArboretumCatalog.ANTS.Contains(a.Oid) && !a.Dead && a.InCombat);
        if (ArboretumCatalog.NARBROOI.Contains(actor.Oid))
            return _actors.Values.Any(a => ArboretumCatalog.NARBROOI.Contains(a.Oid) && !a.Dead && a.InCombat);
        return actor.InCombat;
    }

    private bool ArboretumTrashMember(DungeonActor actor)
        => IsArboretumTrash && !actor.Dead
            && (Active!.BossOid switch
            {
                0x141F => ArboretumCatalog.MALBORO,
                0x1423 => ArboretumCatalog.ANTS,
                _ => ArboretumCatalog.NARBROOI,
            }).Contains(actor.Oid);

    private bool ArboretumTrashAlive(Vector3 player)
        => IsMalboro ? !malboroComplete : _actors.Values.Any(ArboretumTrashMember)
            && (Active?.BossOid != 0x1423 || !ArboretumCatalog.QueenArena.Contains(player));

    private void ArboretumAnimation(ulong source, uint state)
    {
        if (territory != ArboretumCatalog.TERRITORY || state != 0x00040008 || !_actors.TryGetValue(source, out var actor)) return;
        if (actor.Oid == 0x1E9DFA) arbAntFirstOpen = true;
        if (actor.Oid == 0x1E9DFB) arbAntSecondOpen = true;
        if (actor.Oid == 0x1E9DFC) arbNarbrooiOpen = true;
        if (actor.Oid == 0x1E9DF8 && IsMalboro) malboroOpen = true;
        UpdateArboretumTrashArena(_actors.GetValueOrDefault(PlayerId)?.Position);
    }

    private void UpdateArboretumTrashArena(Vector3? player)
    {
        if (!IsArboretumTrash || Arena == null) return;
        var layout = 0;
        if (Active!.BossOid == 0x1423)
        {
            if (arbAntFirstOpen && player is { X: > -127 } position) arbAntInSecond = position.Z < -186;
            if (arbAntSecondOpen) layout = 3;
            else if (arbAntInSecond && arbAntFirstOpen) layout = 2;
            else if (arbAntFirstOpen) layout = 1;
        }
        else if (IsMalboro)
        {
            if (malboroOpen && player is { } p)
            {
                if (p.Z < 16) malboroInSecond = true;
                else if (p.Z > 16) malboroInSecond = false;
            }
            layout = !malboroOpen ? 7 : malboroInSecond ? 9 : 8;
        }
        else
        {
            if (arbNarbrooiOpen && player is { Z: > 12 } position) arbNarbrooiInSecond = position.X > -101;
            if (arbNarbrooiInSecond && arbNarbrooiOpen) layout = 6;
            else if (arbNarbrooiOpen) layout = 5;
            else layout = 4;
        }
        if (arbLayout == layout) return;
        arbLayout = layout;
        var y = Arena.Position.Y;
        Arena = layout switch
        {
            0 => ArboretumArenas.AntFirst(y), 1 => ArboretumArenas.AntFirstOpen(y),
            2 => ArboretumArenas.AntSecond(y), 3 => ArboretumArenas.AntSecondOpen(y),
            4 => ArboretumArenas.NarbrooiFirst(y), 5 => ArboretumArenas.NarbrooiFirstOpen(y),
            7 => MalboroArenas.First(y), 8 => MalboroArenas.Open(y), 9 => MalboroArenas.Second(y),
            _ => ArboretumArenas.NarbrooiSecond(y),
        };
    }

    private IEnumerable<DungeonAmbientAoe> ArboretumTrashAoes()
    {
        if (Active?.BossOid != 0x1423) yield break;
        foreach (var crawler in _actors.Values.Where(a => a.Oid == 0x1425 && !a.Dead && a.Targetable))
        {
            if (!_actors.TryGetValue(crawler.TargetId, out var target)) continue;
            var rotation = MathF.Atan2(target.Position.X - crawler.Position.X, target.Position.Z - crawler.Position.Z);
            yield return new($"arboretum/crawler/{crawler.Id}/display", "毒气与黏丝顺劈", new FanTemplate(7.5f, MathF.PI * 2 / 3),
                crawler.Position, rotation, Navigate: false, OutlineOnly: true);
            if (target.Id != PlayerId)
                yield return new($"arboretum/crawler/{crawler.Id}/avoid", "顺劈", new FanTemplate(7.5f, MathF.PI * 2 / 3),
                    crawler.Position, rotation, Display: false);
            else
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                    yield return new($"arboretum/crawler/{crawler.Id}/{buddy.Id}", "顺劈避开队友", new FanTemplate(100, MathF.PI * 2 / 3),
                        crawler.Position, MathF.Atan2(buddy.Position.X - crawler.Position.X, buddy.Position.Z - crawler.Position.Z), Display: false);
        }
    }
}
