using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;

internal static class EurekaChimeraRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(-300, 0, -300);
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(1103);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x3D9C, Center, InCombat: true, Hp: 100),
            new(2, 0x233C, Center),
            new(20, 0x3D9D, Center + Vector3.UnitX * 2),
            new(21, 0x3D9D, Center - Vector3.UnitX * 2),
            new(100, 0, Center + Vector3.UnitZ * 10, PartySlot: 0, Player: true),
            new(101, 0, Center + Vector3.UnitX * 10, PartySlot: 1, Player: true),
        ];
        internal DateTime Now = Start;
        internal Fixture()
        {
            E.CurrentPartyMembers = [new(0, "First", 100), new(1, "Second", 101)];
            Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = change(Actors[i]);
        }
        internal void Sync() => E.Synchronize(Actors, Actors.First(a => a.Id == 100).Position, Now, playerId: 100);
        internal void Cast(uint action, ulong source = 2, ulong target = 0)
        {
            Now = Now.AddMilliseconds(10);
            Set(source, a => a with { Cast = new(action, a.Position, .3f, Now, Now.AddSeconds(5.3), target, TotalTime: 5) });
            Sync();
        }
        internal void End(ulong source = 2)
        {
            Set(source, a => a with { Cast = null }); Sync();
        }
        internal void Tether(ulong source, ulong target, uint type, bool present = true)
            => E.Tether(target, type, present, Now, source);
    }
    private static void Check(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); }
    internal static void Run(Action<string, Action> run)
    {
        run("Eureka50 voice raw FIFO independent hidden model queue inverse model risk current primary and no expiry", Voices);
        run("Eureka50 any actor model transitions only after creation retained source and consumption clocks", Models);
        run("Eureka50 charge original references dynamic width8 bait navigation and raw Finish only", Charges);
        run("Eureka50 orb any tether duplicates first-original-reference effect and frozen destroyed source", Orbs);
        run("Eureka50 independent stretch timers float51 good bad membership source-only cancel and expiry does not clear bait", Tethers);
        run("Eureka50 source reverse direction dead-party Untether and inactive dead-source line visibility", ReverseTethers);
        run("Eureka50 production dual-view previews source navigation damage and owner clear preserves component models", Output);
        run("Eureka50 live model precedes same-actor Cast and new model4 emits after activation but not bootstrap or Pet", ModelOrder);
        run("Eureka50 source ordered creation excludes later tether target and initial tether precedes death", CreationOrder);
        run("Eureka50 ordinary tether gains follow sanitized Target rather than Type and Kite excludes stretch hints", TetherContract);
        run("Eureka50 raw tether uses preceding party membership while following packets use committed current party", PartyOrder);
    }
    private static void Voices()
    {
        var f = new Fixture();
        f.Set(20, a => a with { ModelState = 4 }); f.Sync();
        var firstModel = f.E.EurekaVoiceModels[0];
        f.Cast(31852);
        var raw = f.E.EurekaVoiceCasts[0];
        Check(f.E.EurekaVoiceCasts.Count == 2 && raw.Shape == new DonutTemplate(8, 40)
            && raw.Cast is { Source: 2, Observed: true }
            && f.E.EurekaVoiceCasts[1].Activation == raw.Activation.AddSeconds(3), "Raw voice source order or clocks lost.");
        var visible = f.E.AmbientAoes().Where(a => a.Id.StartsWith("eureka/voice/")).ToArray();
        Check(visible.Length == 2 && visible[0].Navigate && !visible[1].Navigate && visible[0].Color != null,
            "Raw front-only navigation/color differs.");
        f.End(); f.Now = f.Now.AddSeconds(100); f.Sync();
        Check(f.E.EurekaVoiceCasts.Count == 2 && f.E.EurekaVoiceModels.Count == 2, "Finish/time deleted independent history.");
        f.E.Effect(2, 32807, 1, f.Now); f.E.Effect(2, 31851, 1, f.Now);
        visible = f.E.AmbientAoes().Where(a => a.Id.StartsWith("eureka/voice/")).ToArray();
        Check(visible.Length == 2 && !visible[0].Navigate && visible[1].Navigate
            && f.E.EurekaVoiceModels[0] == firstModel, "Raw effects did not reveal preserved model or inverted model risk lost.");
        f.E.Effect(2, 31853, 1, f.Now);
        Check(f.E.EurekaVoiceModels.Count == 1, "Full repeated packet failed global FIFO consumption.");
    }
    private static void Models()
    {
        var f = new Fixture();
        f.Set(20, a => a with { ModelState = 5 }); f.Sync();
        var model = f.E.EurekaVoiceModels[0];
        Check(model.ModelState == 5 && model.ModelSource!.Actor.Id == 20 && model.Cast == null
            && model.Activation == f.Now.AddSeconds(2.4d), "Any-actor ModelState source or consuming clock lost.");
        f.Set(1, a => a with { Position = Center + Vector3.UnitX * 3.137f }); f.Sync();
        Check(f.E.AmbientAoes().Single(a => a.Id == $"eureka/voice/{model.Generation}").Position
            == DungeonEncounter.QuantizedDungeonPosition(Center + Vector3.UnitX * 3.137f), "Model voice origin not dynamic primary.");
        f.Actors.Add(new(30, 0x233C, Center, ModelState: 4)); f.Sync();
        Check(f.E.EurekaVoiceModels.Count == 2, "Created state fabricated a model transition.");
        f.E.Effect(2, 31854, 1, f.Now); f.E.Effect(2, 31854, 1, f.Now);
        f.Set(30, a => a with { AnimationState1 = 1 }); f.Sync();
        Check(f.E.EurekaVoiceModels.Count == 2 && f.E.EurekaVoiceModels[0].ModelState == 4,
            "Animation change with retained model4 failed transition after queue drained.");
    }
    private static void Charges()
    {
        var f = new Fixture(); f.Cast(31855, target: 100);
        var charge = f.E.EurekaCharges.Single();
        Check(charge.HalfWidth == 4 && charge.Length == 10 && charge.Cast.Observed, "Charge source parameters changed.");
        f.Set(2, a => a with { Position = Center + Vector3.UnitZ * 2 });
        f.Set(100, a => a with { Position = Center + Vector3.UnitZ * 14 }); f.Sync();
        Check(charge.Length == 12 && charge.Rotation == 0
            && f.E.AmbientAoes().Any(a => a.Id.Contains("/party/101")), "Dynamic endpoint or target bait cone lost.");
        f.Set(100, a => a with { Dead = true }); f.Sync();
        Check(f.E.EurekaCharges.Count == 1, "Dead target removed charge.");
        f.E.Effect(2, 31855, 4, f.Now);
        Check(f.E.EurekaCharges.Count == 1, "Effect consumed Finish-only charge.");
        f.Actors.RemoveAll(a => a.Id == 100); f.E.Synchronize(f.Actors, null, f.Now, playerId: 0);
        f.Actors.Add(new(100, 0, Center + Vector3.UnitX * 12, PartySlot: 0, Player: true)); f.Sync();
        Check(charge.Target.Removed && charge.Target.Actor.Position == Center + Vector3.UnitZ * 14
            && f.E.AmbientAoes().Any(a => a.Id.EndsWith("/avoid")), "Same-ID target rebound or navigation used ID instead of reference.");
        f.End();
        Check(f.E.EurekaCharges.Count == 0, "True raw Finish failed first-source removal.");
    }
    private static void Orbs()
    {
        var f = new Fixture(); f.Tether(20, 100, 999); f.Tether(20, 100, 999);
        Check(f.E.EurekaOrbs.Count == 2 && f.E.EurekaOrbTether.Baits.Count == 0, "Orb wrongly restricted to17 or duplicate collapsed.");
        var old = f.E.EurekaOrbs[0];
        f.Tether(20, 0, 0, false);
        Check(f.E.EurekaOrbs.Count == 2, "Untether consumed orb.");
        f.E.Effect(20, 31865, 4, f.Now);
        Check(f.E.EurekaOrbs.Count == 1, "Effect did not remove only first original actor.");
        f.Actors.RemoveAll(a => a.Id == 20); f.Sync();
        f.Actors.Add(new(20, 0x3D9D, Center + Vector3.UnitZ * 5)); f.Sync();
        f.E.Effect(20, 31865, 4, f.Now);
        Check(f.E.EurekaOrbs.Count == 1 && old.Removed, "New same-ID effect consumed old orb.");
        var navigation = f.E.AmbientAoes().Single(a => a.Id == "eureka/orb/0/navigation");
        Check(navigation.Shape == new DungeonCapsuleTemplate(6, 10) && navigation.Position == old.Actor.Position,
            "Orb radius/length reversed or original position rebound.");
    }
    private static void Tethers()
    {
        var f = new Fixture(); f.Tether(20, 100, 57); f.Tether(21, 100, 1);
        var first = f.E.EurekaChargeTether.Baits[0];
        Check(first.Activation == f.Now.AddSeconds((double)5.1f)
            && f.E.EurekaChargeTether.Baits[1].Activation == first.Activation, "Per-player first float clock not reused.");
        Check(f.E.AmbientAoes().Count(a => a.Id.StartsWith("eureka/tether/charge/")) == 2,
            "Good tether dropped source fifteen-yard navigation.");
        f.Tether(20, 0, 0, false);
        Check(f.E.EurekaChargeTether.Baits.Count == 1 && f.E.EurekaChargeTether.Timers.Count == 1,
            "Source-only cancel failed or deleted shared timer.");
        f.Now = f.Now.AddSeconds(7); f.Sync();
        // Raw input now has no tethers, so packet-only old baits are reconciled; create a fresh gain.
        f.Tether(20, 100, 17);
        Check(f.E.EurekaOrbTether.Baits.Single().Activation == f.Now
            && f.E.EurekaChargeTether.Timers.Count == 0, "Independent timer expiry or zero-delay single differs.");
        var single = f.E.EurekaOrbTether.Baits.Single();
        f.E.UpdateEurekaFrame(f.Now.AddSeconds(2));
        Check(f.E.EurekaOrbTether.Baits.Contains(single) && f.E.EurekaOrbTether.Timers.Count == 0,
            "Timer expiry deleted live bait.");
    }
    private static void ReverseTethers()
    {
        var f = new Fixture();
        f.Tether(100, 20, 57);
        f.Set(100, a => a with { Dead = true });
        f.E.Synchronize(f.Actors, null, f.Now, playerId: 0);
        // The source re-evaluates dead party membership on Untether, reversing the pair.
        f.Tether(100, 0, 0, false);
        Check(f.E.EurekaChargeTether.Baits.Count == 1, "Dead player reverse-direction source semantics were normalized away.");
        var draw = new Fixture(); draw.Tether(20, 100, 17); draw.Tether(21, 100, 17);
        draw.Set(20, a => a with { Dead = true, Tethers = new Dictionary<uint, ulong> { [17] = 100 } });
        draw.Set(21, a => a with { Tethers = new Dictionary<uint, ulong> { [17] = 100 } }); draw.Sync();
        Check(draw.E.EurekaChimeraLines().Count() == 2
            && draw.E.AmbientAoes().Count(a => a.Id.StartsWith("eureka/tether/orb/")) == 1,
            "Alive foreground gate failed to draw retained dead-source line independently of navigation.");
    }
    private static void Output()
    {
        var f = new Fixture(); f.Cast(31856, target: 100); f.Tether(20, 100, 17);
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = f.Now };
        var draw = new ScriptDrawApi(); var output = new DungeonOutput(safe, draw, safe.Manager);
        output.Apply(f.E, f.Now, false);
        Check(draw.Previews.Values.Any(p => p.Id.Contains("eureka/charge/"))
            && draw.Lines.Keys.Any(id => id.Contains("eureka/tether/orb/line/"))
            && safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count > 0, "Shared output lost display or navigation.");
        output.Clear();
        Check(draw.Previews.Count == 0 && draw.Lines.Count == 0 && safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count == 0
            && f.E.EurekaCharges.Count == 1, "Output clear leaked or unloaded component history.");
    }
    private static void ModelOrder()
    {
        var f = new Fixture();
        f.Set(2, a => a with { ModelState = 4,
            Cast = new(31851, Center, 0, f.Now, f.Now.AddSeconds(5)) });
        f.Sync();
        Check(f.E.EurekaVoiceModels.Count == 2 && f.E.EurekaVoiceCasts.Count == 2, "Cast ran before the same-actor model event.");
        for (var i = 0; i < 4; ++i) f.E.Effect(2, 31853, 0, f.Now);
        f.Actors.Add(new(30, 0x233C, Center, NativeType: 0x202, ModelState: 4)); f.Sync();
        Check(f.E.EurekaVoiceModels.Count == 0, "Pet created a model voice.");
        f.Actors.Add(new(31, 0x233C, Center, ModelState: 4)); f.Sync();
        Check(f.E.EurekaVoiceModels.Count == 2
            && f.E.EurekaVoiceModels[0].ModelSource!.Actor.Id == 31, "Live Created model4 lost its real model event.");
        var cold = new DungeonEncounter(1103);
        cold.Synchronize([new(1, 0x3D9C, Center, InCombat: true, ModelState: 4),
            new(100, 0, Center, Player: true, PartySlot: 0)], Center, Start, playerId: 100);
        Check(cold.EurekaVoiceModels.Count == 0, "Activation replayed a pre-existing model event.");
    }
    private static void CreationOrder()
    {
        var f = new Fixture();
        f.Set(20, a => a with { ObjectIndex = 1, Tethers = new Dictionary<uint, ulong> { [17] = 102 } });
        f.Actors.Add(new(102, 0, Center, Player: true, PartySlot: 2, ObjectIndex: 2));
        f.E.CurrentPartyMembers = [new(0, "First", 100), new(1, "Second", 101), new(2, "Third", 102)];
        f.Sync();
        Check(f.E.EurekaOrbs.Count == 1 && f.E.EurekaOrbTether.Baits.Count == 0,
            "Tether resolved a later-created target.");
        f.Sync();
        Check(f.E.EurekaOrbTether.Baits.Count == 0, "Unchanged tether retried after target creation.");
        f.Actors.RemoveAll(a => a.Id == 101); f.Sync();
        f.Actors.Add(new(101, 0, Center, Dead: true, Player: true, PartySlot: 1,
            Tethers: new Dictionary<uint, ulong> { [57] = 21 }));
        f.Sync();
        var bait = f.E.EurekaChargeTether.Baits.Single();
        Check(bait.Target.Actor.Id == 101 && bait.Source.Actor.Id == 21 && bait.Target.Actor.Dead,
            "Initial Dead diff preceded Created tether direction.");
    }
    private static void TetherContract()
    {
        var f = new Fixture();
        f.Tether(20, 0, 17);
        f.Tether(20, 0xE0000000, 17);
        Check(f.E.EurekaOrbs.Count == 0, "Zero/sentinel Target emitted an ordinary Gain.");
        f.Tether(20, 100, 0);
        Check(f.E.EurekaOrbs.Count == 1, "Valid Target with zero Type lost an ordinary Gain.");
        f.Tether(20, 100, 17);
        var hints = f.E.Hints(f.Actors.Single(a => a.Id == 100).Position).ToArray();
        Check(hints.Contains("Kite the add!") && !hints.Contains("Stretch tether further!")
            && !hints.Contains("Tether is stretched!"), "Single Kite also emitted parent stretch hints.");
    }
    private static void PartyOrder()
    {
        var f = new Fixture();
        f.E.CurrentPartyMembers = [new(0, "First", 100)]; f.Sync();
        f.E.CurrentPartyMembers = [new(0, "First", 100), new(1, "Second", 101)];
        f.Set(101, a => a with { Tethers = new Dictionary<uint, ulong> { [57] = 20 } }); f.Sync();
        Check(f.E.EurekaChargeTether.Baits.Single().Target.Actor.Id == 20,
            "Actor Gain read the future party membership.");
        f.Sync();
        Check(f.E.EurekaChargeTether.Baits.Count == 1, "Party commit replayed an unchanged raw tether.");
        f.Tether(101, 21, 57);
        Check(f.E.EurekaChargeTether.Baits.Last().Target.Actor.Id == 101,
            "Following packet did not read the committed party.");
        var leaving = new Fixture();
        leaving.E.CurrentPartyMembers = [new(0, "First", 100)];
        leaving.Set(101, a => a with { Tethers = new Dictionary<uint, ulong> { [57] = 20 } }); leaving.Sync();
        Check(leaving.E.EurekaChargeTether.Baits.Single().Target.Actor.Id == 101,
            "Actor Gain lost the preceding party membership on departure.");
    }
}
