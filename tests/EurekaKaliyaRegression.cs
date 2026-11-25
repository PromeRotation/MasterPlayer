using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;

internal static class EurekaKaliyaRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(-600, 0, -300);
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(1106);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x3D18, Center, InCombat: true, TargetId: 101),
            new(2, 0x233C, Center),
            new(3, 0x3D19, Center + Vector3.UnitX * 15),
            new(100, 0, Center - Vector3.UnitX * 6, PartySlot: 0, Player: true),
            new(101, 0, Center + Vector3.UnitX * 10, PartySlot: 1, Player: true),
        ];
        internal DateTime Now = Start;
        internal Fixture(bool sync = true)
        {
            E.CurrentPartyMembers = [new(0, "First", 100), new(1, "Second", 101)];
            if (sync) Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = change(Actors[index]);
        }
        internal void Sync(ulong viewer = 100)
            => E.Synchronize(Actors, Actors.FirstOrDefault(a => a.Id == viewer)?.Position, Now, playerId: viewer);
        internal void Cast(uint action, ulong source = 2, ulong target = 0)
        {
            Now = Now.AddMilliseconds(10);
            Set(source, a => a with { Cast = new(action, a.Position, .3f, Now, Now.AddSeconds(5.3), target, TotalTime: 5) });
            Sync();
        }
        internal void End(ulong source = 2) { Set(source, a => a with { Cast = null }); Sync(); }
        internal void Status(ulong id, params uint[] statuses)
            => Set(id, a => a with { Statuses = statuses });
        internal void Tether()
        {
            Set(3, a => a with { Tethers = new Dictionary<uint, ulong> { [38] = 100 } });
            Sync();
        }
    }
    internal static void Run(Action<string, Action> run)
    {
        run("Eureka80 exact territory ownership and seven activated source components", Catalog);
        run("Eureka80 independent Barofield latch pending AOE and true offset polygon hole", Barofield);
        run("Eureka80 status slot refresh loss ordering any actor and pet exclusion", StatusSlots);
        run("Eureka80 cold pet status replay invalid-source normalization stable native slot and cleave quantization", ReviewEdges);
        run("Eureka80 combined unlimited FIFO first-cross replacement source-only real Finish", Combined);
        run("Eureka80 ordinary cones snapshot raw-only true Finish and independent groups", Cones);
        run("Eureka80 Magnetism cold replay current charges raw origin and strict source clock", ColdMagnetism);
        run("Eureka80 Magnetism hot source creation earlier later status party-slot overwrite cancellation", HotMagnetism);
        run("Eureka80 Magnetism slot permutation immune ignored donut-only display and hole ray", MagnetismMovement);
        run("Eureka80 Resonance old reference delayable Finish repeated effects dynamic roster first activation", Resonance);
        run("Eureka80 Cleave Barofield independent text all primaries and current target", Cleave);
        run("Eureka80 shared output preserves source finite-sampler rejection and owner cleanup", Output);
        run("Eureka80 source text quantized player cleave self angle and zero direction cones", TextEdges);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Catalog()
    {
        var f = new Fixture();
        Check(f.E.Active?.BossOid == 0x3D18 && f.E.Arena!.Position == Center
            && EurekaOrthosCatalog.Owns(1106, 0x3D18) && !EurekaOrthosCatalog.Owns(1106, 0x3D19),
            "Source identity/ownership differs.");
        Check(f.E.Definitions[0].Casts.Length == 8 && f.E.Definitions[0].Casts.All(c => c.Shape == null)
            && !f.E.Definitions[0].Casts.Any(c => c.Action is 31423 or 31424 or 31425 or 31426),
            "Visual-only enums became extra AOEs.");
    }
    private static void Barofield()
    {
        var f = new Fixture(); f.Cast(31427);
        var prediction = f.E.EurekaBarofield!;
        Check(f.E.EurekaBarofieldActive && prediction.Activation == prediction.Cast.FinishAt.AddSeconds(.7d), "Barofield clock/latch.");
        f.End(); f.Now = f.Now.AddSeconds(30); f.Sync();
        Check(ReferenceEquals(prediction, f.E.EurekaBarofield), "Finish or clock cleared persistent Barofield.");
        f.Status(2, 3420); f.Sync();
        Check(f.E.EurekaBarofield == null && f.E.EurekaBarofieldActive
            && f.E.Arena!.GetContours().Count == 2 && !f.E.Arena.Contains(Center, 0)
            && f.E.Arena.Contains(Center + Vector3.UnitX * 6, 0), "Gain did not publish real holed arena.");
        f.Status(2); f.Sync();
        Check(!f.E.EurekaBarofieldActive && f.E.Arena!.GetContours().Count == 1, "Lose failed circular restoration.");
        var cold = new Fixture(false); cold.Status(2, 3420); cold.Sync();
        Check(!cold.E.EurekaBarofieldActive && !cold.E.Arena!.Contains(Center, 0),
            "Cold status-only incorrectly set cast latch.");
    }
    private static void StatusSlots()
    {
        var f = new Fixture(); f.Cast(31429);
        var status = new ReplayStatus(3420, 0, 7, Start.AddSeconds(20)) { SlotIndex = 4 };
        f.Set(2, a => a with { StatusSlots = [status] }); f.Sync();
        f.Cast(31427);
        Check(f.E.EurekaBarofield == null, "Active re-cast replaced source pending state.");
        f.Set(2, a => a with { StatusSlots = [] }); f.Sync();
        f.Cast(31429);
        var pending = f.E.EurekaBarofield;
        Check(pending != null, "Loss failed reset.");
        f.Set(3, a => a with { NativeType = 0x202, StatusSlots = [status] }); f.Sync();
        Check(ReferenceEquals(pending, f.E.EurekaBarofield), "Pet gained Barofield.");
        f.Set(3, a => a with { NativeType = 0, StatusSlots = [] }); f.Sync();
        f.Set(3, a => a with { StatusSlots = [status] }); f.Sync();
        Check(f.E.EurekaBarofield == null && !f.E.Arena!.Contains(Center, 0), "Nonboss status did not affect source arena.");
        f.Set(3, a => a with { StatusSlots = [status with { SourceId = 8 }] }); f.Sync();
        Check(!f.E.EurekaBarofieldActive && !f.E.Arena!.Contains(Center, 0), "Source change lost Lose→Gain semantics.");
    }
    private static void Combined()
    {
        var f = new Fixture(); f.Cast(32930); f.Cast(32930, 3);
        Check(f.E.EurekaKaliyaCombined.Select(a => a.Kind).SequenceEqual(["ring", "cross", "ring", "cross"]), "Ring order/cap.");
        f.Cast(31432, 1);
        Check(f.E.EurekaKaliyaCombined.Select(a => a.Kind).SequenceEqual(["ring", "ring", "cross", "cannons"])
            && f.E.EurekaKaliyaCombined.Last().Shape == new DungeonCrossTemplate(43, 2.5f), "Cannons shape/first-cross selection.");
        f.End(3);
        Check(f.E.EurekaKaliyaCombined.Select(a => a.Kind).SequenceEqual(["ring", "cross", "cannons"]), "Finish deleted more than first same-source item.");
        f.Now = f.Now.AddSeconds(100); f.Sync();
        Check(f.E.EurekaKaliyaCombined.Count == 3, "Predicted time expired combined records.");
        f.End(2); f.End(1);
        Check(f.E.EurekaKaliyaCombined.Single().Kind == "cross", "Residual source cross was globally cleared.");
    }
    private static void ReviewEdges()
    {
        var cold = new Fixture(false);
        cold.Set(3, a => a with { NativeType = 0x202, Statuses = [3420] }); cold.Sync();
        Check(!cold.E.EurekaBarofieldActive && !cold.E.Arena!.Contains(Center, 0), "Cold pet state was filtered like hot input.");
        var f = new Fixture(); f.Cast(31427);
        var field = new ReplayStatus(3420, 0, 0xE0000000, Start.AddSeconds(30)) { SlotIndex = 4 };
        var other = new ReplayStatus(160, 0, 0, Start.AddSeconds(30)) { SlotIndex = 2 };
        f.Set(2, a => a with { StatusSlots = [other, field] }); f.Sync();
        f.Set(2, a => a with { StatusSlots = [field with { SourceId = 0 }] }); f.Sync();
        Check(f.E.EurekaBarofieldActive && !f.E.Arena!.Contains(Center, 0),
            "Removing unrelated slot or normalizing invalid source emitted false Lose.");
        var cleave = new Fixture(); cleave.Set(1, a => a with { Position = Center + new Vector3(.007f, 0, .013f) }); cleave.Sync();
        Check(cleave.E.AmbientAoes().Single(a => a.Id.Contains("/cleave/1/display")).Position
            == DungeonEncounter.QuantizedDungeonPosition(cleave.E.Actors[1].Position), "Cleave display lost quantization.");
        cleave.Sync(101);
        Check(cleave.E.AmbientAoes().Where(a => a.Id.Contains("/cleave/1/party/")).All(a => a.Position
            == DungeonEncounter.QuantizedDungeonPosition(cleave.E.Actors[1].Position)), "Cleave target-party navigation lost quantization.");
        cleave.Set(1, a => a with { TargetId = 1, Rotation = 1 }); cleave.Sync();
        Check(cleave.E.AmbientAoes().Single(a => a.Id.Contains("/cleave/1/display")).Rotation == 0,
            "Self-target cleave inherited Resonance rotation fallback.");
    }
    private static void Cones()
    {
        var f = new Fixture(); f.Cast(32934);
        var side = f.E.EurekaKaliyaSides.Single();
        f.Set(2, a => a with { Position = Center + Vector3.UnitZ * 7, Rotation = 2 }); f.Sync();
        f.Cast(32933, 3);
        Check(side.Origin == Center && side.Shape == new FanTemplate(25.5f, MathF.PI)
            && f.E.EurekaKaliyaCentral.Single().Shape == new FanTemplate(25.5f, 120 * (MathF.PI / 180f)),
            "Ordinary cones did not freeze source cast geometry.");
        f.End(2);
        Check(f.E.EurekaKaliyaSides.Count == 0 && f.E.EurekaKaliyaCentral.Count == 1, "Separate finish groups merged.");
        f.End(3);
        Check(f.E.EurekaKaliyaCentral.Count == 0, "Central Finish not consumed.");
    }
    private static void TextEdges()
    {
        var f = new Fixture();
        f.Set(100, a => a with { Position = Center + Vector3.UnitX * 11.001f }); f.Sync();
        Check(f.E.Hints(f.E.Actors[100].Position).Contains("GTFO from cleave!"),
            "Cleave text used raw player rather than source quantized player.");
        f.Set(1, a => a with { TargetId = 1, Rotation = MathF.PI / 2 });
        f.Set(100, a => a with { Position = Center + Vector3.UnitZ * 8 }); f.Sync();
        Check(f.E.Hints(f.E.Actors[100].Position).Contains("GTFO from cleave!"),
            "Self cleave text incorrectly used Resonance rotation fallback.");
        f.Set(100, a => a with { Position = Center }); f.Sync(); f.Cast(31422, target: 101);
        Check(!f.E.Hints(Center).Contains("GTFO from baited aoe!"),
            "Source cone zero direction was treated as inside 90 degrees.");
        f.Set(101, a => a with { Position = Center + Vector3.UnitX * 10 }); f.Sync(101);
        Check(!f.E.Hints(f.E.Actors[101].Position).Contains("Bait away from raid!"),
            "Bait text treated party at cone origin as inside.");
        f.End(); f.Cast(32934);
        Check(f.E.Hints(Center).Contains("GTFO from aoe!"),
            "Source 180 degree cone zero direction uses actual negative float cosine.");
        var ring = new Fixture(); ring.Cast(32930);
        ring.Set(3, a => a with { Position = Center + Vector3.UnitX * 100 }); ring.Cast(31432, 3);
        Check(!ring.E.Hints(Center + Vector3.UnitX * 8).Contains("GTFO from aoe!"),
            "Donut inner boundary was included by source text.");
    }
    private static void ColdMagnetism()
    {
        var f = new Fixture(false);
        f.Status(3, 3417); f.Status(100, 3418);
        f.Set(3, a => a with { Tethers = new Dictionary<uint, ulong> { [38] = 100 } });
        f.Sync();
        var kb = f.E.EurekaMagnetisms[0]!;
        Check(kb.Pull && kb.Origin == Center + Vector3.UnitX * 15 && kb.Activation == Start.AddSeconds(10d)
            && kb.Distance == 10 && kb.IgnoreImmunes && kb.StopAfterWall, "Cold magnetism model lost raw source parameters.");
        f.Status(3); f.Status(100); f.Now = f.Now.AddSeconds(20); f.Sync();
        Check(ReferenceEquals(kb, f.E.EurekaMagnetisms[0]), "Charge loss/time erased retained slot model.");
    }
    private static void HotMagnetism()
    {
        var f = new Fixture();
        f.Set(100, a => a with { ObjectIndex = 0, Statuses = [3418] });
        f.Set(3, a => a with { ObjectIndex = 1, Statuses = [3417], Tethers = new Dictionary<uint, ulong> { [38] = 100 } });
        f.Sync();
        Check(f.E.EurekaMagnetisms[0]?.Pull == true, "Feed existing actor must apply Cast→Status→Tether in source order.");
        f.E.Tether(999, 999, false, f.Now, 3);
        Check(f.E.EurekaMagnetisms[0] == null, "Cancel used packet pair instead of stored tether.");
        var newSource = new Fixture();
        newSource.Status(100, 3418); newSource.Sync();
        newSource.Actors.Add(new(4, 0x3D19, Center, Statuses: [3417], Tethers: new Dictionary<uint, ulong> { [38] = 100 }));
        newSource.Sync();
        Check(newSource.E.EurekaMagnetisms.All(k => k == null), "New source initial tether incorrectly read future source statuses.");
        var later = new Fixture();
        later.Set(3, a => a with { ObjectIndex = 0, Statuses = [3417], Tethers = new Dictionary<uint, ulong> { [38] = 100 } });
        later.Set(100, a => a with { ObjectIndex = 1, Statuses = [3418] }); later.Sync();
        Check(later.E.EurekaMagnetisms[0] == null, "Earlier drone read later target charge before its actor input.");
    }
    private static void MagnetismMovement()
    {
        var f = new Fixture(); f.Status(3, 3417); f.Status(100, 3418, 160); f.Sync(); f.Tether();
        Check(f.E.EurekaKaliyaMovement() == null, "Magnetic display ignored donut gate.");
        f.Cast(32930); f.Status(2, 3420); f.Sync();
        var movement = f.E.EurekaKaliyaMovement()!.Value;
        Check(movement.To.X > Center.X - 5.04f && movement.To.X < Center.X - 5.02f && movement.Unsafe,
            "Pull did not stop epsilon into real offset inner boundary or immunity incorrectly removed it.");
        f.E.CurrentPartyMembers = [new(1, "First", 100), new(0, "Second", 101)]; f.Sync();
        Check(f.E.EurekaKaliyaMagnetism() == null, "Stored slot followed target identity on roster permutation.");
        f.E.Tether(999, 999, false, f.Now, 3);
        Check(f.E.EurekaMagnetisms[0] != null, "Untether cleared historical rather than current target slot.");
        f.Sync(101);
        Check(f.E.EurekaKaliyaMagnetism() != null, "Stored slot was rebound to original target.");
    }
    private static void Resonance()
    {
        var f = new Fixture(); f.Cast(31422, target: 101); f.End(); f.Cast(31422, target: 101); f.End();
        var first = f.E.EurekaResonances[0];
        f.Actors.RemoveAll(a => a.Id == 101); f.Sync();
        Check(f.E.EurekaResonances.Count == 2 && first.Target.Removed, "Destroyed target erased delayable bait.");
        f.Actors.Add(new(101, 0, Center - Vector3.UnitZ * 8, PartySlot: 1, Player: true)); f.Sync();
        Check(first.Target.Actor.Position != f.E.Actors[101].Position, "Same-ID target replaced original reference.");
        f.E.CurrentPartyMembers = [new(0, "First", 100), new(3, "Second", 101)];
        Check(f.E.PredictedDamage.Single(d => d.TargetId == 101).PartySlot == 3
            && f.E.PredictedDamage.Single(d => d.TargetId == 101).Activation == first.Cast.FinishAt,
            "Dynamic mask or first stored activation changed.");
        f.E.Effect(2, 31422, 7, f.Now, target: 999); f.E.Effect(2, 31422, 7, f.Now, target: 999);
        Check(f.E.EurekaResonances.Count == 0, "Repeated full source effects failed one-item FIFO.");
        f.Cast(31422, target: 101); f.Set(2, a => a with { Dead = true }); f.Sync();
        Check(f.E.EurekaResonances.Count == 0, "Update failed source death cleanup.");
    }
    private static void Cleave()
    {
        var f = new Fixture(); f.Set(100, a => a with { Position = Center + Vector3.UnitX * 8 }); f.Sync();
        Check(f.E.AmbientAoes().Any(a => a.Id.Contains("/cleave/1/display") && a.OutlineOnly), "Cleave contour missing.");
        f.Cast(31427);
        Check(!f.E.AmbientAoes().Any(a => a.Id.Contains("/cleave/"))
            && f.E.Hints(f.E.Actors[100].Position).Contains("GTFO from cleave!"), "Latch suppressed source text or failed display gate.");
        var cold = new Fixture(false); cold.Status(2, 3420); cold.Sync();
        Check(cold.E.AmbientAoes().Any(a => a.Id.Contains("/cleave/")), "Cold status-only incorrectly suppressed cleave.");
    }
    private static void Output()
    {
        var f = new Fixture(); f.Status(3, 3417); f.Status(100, 3418); f.Sync(); f.Tether();
        Check(DungeonOutput.EurekaMagnetismDistance(Center, Center + Vector3.UnitX * 15, true,
            [(Center + Vector3.UnitX * 15, 0)], Center.X - 6, Center.Z) == 0,
            "Source OR Distance changed to Contains or zero became safe.");
        f.Cast(32930); f.Status(2, 3420); f.Sync();
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = f.Now };
        var draw = new ScriptDrawApi(); var output = new DungeonOutput(safe, draw, safe.Manager);
        output.Apply(f.E, f.Now, false);
        var snapshot = safe.Manager.BuildSnapshot(f.Now);
        Check(snapshot.ArenaBoundary!.GetContours().Count == 2 && snapshot.DangerAreas.Count != 0
            && draw.Previews.Keys.Any(k => k.Contains("/magnetism/")), "Native hole, actual navigation or shared displacement preview missing.");
        output.Clear();
        Check(draw.Previews.Count == 0 && safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count == 0
            && f.E.EurekaMagnetisms[0] != null, "Owner clear leaked or erased model.");
    }
}
