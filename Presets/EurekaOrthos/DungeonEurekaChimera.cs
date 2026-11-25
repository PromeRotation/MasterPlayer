using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<EurekaVoice> eurekaVoiceCasts = [];
    private readonly List<EurekaVoice> eurekaVoiceModels = [];
    private readonly List<EurekaCharge> eurekaCharges = [];
    private readonly List<AlphaActorState> eurekaOrbs = [];
    private readonly Dictionary<AlphaActorState, (byte Model, byte Anim1, byte Anim2)> eurekaModelStates = [];
    private readonly HashSet<AlphaActorState> eurekaTetherObserved = [];
    private bool eurekaChimeraBootstrapped;
    private bool eurekaApplyingActorInput;
    private IReadOnlyList<ReplayPartyMember> eurekaCommittedParty = [];
    private readonly EurekaStretchTether eurekaOrbTether = new(17, 17, 0d, true);
    private readonly EurekaStretchTether eurekaChargeTether = new(57, 1, (double)5.1f, false);
    internal IReadOnlyList<EurekaVoice> EurekaVoiceCasts => eurekaVoiceCasts;
    internal IReadOnlyList<EurekaVoice> EurekaVoiceModels => eurekaVoiceModels;
    internal IReadOnlyList<EurekaCharge> EurekaCharges => eurekaCharges;
    internal IReadOnlyList<AlphaActorState> EurekaOrbs => eurekaOrbs;
    internal EurekaStretchTether EurekaOrbTether => eurekaOrbTether;
    internal EurekaStretchTether EurekaChargeTether => eurekaChargeTether;
    private bool EurekaUsesOrderedInput => territory is EurekaOrthosCatalog.CHIMERA or EurekaOrthosCatalog.AETURNA or EurekaOrthosCatalog.PROTO_KALIYA or EurekaOrthosCatalog.ADMINISTRATOR or EurekaOrthosCatalog.EXCALIBUR;

    private IEnumerable<DungeonActor> EurekaInputActors()
        => EurekaUsesOrderedInput && !eurekaChimeraBootstrapped
            ? eurekaActors.Values.Select(a => a.Actor)
            : _actors.Values.OrderBy(a => a.ObjectIndex ?? ushort.MaxValue);

    private IEnumerable<(EurekaStretchTether Component, string Key)> EurekaTetherComponents()
    {
        if (territory == EurekaOrthosCatalog.CHIMERA)
        {
            yield return (eurekaOrbTether, "orb");
            yield return (eurekaChargeTether, "charge");
        }
        else if (territory == EurekaOrthosCatalog.AETURNA)
            yield return (eurekaFerocityTether, "ferocity");
    }

    private IReadOnlyList<ReplayPartyMember> EurekaInputParty
        => eurekaApplyingActorInput ? eurekaCommittedParty : CurrentPartyMembers;
    private bool EurekaParty(AlphaActorState actor) => !actor.Actor.Dead
        && EurekaInputParty.Any(p => p.EntityId == actor.Actor.Id);

    private bool EurekaChimeraBeforeCast(DungeonActor actor)
    {
        if (!EurekaUsesOrderedInput || Active == null || !eurekaEnabled) return false;
        var created = !eurekaActors.TryGetValue(actor.Id, out var state);
        if (created)
        {
            state = new(actor with { Dead = false, Statuses = null, StatusSlots = [] });
            eurekaActors.Add(actor.Id, state);
            // Create applies the initial tether before the first Dead/Model/Cast diffs.
            var initial = EurekaRawTether(actor);
            if (initial.Type != 0) EurekaReplaceTether(state, initial);
            eurekaTetherObserved.Add(state);
        }
        EurekaKaliyaUnknownFinish(state!.Actor, actor);
        EurekaExcaliburUnknownFinish(state.Actor, actor);
        state.Actor = actor;
        EurekaAeturnaObserve(state);
        var value = (actor.ModelState, actor.AnimationState1, actor.AnimationState2);
        var changed = eurekaModelStates.TryGetValue(state, out var previous) ? previous != value
            : eurekaChimeraBootstrapped && value != default;
        if (territory == EurekaOrthosCatalog.CHIMERA && changed && actor.NativeType != 0x202
            && eurekaVoiceCasts.Count == 0 && eurekaVoiceModels.Count == 0 && actor.ModelState is 4 or 5)
        {
            var first = actor.ModelState == 4 ? (GeometryTemplate)new CircleTemplate(9) : new DonutTemplate(8, 40);
            var second = actor.ModelState == 4 ? (GeometryTemplate)new DonutTemplate(8, 40) : new CircleTemplate(9);
            eurekaVoiceModels.Add(new(++eurekaGeneration, first, default, _now, _now.AddSeconds(2.4d),
                ModelSource: state, ModelState: actor.ModelState));
            eurekaVoiceModels.Add(new(++eurekaGeneration, second, default, _now, _now.AddSeconds(5.5d),
                ModelSource: state, ModelState: actor.ModelState));
        }
        eurekaModelStates[state] = value;
        return created;
    }

    private static (uint Type, ulong Target) EurekaRawTether(DungeonActor actor)
    {
        var tether = actor.Tethers?.FirstOrDefault() ?? default;
        return (tether.Key, tether.Value == 0xE0000000 ? 0 : tether.Value);
    }

    private void EurekaChimeraAfterCast(DungeonActor actor, bool created)
    {
        if (!EurekaUsesOrderedInput || Active == null || !eurekaEnabled
            || !eurekaActors.TryGetValue(actor.Id, out var state)) return;
        EurekaKaliyaStatus(state);
        EurekaExcaliburStatus(state);
        if (created) return;
        var current = EurekaRawTether(actor);
        var first = eurekaTetherObserved.Add(state);
        if (first || state.Tether != current)
            EurekaReplaceTether(state, current, bootstrap: !eurekaChimeraBootstrapped && first);
    }

    private void EurekaChimeraInputComplete()
    {
        if (EurekaUsesOrderedInput && Active != null && eurekaEnabled)
        {
            eurekaCommittedParty = CurrentPartyMembers.ToArray();
            eurekaApplyingActorInput = false;
            eurekaChimeraBootstrapped = true;
        }
    }

    private void EurekaReplaceTether(AlphaActorState source, (uint Type, ulong Target) next, bool bootstrap = false)
    {
        if (next.Target == 0xE0000000) next.Target = 0;
        if (source.Tether.Target != 0)
        {
            var old = source.Tether;
            EurekaKaliyaTether(source, old, false);
            var target = eurekaActors.GetValueOrDefault(old.Target);
            foreach (var (component, _) in EurekaTetherComponents())
                component.Lose(source, target, old.Type, EurekaParty);
        }
        source.Tether = next;
        if (bootstrap ? next.Type == 0 : next.Target == 0) return;
        if (territory == EurekaOrthosCatalog.CHIMERA && source.Actor.Oid == 0x3D9D) eurekaOrbs.Add(source);
        EurekaKaliyaTether(source, next, true);
        var nextTarget = eurekaActors.GetValueOrDefault(next.Target);
        foreach (var (component, _) in EurekaTetherComponents())
            component.Gain(source, nextTarget, next.Type, EurekaParty, _now, ++eurekaGeneration);
    }

    private void EurekaTether(ulong source, uint type, ulong target, bool present)
    {
        if (!EurekaUsesOrderedInput || Active == null || !eurekaEnabled
            || !eurekaActors.TryGetValue(source, out var state)) return;
        eurekaTetherObserved.Add(state);
        // TetherCancel carries the source only; consume its stored old tether.
        EurekaReplaceTether(state, present ? (type, target) : default);
    }

    private void EurekaChimeraRemoved(AlphaActorState state)
    {
        if (!EurekaUsesOrderedInput) return;
        EurekaReplaceTether(state, default);
        eurekaAeturnaCrystals.Remove(state);
        if (IsEurekaKaliya || IsEurekaExcalibur)
        {
            var previous = state.Actor;
            state.Actor = previous with { Statuses = null, StatusSlots = [] };
            EurekaKaliyaStatus(state);
            EurekaExcaliburStatus(state);
            state.Actor = previous;
            eurekaKaliyaStatuses.Remove(state);
            eurekaExcaliburStatuses.Remove(state);
        }
        eurekaModelStates.Remove(state);
        eurekaTetherObserved.Remove(state);
    }

    private void EurekaChimeraCast(DungeonPendingCast cast)
    {
        if (territory != EurekaOrthosCatalog.CHIMERA || !cast.Observed) return;
        if (cast.Rule.Action is 31851 or 31852)
        {
            var first = cast.Rule.Action == 31851 ? (GeometryTemplate)new CircleTemplate(9) : new DonutTemplate(8, 40);
            var second = cast.Rule.Action == 31851 ? (GeometryTemplate)new DonutTemplate(8, 40) : new CircleTemplate(9);
            eurekaVoiceCasts.Add(new(++eurekaGeneration, first, cast.Location, _now, cast.FinishAt, cast));
            eurekaVoiceCasts.Add(new(++eurekaGeneration, second, cast.Location, _now, cast.FinishAt.AddSeconds(3d), cast));
        }
        else if (cast.Rule.Action is 31855 or 31856 && eurekaActors.TryGetValue(cast.Source, out var source)
            && eurekaActors.TryGetValue(cast.TargetId, out var target))
            eurekaCharges.Add(new(++eurekaGeneration, source, target, cast));
    }

    private void EurekaChimeraFinish(DungeonPendingCast cast)
    {
        if (territory != EurekaOrthosCatalog.CHIMERA || !cast.Observed || cast.Rule.Action is not (31855 or 31856)) return;
        var index = eurekaCharges.FindIndex(c => c.Cast.Source == cast.Source && c.Cast.Rule.Action == cast.Rule.Action);
        if (index >= 0) eurekaCharges.RemoveAt(index);
    }

    private void EurekaChimeraEffect(ulong source, uint action)
    {
        if (action == 31865)
        {
            if (eurekaActors.TryGetValue(source, out var caster)) eurekaOrbs.Remove(caster);
        }
        else if (action is 31851 or 31852 or 31853 or 31854 or 32806 or 32807)
        {
            if (eurekaVoiceCasts.Count != 0) eurekaVoiceCasts.RemoveAt(0);
            else if (eurekaVoiceModels.Count != 0) eurekaVoiceModels.RemoveAt(0);
        }
    }

    private IEnumerable<DungeonAmbientAoe> EurekaChimeraAoes()
    {
        if (territory != EurekaOrthosCatalog.CHIMERA) yield break;
        if (eurekaVoiceCasts.Count > 1) eurekaVoiceCasts[0].Danger = true;
        var voices = eurekaVoiceCasts.Count != 0 ? eurekaVoiceCasts : eurekaVoiceModels;
        for (var i = 0; i < voices.Count; ++i)
        {
            var voice = voices[i];
            var raw = voice.Cast != null;
            var position = raw ? voice.Origin : QuantizedDungeonPosition(_actors[PrimaryId].Position);
            var danger = raw ? voice.Danger : i == 0 && voices.Count > 1;
            yield return new($"eureka/voice/{voice.Generation}", "Ram / Dragon Voice", voice.Shape, position,
                Navigate: raw ? i == 0 : i != 0, Activation: voice.Activation,
                Color: danger ? new Vector4(1, 0, 0, 1) : null);
        }
        for (var i = 0; i < eurekaOrbs.Count; ++i)
        {
            var orb = eurekaOrbs[i].Actor;
            yield return new($"eureka/orb/{i}/display", "Cacophony", new CircleTemplate(6), orb.Position, Navigate: false);
            yield return new($"eureka/orb/{i}/navigation", "Cacophony", new DungeonCapsuleTemplate(6, 10),
                orb.Position, orb.Rotation, Display: false);
        }
        foreach (var charge in eurekaCharges.Where(c => !c.Source.Actor.Dead))
        {
            var prefix = $"eureka/charge/{charge.Generation}";
            var origin = charge.Source.Actor.Position;
            var shape = RayRectangleTemplate.Symmetric(8, charge.Length);
            yield return new(prefix + "/display", charge.Cast.Rule.Name, shape, origin, charge.Rotation,
                Navigate: false, OutlineOnly: charge.Target.Actor.Id == PlayerId);
            if (!ReferenceEquals(charge.Target, eurekaActors.GetValueOrDefault(PlayerId)))
                yield return new(prefix + "/avoid", charge.Cast.Rule.Name, shape, origin, charge.Rotation,
                    Display: false, Activation: charge.Cast.FinishAt);
            else if (!ReferenceEquals(charge.Source, charge.Target))
                foreach (var member in CurrentPartyMembers.Select(p => _actors.GetValueOrDefault(p.EntityId))
                    .Where(a => a != null && !a.Dead && a.Id != PlayerId))
                {
                    var offset = member!.Position - origin;
                    var half = MathF.Asin(4 / new Vector2(offset.X, offset.Z).Length());
                    if (!float.IsFinite(half)) continue;
                    yield return new($"{prefix}/party/{member.Id}", "Bait away from raid", new FanTemplate(100, 2 * half),
                        origin, MathF.Atan2(offset.X, offset.Z), Display: false, Activation: charge.Cast.FinishAt);
                }
        }
        foreach (var aoe in EurekaTetherAoes()) yield return aoe;
    }

    private IEnumerable<DungeonAmbientAoe> EurekaTetherAoes()
    {
        var current = eurekaActors.GetValueOrDefault(PlayerId);
        foreach (var (component, key) in EurekaTetherComponents())
            foreach (var bait in component.Baits.Where(b => !b.Source.Actor.Dead && ReferenceEquals(b.Target, current)))
                yield return new($"eureka/tether/{key}/{bait.Generation}", "Stretch tether", new CircleTemplate(15),
                    bait.Source.Actor.Position, Display: false, Activation: bait.Activation);
    }

    internal IEnumerable<(string Id, Vector3 Start, Vector3 End, bool Safe)> EurekaChimeraLines()
    {
        if (!EurekaUsesOrderedInput || Active == null || !eurekaEnabled
            || eurekaActors.GetValueOrDefault(PlayerId) is not { } current) yield break;
        foreach (var (component, key) in EurekaTetherComponents())
        {
            if (!component.Baits.Any(b => !b.Source.Actor.Dead && ReferenceEquals(b.Target, current))) continue;
            var bad = component.Members.Contains((current, component.Bad));
            if (!bad && !component.Members.Contains((current, component.Good))) continue;
            // Source drawing iterates all baits once an active bait opens the foreground gate.
            foreach (var bait in component.Baits.Where(b => ReferenceEquals(b.Target, current)))
                yield return ($"eureka/tether/{key}/line/{bait.Generation}",
                    bait.Source.Actor.Position, bait.Target.Actor.Position, !bad);
        }
    }

    private IEnumerable<DungeonDamagePrediction> EurekaChimeraDamage()
    {
        if (territory != EurekaOrthosCatalog.CHIMERA || Active == null || !eurekaEnabled) yield break;
        foreach (var action in new uint[] { 31855, 31856 })
        {
            var charges = eurekaCharges.Where(c => c.Cast.Rule.Action == action).ToArray();
            if (charges.Length == 0) continue;
            foreach (var target in charges.Where(c => !c.Source.Actor.Dead).Select(c => c.Target.Actor).DistinctBy(a => a.Id))
            {
                var slot = CurrentPartyMembers.FirstOrDefault(p => p.EntityId == target.Id)?.Slot ?? -1;
                if (slot is >= 0 and < 64)
                    yield return new(target.Id, slot, target.PartyRole, target.Name, charges[0].Cast.FinishAt, "Tankbuster");
            }
        }
    }

    private IEnumerable<string> EurekaChimeraHints(Vector3 position)
    {
        if (territory != EurekaOrthosCatalog.CHIMERA || Active == null || !eurekaEnabled
            || eurekaActors.GetValueOrDefault(PlayerId) is not { } current) yield break;
        if (EurekaChimeraAoes().Where(a => a.Navigate && a.Id.StartsWith("eureka/voice/")
                || a.Id.StartsWith("eureka/orb/") && a.Id.EndsWith("/display"))
            .Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(position)))
            yield return "GTFO from aoe!";
        foreach (var hint in EurekaTetherHints(position)) yield return hint;
        foreach (var charge in eurekaCharges.Where(c => !c.Source.Actor.Dead))
        {
            var shape = RayRectangleTemplate.Symmetric(8, charge.Length).Resolve(charge.Source.Actor.Position, charge.Rotation, null);
            if (charge.Target.Actor.Id == PlayerId && CurrentPartyMembers.Select(p => _actors.GetValueOrDefault(p.EntityId))
                .Any(a => a != null && !a.Dead && a.Id != PlayerId && shape.Contains(a.Position)))
                yield return "Bait away from raid!";
            if (charge.Target.Actor.Id != PlayerId && shape.Contains(position)) yield return "GTFO from baited aoe!";
        }
    }

    private IEnumerable<string> EurekaTetherHints(Vector3 position)
    {
        if (eurekaActors.GetValueOrDefault(PlayerId) is not { } current) yield break;
        foreach (var (component, _) in EurekaTetherComponents())
        {
            if (component.Kite && component.Baits.Count != 0 && component.Members.Contains((current, component.Bad)))
            {
                yield return "Kite the add!";
                continue;
            }
            var first = component.Baits.FirstOrDefault(b => !b.Source.Actor.Dead && ReferenceEquals(b.Target, current));
            if (first != null)
            {
                var distance = Vector2.DistanceSquared(new(first.Source.Actor.Position.X, first.Source.Actor.Position.Z),
                    new(position.X, position.Z));
                if (distance < 225 && component.Members.Contains((current, component.Bad))) yield return "Stretch tether further!";
                else if (distance >= 225 || component.Members.Contains((current, component.Good))) yield return "Tether is stretched!";
            }
        }
    }

    private void ResetEurekaChimera()
    {
        eurekaVoiceCasts.Clear();
        eurekaVoiceModels.Clear();
        eurekaCharges.Clear();
        eurekaOrbs.Clear();
        eurekaModelStates.Clear();
        eurekaTetherObserved.Clear();
        eurekaChimeraBootstrapped = false;
        eurekaApplyingActorInput = false;
        eurekaCommittedParty = [];
        eurekaOrbTether.Clear();
        eurekaChargeTether.Clear();
        eurekaFerocityTether.Clear();
    }
}
