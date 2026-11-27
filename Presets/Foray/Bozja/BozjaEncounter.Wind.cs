using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    private void Created(BozjaActor actor)
    {
        HeavyCreated(actor);
        LightCreated(actor);
        RaidenCreated(actor);
        GraveCreated(actor);
        RobotCreated(actor);
        foreach (var rule in ActiveRules.Where(r => r.Kind == BozjaRuleKind.Tower && r.EnemyOid != 0))
            CreatedTower(actor, rule);
        HoofCreated(actor);
        HashmalCreated(actor);
        if (actor.NativeNpc) SeekerCreated(actor);
        foreach (var rule in Definition.Rules) FrostCreated(actor, rule);
        if (!Active || !actor.NativeNpc || actor.Actor.Oid != 0x1EB02E
            || !Definition.Rules.Any(r => r.Kind == BozjaRuleKind.ArenaChange)) return;
        Center = DungeonEncounter.QuantizedDungeonPosition(Center);
        Arena = new CircleGeometry(Center, 25);
        Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.ArenaChange);
    }

    internal void Effect(ulong source, uint action, ulong target = 0, Vector3? position = null)
    {
        PyromancerThermalEffect(source, action);
        PatriotEffect(source, action);
        MetalFoxEffect(source, action);
        if (Active && Actors.GetValueOrDefault(source) is { NativeNpc: true } caster)
        {
            DeathgazeEffect(action, target);
            Cll1Effect(caster, action);
            Cll4Effect(action);
            ArtEffect(caster, action, target, position);
            OwainEffect(action);
            RaidenEffect(action, target);
            VirtueEffect(action);
            HoofEffect(caster, action, target);
            BehemothEffect(caster, action, target, position);
            FireEffect(caster, action);
            BurnEffect(caster, action);
            GraveEffect(action);
            RobotEffect(action);
            CreatedTowerEffect(caster, action);
            ColossusEffect(action);
            CuchEffect(caster, action, position);
            SaunionEffect(action);
            DiabloEffect(caster, action);
            GauntletEffect(action, target);
            TimeEffect(caster, action);
            HashmalEffect(caster, action, position);
            AidaEffect(caster, action, position);
            CavalryEffect(action, target);
            if (IsHeavyArtillery && action == 20658) HeavyEnergy.Remove(caster);
            LightEffect(action);
            RhalgrEffect(action);
            DahuEffect(caster, action);
            PhantomSavageEffect(action);
            DahuSavageEffect(action, target);
            SeekerEffect(caster, action, target);
            GuardEffect(caster, action);
            if (action == 24089 && Hailfires.Count > 0) Hailfires.RemoveAt(0);
        }
        // Wind consumes the four actual spell effects from any NPC source.
        // Its visual Finish, destruction and elapsed prediction do not clear it.
        if (Active && Actors.GetValueOrDefault(source) is { NativeNpc: true }
            && action is 20184 or 20185 or 20186 or 20187)
            Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.Wind);
    }

    private void UpdateInterruptHint(BozjaRule rule)
    {
        if (IsAllianceTrash && rule.Hint != null)
        {
            interruptHints[rule] = rule.Hint;
            return;
        }
        var casters = Casts.Where(c => c.Rule == rule).ToArray();
        interruptHints[rule] = !IsAlbeleo && casters.Length == 1 ? $"Interrupt {casters[0].Source.Actor.Name}!" : "Interrupt!";
    }

    internal IEnumerable<BozjaActor> InterruptTargets
        => Active ? Casts.Where(c => c.Rule.Kind == BozjaRuleKind.Interrupt).Select(c => c.Source) : [];

    internal bool ImmuneAt(int slot, DateTime activation)
        => slot is >= 0 and < 64 && (immunity[slot, 0] > activation
            || immunity[slot, 1] > activation || immunity[slot, 2] > activation);

    private bool WindImmune(BozjaActor viewer, DateTime activation)
        => ImmuneAt(Party.FirstOrDefault(p => p.EntityId == viewer.Actor.Id)?.Slot ?? -1, activation);

    internal IEnumerable<(BozjaCast Cast, Vector3 From, Vector3 To)> WindMovements(BozjaActor viewer)
    {
        if (!Active) yield break;
        foreach (var cast in Casts.Where(c => c.Rule.Kind == BozjaRuleKind.Wind))
        {
            if (WindImmune(viewer, cast.Activation)) continue;
            var (sin, cos) = Math.SinCos(cast.Rotation);
            var from = viewer.Actor.Position;
            yield return (cast, from, from + 30 * new Vector3((float)sin, 0, (float)cos));
        }
    }

    private IEnumerable<BozjaZone> WindNavigation(BozjaActor viewer)
    {
        foreach (var cast in Casts.Where(c => c.Rule.Kind == BozjaRuleKind.Wind))
        {
            if (WindImmune(viewer, cast.Activation)) continue;
            var (sin, cos) = Math.SinCos(cast.Rotation);
            var origin = Center - 10 * new Vector3((float)sin, 0, (float)cos);
            yield return new($"wind/{cast.Generation}", cast.Rule.Component, cast.Activation,
                new FanTemplate(30, 2 * (135 * MathF.PI / 180)), origin, cast.Rotation);
        }
    }
}
