using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long carnivaleDadJokeGeneration;
    private static bool CarnivalePapaAction(uint action) => action is 18687 or 18686 or 18688 or 18602 or 18604 or 18603 or 18601;
    internal CarnivaleDadJoke? CarnivaleDadJoke => IsCarnivale && Active?.BossOid == 0x2C58 ? carnivaleSelected?.DadJoke : null;

    private void CarnivalePapaIcon(ulong source, ulong target, uint icon, DateTime occurredAt)
    {
        if (!IsCarnivale || icon != 23 || !_actors.ContainsKey(source)) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Primary.Actor.Oid == 0x2C58))
            module.DadJoke = new(++carnivaleDadJokeGeneration, source, target, icon, occurredAt, _now,
                _now.AddSeconds(5d), module.Primary, 15);
    }

    private void CarnivalePapaEffect(ulong source, uint action)
    {
        if (!IsCarnivale || action != 18605 || !_actors.ContainsKey(source)) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Primary.Actor.Oid == 0x2C58))
            module.DadJoke = null;
    }

    private void CarnivalePapaBootstrap()
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.PapaNeedsBootstrap))
        {
            module.PapaNeedsBootstrap = false;
            if (module.Primary.Actor.Oid != 0x2C84) continue;
            foreach (var cast in CarnivaleOrderedCasts.Where(c => c.Rule.Action == 18688))
                if (carnivaleActors.TryGetValue(cast.Source, out var source))
                    module.Interrupts.Add(new(++carnivaleInterruptGeneration, source, _casts[cast.Source]));
        }
    }

    private IEnumerable<DungeonAmbientAoe> CarnivaleThunderheadAoes()
    {
        if (!IsCarnivale || Active?.BossOid != 0x2C58) yield break;
        foreach (var actor in CarnivaleEnemies(0x2C59).Where(a => a.EventState != 7))
            yield return new($"carnivale/thunderhead/{actor.Id}", "Thunderhead", new CircleTemplate(8),
                actor.Position, actor.Rotation, Obstacle: true);
    }

    internal Vector3? CarnivalePapaLanding(DungeonActor actor)
    {
        if (CarnivaleDadJoke is not { } kb
            || carnivaleSelected!.ChargeImmunity.ImmuneAt(CarnivaleSlot(actor.Id), kb.Activation)) return null;
        var delta = actor.Position - kb.Origin.Actor.Position;
        var angle = MathF.Atan2(delta.X, delta.Z);
        return actor.Position + new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle)) * kb.Distance;
    }

    private IEnumerable<DungeonAmbientAoe> CarnivalePapaAoes()
    {
        foreach (var aoe in CarnivaleThunderheadAoes()) yield return aoe;
        if (_actors.GetValueOrDefault(PlayerId) is not { } player || CarnivalePapaLanding(player) is not { } to) yield break;
        foreach (var aoe in QuestKnockbackProjection($"carnivale/papa/kb/{CarnivaleDadJoke!.Generation}", player, player.Position, to))
            yield return aoe;
    }

    private IEnumerable<string> CarnivalePapaHints(Vector3 position)
    {
        if (Active?.BossOid is not (0x2C84 or 0x2C58) || carnivalePrimary == null) yield break;
        var firstAct = Active.BossOid == 0x2C84;
        if (CarnivaleHasStatus(carnivalePrimary, firstAct ? 63u : 1797u))
            yield return $"Dispel {carnivalePrimary.Name} with Eerie Soundwave!";
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (CarnivaleHasStatus(player, firstAct ? 269u : 271u))
            yield return firstAct ? "Windburn on you! Cleanse it with Exuviation." : "Electrocution on you! Cleanse it with Exuviation.";
        var pools = CarnivaleThunderheadAoes().ToArray();
        if (pools.Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(position)))
            yield return "GTFO from voidzone!";
        if (CarnivalePapaLanding(player) is { } to
            && (Arena?.Contains(to) == false || pools.Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(to))))
            yield return "About to be knocked into danger!";
    }
}
