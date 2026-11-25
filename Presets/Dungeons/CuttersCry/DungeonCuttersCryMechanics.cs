using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<DungeonPendingCast> sandPillars = [];
    private readonly Dictionary<ulong, DateTime> orbs = [];
    private readonly HashSet<ulong> detonatedOrbs = [];
    private ulong orbTarget;
    private static readonly (Vector2 Position, float Degrees)[] SAND_STARTS =
    [
        (new(-135.623f, 147.871f), -45), (new(-142.695f, 144.942f), 0),
        (new(-149.766f, 162.013f), 135), (new(-142.695f, 164.942f), 180),
        (new(-152.695f, 154.942f), 90), (new(-135.623f, 162.013f), -135),
        (new(-149.766f, 147.871f), 45), (new(-132.694f, 154.942f), -90),
    ];

    private void SynchronizeCuttersCry()
    {
        foreach (var id in orbs.Keys.Where(id => !_actors.TryGetValue(id, out var actor) || actor.Dead).ToArray())
            orbs.Remove(id);
        if (Active?.BossOid != 0x4900) return;
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0x4901 && !a.Dead && !detonatedOrbs.Contains(a.Id)))
            orbs.TryAdd(actor.Id, _now.AddSeconds(10.5));
    }

    public void Icon(ulong target, uint icon, DateTime? timestamp = null, ulong actorId = 0)
    {
        EurekaIcon(actorId, icon);
        CarnivalePapaIcon(actorId, target, icon, timestamp ?? _now);
        FateIcon(actorId, icon, timestamp ?? _now);
        TacticalIcon(target, icon, timestamp ?? _now, actorId is 0 or 0xE0000000 ? target : actorId);
        TreasureBaitIcon(actorId, icon);
        SlickshellIcon(target, actorId, icon);
        KenkoIcon(target, icon);
        if (icon == 129) IseultalonIcon(actorId is 0 or 0xE0000000 ? target : actorId);
        TreasureRotationIcon(actorId is 0 or 0xE0000000 ? target : actorId, icon);
        TreasureBuffetIconReceived(actorId, icon);
        FallenIcon(actorId is 0 or 0xE0000000 ? target : actorId, icon, _now);
        SusanoIcon(actorId is 0 or 0xE0000000 ? target : actorId, icon, timestamp ?? _now);
        LakshmiIcon(target, icon, timestamp ?? _now);
        ShinryuIcon(actorId is 0 or 0xE0000000 ? target : actorId, target, icon, timestamp ?? _now);
        YojimboIcon(actorId is 0 or 0xE0000000 ? target : actorId, icon, timestamp ?? _now);
        SeiryuIcon(actorId is 0 or 0xE0000000 ? target : actorId, icon, timestamp ?? _now);
        var titaniaIconTarget = actorId is 0 or 0xE0000000 ? target : actorId;
        if (icon == 230 && _actors.ContainsKey(target)) titaniaIconTarget = target;
        TitaniaIcon(titaniaIconTarget, icon, timestamp ?? _now);
        InnocenceIcon(_actors.ContainsKey(target) ? target : actorId, icon, timestamp ?? _now);
        HadesIcon(_actors.ContainsKey(target) ? target : actorId, icon, timestamp ?? _now);
        WolIcon(actorId is 0 or 0xE0000000 ? target : actorId, target, icon, timestamp ?? _now);
        HydaIcon(actorId is 0 or 0xE0000000 ? target : actorId, icon, timestamp ?? _now);
        ZeleniaIcon(actorId is 0 or 0xE0000000 ? target : actorId, icon, timestamp ?? _now);
        ValigIcon(actorId is 0 or 0xE0000000 ? target : actorId, target, icon, timestamp ?? _now);
        ArkIcon(actorId is 0 or 0xE0000000 ? target : actorId, icon, timestamp ?? _now);
        EnuoIcon(actorId is 0 or 0xE0000000 ? target : actorId, target, icon);
        NecronIcon(actorId is 0 or 0xE0000000 ? target : actorId, target, icon);
        QueenIcon(actorId is 0 or 0xE0000000 ? target : actorId, target, icon);
        ZoraalIcon(actorId is 0 or 0xE0000000 ? target : actorId, icon);
        DoomIcon(actorId is 0 or 0xE0000000 ? target : actorId, icon, target);
        AlexIcon(target, icon, timestamp ?? _now);
        TenderIcon(target, icon, timestamp ?? _now);
        StrayIcon(target, icon, timestamp ?? _now, actorId);
        UnderIcon(target, icon, timestamp ?? _now, actorId);
        MesoIcon(target, icon, timestamp ?? _now, actorId);
        MistIcon(target, icon, timestamp ?? _now, actorId);
        ClyIcon(target, icon, timestamp ?? _now, actorId);
        LapisIcon(actorId is 0 or 0xE0000000 ? target : actorId, icon, timestamp ?? _now);
        AetherfontIcon(actorId is 0 or 0xE0000000 ? target : actorId, icon, timestamp ?? _now);
        WorqorIcon(actorId is 0 or 0xE0000000 ? target : actorId, icon, timestamp ?? _now);
        BabilIcon(target, icon, timestamp ?? _now);
        KtisisIcon(target, icon, timestamp ?? _now);
        SohmIcon(target, icon, timestamp ?? _now);
        VaultIcon(target, icon, timestamp ?? _now);
        GubalIcon(target, icon, timestamp ?? _now);
        ResearchIcon(target, icon, timestamp ?? _now);
        ArboretumIcon(target, icon, timestamp ?? _now);
        AntitowerIcon(target, icon, timestamp ?? _now);
        SohrIcon(target, icon, timestamp ?? _now);
        XelIcon(target, icon, timestamp ?? _now);
        BaelIcon(target, icon);
        SirenIcon(target, icon, timestamp ?? _now);
        ShisuiIcon(target, icon, timestamp ?? _now);
        DarknessIcon(target, icon);
        BardamIcon(target, icon, timestamp ?? _now);
        DomaIcon(target, icon, timestamp ?? _now);
        SkallaIcon(target, icon, timestamp ?? _now);
        BurnIcon(target, icon, timestamp ?? _now);
        GhimlytIcon(icon);
        HolminsterIcon(target, icon, timestamp ?? _now);
        DohnIcon(target, icon, timestamp ?? _now);
        MalikahIcon(target, icon);
        AmaurotIcon(target, icon, timestamp ?? _now);
        TwinningIcon(target, icon, timestamp ?? _now);
        CosmosIcon(target, icon, timestamp ?? _now);
        HeroesIcon(target, icon, timestamp ?? _now);
        PaglthanIcon(target, icon, timestamp ?? _now);
        if (territory == MatoyaCatalog.TERRITORY && Active?.BossOid == 0x300B && icon == 169)
            matoyaSpreads[target] = (timestamp ?? _now).AddSeconds(6.7);
        if (Active?.BossOid == 0x4900 && icon == 23) orbTarget = target;
        if (Active?.BossOid == 0x41D && icon == 1) bavaroisKiter = target;
    }

    private void CuttersCryEffect(ulong source, uint action, DateTime now)
    {
        if (Active?.BossOid == 0x4900 && action == 44240)
        {
            detonatedOrbs.Add(source);
            orbs.Remove(source);
            orbTarget = 0;
        }
        if (Active?.BossOid != 0x48FF || !_actors.TryGetValue(source, out var actor)) return;
        switch (action)
        {
            case 44232:
                if (sandPillars.Count != 0)
                {
                    sandPillars.RemoveAt(0);
                    break;
                }
                foreach (var (position, degrees) in SAND_STARTS)
                {
                    if (Vector2.DistanceSquared(position, new(actor.Position.X, actor.Position.Z)) > 1) continue;
                    var radians = degrees * MathF.PI / 180;
                    var step = new Vector2(MathF.Sin(radians), MathF.Cos(radians)) * 4;
                    for (var i = 1; i < 8; ++i)
                    {
                        var next = Quantize(position + step * i, actor.Position.Y);
                        var rule = new DungeonCastRule(i == 7 ? 44233u : 44232u, actor.Oid,
                            i == 7 ? "Earthbreak" : "SandPillar", new CircleTemplate(i == 7 ? 10 : 4));
                        sandPillars.Add(new(source, rule, next, 0, now, now.AddSeconds(.9 * i)));
                    }
                    break;
                }
                break;
            case 44233:
                sandPillars.Clear();
                break;
            case 44234:
                sandPillars.Add(new(source, new(44233, actor.Oid, "Earthbreak", new CircleTemplate(10)),
                    Quantize(new(actor.Position.X, actor.Position.Z), actor.Position.Y), 0, now, now.AddSeconds(3.1)));
                break;
        }
    }

    private IEnumerable<DungeonAmbientAoe> OrbAoes()
    {
        foreach (var (id, activation) in orbs)
        {
            if (!_actors.TryGetValue(id, out var orb) || orb.Dead) continue;
            var position = Quantize(new(orb.Position.X, orb.Position.Z), orb.Position.Y);
            yield return new($"orb/{id}", "ChaoticChorus", new CircleTemplate(6), position, Activation: activation);
            if (PlayerId == orbTarget)
                yield return new($"orb/kite/{id}", "被点名：远离雷球", new CircleTemplate(10),
                    position, Display: false, Activation: activation);
        }
    }

    private static Vector3 Quantize(Vector2 position, float y)
    {
        const float GRID = 2000f / 65535f;
        const float INVERSE = 1f / GRID;
        return new(((int)MathF.Round(position.X * INVERSE) - .5f) * GRID, y,
            ((int)MathF.Round(position.Y * INVERSE) - .5f) * GRID);
    }
}
