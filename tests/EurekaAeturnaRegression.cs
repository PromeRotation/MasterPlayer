using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;

internal static class EurekaAeturnaRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(-300, 0, -300);
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(1105);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x3D1B, Center, InCombat: true, TargetId: 101),
            new(2, 0x233C, Center),
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
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = change(Actors[index]);
        }
        internal void Sync() => E.Synchronize(Actors, Actors.Single(a => a.Id == 100).Position, Now, playerId: 100);
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
        internal void Crystals(int count)
        {
            for (var i = 0; i < count; ++i)
                Actors.Add(new((ulong)(20 + i), 0x3D1C, Center + Vector3.UnitX * (i + 1),
                    Rotation: i * .2f, Dead: i == 1, Targetable: i != 2));
            Sync();
        }
    }
    internal static void Run(Action<string, Action> run)
    {
        run("Eureka70 exact source scope arena eight activated components ordinary dimensions no NPC teleport", Catalog);
        run("Eureka70 Shatter unlimited crystal snapshots Ferocity latch real Turn Finish and any Shatter Finish all-clear", Shatter);
        run("Eureka70 Shatter current source-index event positions and persistent crystal creation ordering", CrystalOrder);
        run("Eureka70 cold cast replay follows persistent world order and crystals initialize lazily", ColdOrder);
        run("Eureka70 Shatter source cone origin and circle border text raw-only cancellation input", CancellationAndText);
        run("Eureka70 SteelClaw true Finish timeout dead destroyed target slot retention and main-target first-effect FIFO", SteelClaw);
        run("Eureka70 SteelClaw self fallback only and old-party slot captured before current roster commit", SteelClawSlots);
        run("Eureka70 missing perspective preserves claw history and same-ID actor reconstruction never rebinds it", MissingView);
        run("Eureka70 Ferocity independent source float clock good lines retain fifteen-yard navigation and source cancel", Ferocity);
        run("Eureka70 production shared views real solver escape owner clear and preserved component model", Output);
    }
    private static void Check(bool ok, string text)
    {
        if (!ok) throw new InvalidOperationException(text);
    }
    private static void Catalog()
    {
        var f = new Fixture();
        Check(f.E.Active?.BossOid == 0x3D1B && f.E.Arena!.Position == Center
            && f.E.Arena.Contains(Center + Vector3.UnitZ * 20, 0)
            && !EurekaOrthosCatalog.Owns(1105, 0x3D1C), "Boss/territory ownership or source arena differs.");
        f.Cast(31441);
        Check(f.E.Casts[2].Rule.Shape == new CircleTemplate(3), "Falling Rock radius changed.");
        f.Cast(31438);
        Check(f.E.Casts[2].Rule.Shape == new CircleTemplate(5), "Impact radius changed.");
        f.Cast(31437);
        Check(f.E.Casts[2].Rule.Shape == new DonutTemplate(6, 30), "Turn donut changed.");
        f.Cast(31435);
        Check(f.E.Casts[2].Rule.Shape == null && f.E.PredictedDamage.Count(d => d.DamageType == "Raidwide") == 2,
            "Roar was projected as an avoidable circle or lost predictions.");
        Check(!f.E.Definitions.Single().Casts.Any(c => c.Action is 31446 or 31443 or 31444 or 6497),
            "Inactive source enums expanded mechanism scope.");
    }
    private static void Shatter()
    {
        var f = new Fixture(); f.Crystals(6); f.Cast(31436);
        var first = f.E.EurekaShatters[0];
        Check(f.E.EurekaShatters.Count == 6 && first.Shape is FanTemplate { Radius: 23.95f }
            && first.Trigger.Observed && first.Activation == first.Trigger.FinishAt.AddSeconds(.5),
            "Shatter cap, geometry or trigger clock differs.");
        var original = first.Origin;
        f.Set(20, a => a with { Position = Center + Vector3.UnitZ * 16, Rotation = 2 }); f.Sync();
        f.Actors.RemoveAll(a => a.Id == 20); f.Sync(); f.End();
        f.Now = f.Now.AddSeconds(100); f.Sync();
        Check(first.Crystal.Removed && first.Origin == original && f.E.EurekaShatters.Count == 6,
            "Destroyed/expired crystal rebound or deleted prediction.");
        f.Cast(31442); f.End(); f.Cast(31437);
        Check(f.E.EurekaShatters.Count == 6, "Ferocity Finish incorrectly reset the Turn latch.");
        f.End(); f.Cast(31437);
        Check(f.E.EurekaShatters.Count == 11 && f.E.EurekaShatters.Last().Shape == new CircleTemplate(8),
            "True Turn Finish failed latch reset or new crystal snapshot append.");
        f.End(); f.Cast(31440); f.End();
        Check(f.E.EurekaShatters.Count == 0, "Any real Shatter Finish failed global clear.");
    }
    private static void CrystalOrder()
    {
        var f = new Fixture(); f.Crystals(3);
        f.Set(2, a => a with { ObjectIndex = 1 });
        f.Set(20, a => a with { ObjectIndex = 2, Position = Center + Vector3.UnitZ * 7 });
        f.Cast(31436);
        Check(f.E.EurekaShatters[0].Origin == DungeonEncounter.QuantizedDungeonPosition(Center + Vector3.UnitX),
            "Earlier Turn read a later crystal movement.");
        f.End();
        f.Actors.RemoveAll(a => a.Id == 20); f.Sync();
        f.Actors.Add(new(23, 0x3D1C, Center)); f.Sync();
        f.Cast(31437);
        Check(f.E.EurekaShatters.Skip(3).Select(s => s.Crystal.Actor.Id).SequenceEqual(new ulong[] { 21, 22, 23 }),
            "Dictionary slot reuse reordered the source crystal list.");
    }
    private static void SteelClaw()
    {
        var f = new Fixture(); f.Cast(31445, target: 101);
        var first = f.E.EurekaSteelClaws.Single();
        f.End(); f.Cast(31445, target: 101); f.End();
        f.Set(101, a => a with { Dead = true }); f.Sync();
        f.Actors.RemoveAll(a => a.Id == 101); f.Sync();
        f.Now = f.Now.AddSeconds(100); f.Sync();
        Check(f.E.EurekaSteelClaws.Count == 2 && first.Target.Removed
            && f.E.PredictedDamage.Count(d => d.DamageType == "Tankbuster") == 2, "Finish/death/destroy/time removed delayable records.");
        f.E.Effect(1, 31445, 4, f.Now, target: 100);
        Check(f.E.EurekaSteelClaws.Count == 2, "Wrong main target removed a claw.");
        f.E.Effect(1, 31445, 4, f.Now, target: 101);
        Check(f.E.EurekaSteelClaws.Count == 1 && !ReferenceEquals(f.E.EurekaSteelClaws[0], first),
            "Complete effect failed main-target first-match consumption.");
        f.E.Effect(1, 31445, 4, f.Now, target: 101);
        Check(f.E.EurekaSteelClaws.Count == 0, "Repeated full packet failed second consumption.");
    }
    private static void ColdOrder()
    {
        var e = new DungeonEncounter(1105) { CurrentPartyMembers = [new(0, "First", 100)] };
        List<DungeonActor> actors =
        [
            new(1, 0x3D1B, Center, ObjectIndex: 0),
            new(2, 0x233C, Center, ObjectIndex: 3),
            new(20, 0x3D1C, Center + Vector3.UnitX, ObjectIndex: 4),
            new(100, 0, Center + Vector3.UnitZ * 10, Player: true, PartySlot: 0, ObjectIndex: 5),
        ];
        e.Synchronize(actors, actors[3].Position, Start, playerId: 100);
        actors.Add(new(3, 0x233C, Center, ObjectIndex: 1));
        e.Synchronize(actors, actors[3].Position, Start.AddSeconds(.1), playerId: 100);
        actors[0] = actors[0] with { InCombat = true };
        actors[1] = actors[1] with { Cast = new(31442, Center, 0, Start.AddSeconds(.2), Start.AddSeconds(5.5), TotalTime: 5) };
        actors[4] = actors[4] with { Cast = new(31436, Center, 0, Start.AddSeconds(.2), Start.AddSeconds(5.5), TotalTime: 5) };
        e.Synchronize(actors, actors[3].Position, Start.AddSeconds(.2), playerId: 100);
        Check(e.Active != null && e.EurekaShatters.Count == 0,
            "Cold bootstrap sorted by current object index and replayed Turn before earlier Ferocity.");

        var f = new Fixture(); f.Crystals(3);
        f.Actors.RemoveAll(a => a.Id == 20); f.Sync();
        f.Actors.Add(new(23, 0x3D1C, Center + Vector3.UnitZ * 8)); f.Sync();
        f.Cast(31437);
        Check(f.E.EurekaShatters.Select(s => s.Crystal.Actor.Id).SequenceEqual(new ulong[] { 23, 21, 22 }),
            "Crystal list was built before first consumer instead of the current persistent dictionary.");
    }
    private static void MissingView()
    {
        var f = new Fixture(); f.Cast(31445, target: 101);
        var claw = f.E.EurekaSteelClaws.Single();
        f.Actors.RemoveAll(a => a.Id is 100 or 101); f.E.CurrentPartyMembers = [];
        f.Now = f.Now.AddSeconds(.1);
        f.E.Synchronize(f.Actors, null, f.Now, playerId: 999);
        Check(f.E.Active != null && f.E.EurekaSteelClaws.Single() == claw && claw.Target.Removed,
            "Missing viewer and Party0 cleared global claw history.");
        f.Actors.Add(new(101, 0, Center + Vector3.UnitZ * 12, Player: true, PartySlot: 5));
        f.E.CurrentPartyMembers = [new(5, "New second", 101)];
        f.E.Synchronize(f.Actors, null, f.Now.AddSeconds(.1), playerId: 999);
        Check(claw.Target.Actor.Position == Center + Vector3.UnitX * 10
            && claw.Slot == 1 && claw.Target.Removed,
            "Same ID reconstruction rebound the frozen target or captured slot.");
        f.E.Effect(1, 31445, 0, f.Now.AddSeconds(.2), target: 101);
        Check(f.E.EurekaSteelClaws.Count == 0, "Missing perspective blocked complete main-target effect consumption.");
    }
    private static void CancellationAndText()
    {
        var f = new Fixture(); f.Crystals(1); f.Cast(31436);
        var shatter = f.E.EurekaShatters.Single();
        Check(!f.E.Hints(shatter.Origin).Contains("GTFO from aoe!"),
            "Source zero-vector cone origin acquired a generic epsilon warning.");
        f.End(); f.Cast(31442); f.End(); f.Cast(31436);
        var adapter = new DungeonEventAdapter(f.E, f.Sync, _ => 0, _ => { });
        adapter.Handle(new LogSystemCancelCastEvent
            { SourceId = 2, ActionId = 31436, Timestamp = f.Now.AddMilliseconds(1) }, f.Now);
        Check(f.E.Casts.ContainsKey(2), "Cancel packet ended an unchanged observed raw cast.");
        f.Actors.Add(new(3, 0x233C, Center)); f.Cast(31437, source: 3);
        Check(f.E.EurekaShatters.Count == 1, "Cancel packet prematurely cleared Ferocity latch.");
        f.End(3); f.End();
        f.Cast(31439);
        adapter.Handle(new LogSystemInterruptCastEvent
            { SourceId = 2, ActionId = 31439, Timestamp = f.Now.AddMilliseconds(1) }, f.Now);
        Check(f.E.EurekaShatters.Count == 1, "Interrupt packet prematurely consumed Shatter history.");
        f.End();
        Check(f.E.EurekaShatters.Count == 0, "True raw Finish failed Shatter clear.");
        f.Cast(31437);
        var circle = f.E.EurekaShatters.Single();
        Check(f.E.Hints(circle.Origin + Vector3.UnitX * 8).Contains("GTFO from aoe!")
            && !f.E.Hints(circle.Origin + Vector3.UnitX * 8.0001f).Contains("GTFO from aoe!"),
            "Source circle boundary acquired display geometry epsilon.");
    }
    private static void SteelClawSlots()
    {
        var f = new Fixture();
        f.Cast(31445, source: 1, target: 1);
        Check(f.E.EurekaSteelClaws.Single().Target.Actor.Id == 101, "Self-targeted Steel Claw missed caster main target.");
        f.End(1); f.Cast(31445, source: 1);
        Check(f.E.EurekaSteelClaws.Count == 1, "Zero target incorrectly fell back to main target.");
        f.End(1);
        f.E.CurrentPartyMembers = [new(0, "First", 100), new(5, "Second", 101)];
        f.Cast(31445, target: 101);
        Check(f.E.EurekaSteelClaws.Last().Slot == 1, "Raw start used a not-yet-committed new slot.");
        f.End(); f.Cast(31445, target: 101);
        Check(f.E.EurekaSteelClaws.Last().Slot == 5 && f.E.EurekaSteelClaws[0].Slot == 1,
            "Committed slot was not captured or rewrote a past record.");
    }
    private static void Ferocity()
    {
        var f = new Fixture();
        f.E.Tether(100, 57, true, f.Now, 1);
        var first = f.E.EurekaFerocityTether.Baits.Single();
        Check(first.Activation == f.Now.AddSeconds((double)5.7f), "Ferocity float delay changed.");
        f.Set(1, a => a with { Tethers = new Dictionary<uint, ulong> { [1] = 100 } }); f.Sync();
        Check(f.E.EurekaChimeraLines().Single().Safe
            && f.E.AmbientAoes().Single(a => a.Id.StartsWith("eureka/tether/ferocity/")).Activation == first.Activation,
            "Good tether lost line colour, original timer or independent navigation.");
        f.E.UpdateEurekaFrame(first.Activation.AddSeconds(2));
        Check(f.E.EurekaFerocityTether.Baits.Count == 1 && f.E.EurekaFerocityTether.Timers.Count == 0,
            "Timer expiry deleted Ferocity bait.");
        f.E.Tether(999, 999, false, f.Now, 1);
        Check(f.E.EurekaFerocityTether.Baits.Count == 0, "Source-only cancel used packet target/type.");
    }
    private static void Output()
    {
        var f = new Fixture(); f.E.Tether(100, 57, true, f.Now, 1);
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = f.Now };
        var draw = new ScriptDrawApi(); var output = new DungeonOutput(safe, draw, safe.Manager);
        output.Apply(f.E, f.Now, false);
        var snapshot = safe.Manager.BuildSnapshot(f.Now);
        var solution = new SafeZoneSolver().Solve(snapshot,
            new(Center + Vector3.UnitZ * 10, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions(), f.Now);
        Check(draw.Lines.Keys.Any(id => id.Contains("eureka/tether/ferocity/"))
            && snapshot.DangerAreas.Count != 0 && solution.IsSuccess && !solution.IsLeastDangerFallback,
            "Shared line or actual solver navigation missing.");
        output.Clear();
        Check(draw.Lines.Count == 0 && safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count == 0
            && f.E.EurekaFerocityTether.Baits.Count == 1, "Owner clear leaked or destroyed mechanism history.");
    }
}
