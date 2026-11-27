using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsCll1 => Definition.PrimaryOid == 0x2ECC;
    internal Dictionary<BozjaActor, BozjaCll1State> Cll1Constructions { get; } = [];
    internal BozjaCll1State? Cll1 => Primary == null ? null : Cll1Constructions.GetValueOrDefault(Primary);
    internal bool UsesManagerParty0 => IsSkirmish || IsCll1 || IsCll3 || IsCll4 || IsBaldesion || IsAllianceTrash;

    private static Vector2 Cll1XZ(Vector3 point) => new(point.X, point.Z);
    private static ulong Cll1Bit(int slot) => slot is >= 0 and < 64 ? 1UL << slot : 0;
    private int Cll1Slot(ulong id) => Party.Where(p => p.EntityId == id).OrderBy(p => p.Slot).FirstOrDefault()?.Slot ?? -1;
    private BozjaRule Cll1Rule(string component) => Definition.Rules.Single(r => r.Component == component);

    private void Cll1Created(BozjaActor actor)
    {
        if (!IsCll1) return;
        foreach (var state in Cll1Constructions.Values)
            if (state.Enemies.TryGetValue(actor.Actor.Oid, out var enemies)) enemies.Add(actor);
        if (actor.Actor.Oid == Definition.PrimaryOid)
        {
            if (Primary is { Destroyed: true } old && old.Actor.Id == actor.Actor.Id
                && Cll1Constructions.Remove(old, out var existing))
                Cll1Constructions[actor] = existing;
            else
                ConstructCll1(actor);
        }
        if (Active && actor.NativeNpc)
        {
            Cll1OrbCreated(actor, Cll1Rule("OrbsAOE"));
            Cll1PoleCreated(actor);
        }
    }

    private void ConstructCll1(BozjaActor primary)
    {
        if (!IsCll1) return;
        var state = new BozjaCll1State();
        Cll1Constructions[primary] = state;
    }

    private List<BozjaActor> Cll1Enemies(uint oid)
    {
        var state = Cll1!;
        if (!state.Enemies.TryGetValue(oid, out var actors))
            state.Enemies.Add(oid, actors = Actors.Values.Where(a => a.Actor.Oid == oid).ToList());
        return actors;
    }

    private void PrepareCll1Rule(BozjaRule rule)
    {
        if (!IsCll1) return;
        if (rule.Component == "BossHealths") _ = Cll1Enemies(0x2ED3);
        if (rule.Component == "InfraredBlast") _ = Cll1Enemies(0x2ED7);
    }

    private void Cll1Destroyed(BozjaActor actor)
    {
        foreach (var state in Cll1Constructions.Values)
            if (state.Enemies.TryGetValue(actor.Actor.Oid, out var enemies)) enemies.Remove(actor);
    }

    private bool Cll1Complete(BozjaActor primary)
        => Cll1 is { } state && Cll1Enemies(0x2F4E).All(a => a.Destroyed || a.Actor.Dead)
            && (primary.Destroyed || primary.Actor.Dead)
            && (state.HellDiver == null || state.HellDiver.Destroyed || state.HellDiver.Actor.Dead);

    // BossModule.UpdateModule runs after the state machine's completion check.
    private void UpdateCll1()
    {
        if (IsCll1 && Active && !wipeInProgress && Cll1 is { } state)
            state.HellDiver ??= Cll1Enemies(0x2ED3).FirstOrDefault();
    }

    private void RestoreCll1Arena()
    {
        if (Cll1 is not { } state) return;
        Center = state.Top ? BozjaCll1Catalog.TopCenter : BozjaCll1Catalog.BottomCenter;
        Arena = state.Top ? BozjaCll1Catalog.Top : BozjaCll1Catalog.Bottom;
    }

    internal void ObserveCll1Drawing(BozjaActor viewer)
    {
        if (Cll1 is not { } state) return;
        var pos = viewer.Actor.Position;
        if (state.Top && Cll1InBottom(pos)) state.Top = false;
        else if (!state.Top && Cll1InTop(pos)) state.Top = true;
        RestoreCll1Arena();
    }

    private static bool Cll1InTop(Vector3 p)
        => MathF.Abs(p.X - 80) <= 29.5f && MathF.Abs(p.Z + 222) <= 14.5f;
    private static bool Cll1InBottom(Vector3 p)
        => MathF.Abs(p.X - 80) <= 29.58f && MathF.Abs(p.Z - (-179.41f)) <= 24.59f;

    private bool Cll1OutputEnabled(BozjaRule rule)
    {
        if (!IsCll1 || Cll1 == null) return true;
        return rule.Component switch
        {
            "ElectricAnvil" or "Voltstream" or "FalseThunder" or "LightningShower" or "OrbsAOE" => Cll1.Top,
            "ChainCannon" or "WildCharges" or "CommandSuppressiveFormation" or "SurfaceMissile"
                or "MRVMissile" or "MagitekMissiles" or "InfraredBlast" => !Cll1.Top,
            _ => true,
        };
    }

    private void BeginCll1Rule(BozjaRule rule, BozjaActor actor)
    {
        if (actor.NativeNpc)
        {
            if (rule.Component == "OrbsAOE") Cll1OrbCreated(actor, rule);
            if (rule.Component == "Magnetism") Cll1PoleCreated(actor);
            if (actor.Cast != null) Start(actor, rule);
        }
        if (rule.Component == "Magnetism") Cll1MagnetTether(actor, actor.ObservedTether);
        if (rule.Component == "InfraredBlast")
        {
            Cll1InfraredTether(actor, actor.ObservedTether, false);
            foreach (var status in actor.Statuses.OfType<ReplayStatus>()) Cll1Status(actor, status, true);
        }
    }

    private void StartCll1(BozjaActor actor, BozjaRule rule)
    {
        if (Cll1 is not { } state || actor.Cast is not { } spell) return;
        if (rule.Component == "ChainCannon" && spell.ActionId == 20985)
        {
            var cast = new BozjaCast(++serial, rule, actor, spell.Location, spell.Rotation ?? 0, Activation(actor));
            Casts.Add(cast);
            state.ChainHits.Add(cast.Generation, 0);
        }
        if (rule.Component == "WildCharges")
        {
            var delta = Cll1XZ(spell.Location) - Cll1XZ(actor.Actor.Position);
            Casts.Add(new(++serial, rule, actor, DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position),
                MathF.Atan2(delta.X, delta.Y), Activation(actor))
            {
                ShapeOverride = RayRectangleTemplate.Symmetric(spell.ActionId == 20971 ? 6 : 10, delta.Length()),
                Action = spell.ActionId,
            });
        }
        if (rule.Component != "OrbsAOE") return;
        if (spell.ActionId == 20944) ++state.EnergyGenerations;
        else
        {
            var shift = spell.ActionId == 20947;
            foreach (var orb in state.Orbs)
            {
                var shape = orb.Shape;
                if (shift) shape = shape is DonutTemplate ? new CircleTemplate(12) : new DonutTemplate(5, 20);
                AddCll1Orb(orb.Actor, shape, Now.AddSeconds(shift ? 11.9d : 11d), rule);
            }
        }
    }

    private void AddCll1Orb(BozjaActor actor, GeometryTemplate shape, DateTime activation, BozjaRule rule)
        => Casts.Add(new(++serial, rule, actor, DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position), 0, activation)
            { ShapeOverride = shape });

    private void Cll1OrbCreated(BozjaActor actor, BozjaRule rule)
    {
        if (Cll1 is not { } state) return;
        GeometryTemplate? shape = actor.Actor.Oid switch
        {
            0x2ECD => new DonutTemplate(5, 20),
            0x2ECE => new CircleTemplate(12),
            _ => null,
        };
        if (shape == null) return;
        if (state.EnergyGenerations > 2) state.Orbs.Add((actor, shape));
        else AddCll1Orb(actor, shape, Now.AddSeconds(11d), rule);
    }

    private void Cll1PoleCreated(BozjaActor actor)
    {
        if (Cll1 is { } state && actor.Actor.Oid is 0x2ECD or 0x2ECE)
            state.Poles.Add((actor.Actor.Id, actor.Actor.Position, [], 0));
    }

    private void FinishCll1(ReplayCast spell, BozjaRule rule)
    {
        if (Cll1 is not { } state) return;
        if (rule.Component == "OrbsAOE" && spell.ActionId is 20945 or 20946)
        {
            Casts.RemoveAll(c => c.Rule == rule);
            state.Orbs.Clear();
        }
        if (rule.Component == "WildCharges" && rule.Actions.Contains(spell.ActionId))
        {
            var head = Casts.FindIndex(c => c.Rule == rule);
            if (head >= 0) Casts.RemoveAt(head);
        }
    }

    private void Cll1Effect(BozjaActor caster, uint action)
    {
        if (!IsCll1 || Cll1 is not { } state) return;
        if (action is 20985 or 20986)
        {
            var pos = caster.Actor.Position;
            var head = Casts.FirstOrDefault(c => c.Rule.Component == "ChainCannon"
                && MathF.Abs(c.Origin.X - pos.X) <= .1f && MathF.Abs(c.Origin.Z - pos.Z) <= .1f);
            if (head != null && ++state.ChainHits[head.Generation] == 7)
            {
                Casts.Remove(head);
                state.ChainHits.Remove(head.Generation);
            }
        }
        if (state.MagnetTethered != 0 && action is 20952 or 20950)
        {
            Array.Clear(state.MagnetSources);
            Array.Clear(state.PlayerPoles);
            state.MagnetTethered = 0;
            state.Poles.Clear();
        }
        if (action == 20974) state.InfraredActivation = default;
    }

    private IEnumerable<BozjaAoe> Cll1Aoes(BozjaRule rule)
    {
        if (!IsCll1 || rule.Kind != BozjaRuleKind.Cll1Custom) yield break;
        if (rule.Component is "ChainCannon" or "OrbsAOE" or "WildCharges")
            foreach (var cast in Casts.Where(c => c.Rule == rule)) yield return new(cast, false, true, rule.Safe);
    }

    private bool Cll1TimewindowVisible(BozjaRule rule, BozjaCast[] list, int index)
        => !(IsCll1 && rule.Component == "FalseThunder" || IsHeavyArtillery && rule.Component == "ManeuverHighPoweredLaser") || list[index].Activation < list[0].Activation.AddSeconds(1d);
}
