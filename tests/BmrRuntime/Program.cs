using System.Numerics;
using System.Reflection;
using System.Text;
using BossMod;
using BossMod.Components;
using MasterPlayer.BmrRuntime;
using Microsoft.VisualBasic.FileIO;

var root = Path.GetFullPath(args.ElementAtOrDefault(0) ?? Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var sqpack = args.ElementAtOrDefault(1) ?? Environment.GetEnvironmentVariable("FFXIV_SQPACK");
if (string.IsNullOrWhiteSpace(sqpack) || !Directory.Exists(sqpack))
    throw new ArgumentException("Supply the repository root and the real game sqpack directory (or FFXIV_SQPACK).");
using var data = new Lumina.GameData(sqpack, new Lumina.LuminaOptions
{
    DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified,
    PanicOnSheetChecksumMismatch = true
});
var logs = new List<string>();
var logger = DispatchProxy.Create<Dalamud.Plugin.Services.IPluginLog, RecordingLog>();
((RecordingLog)logger).Messages = logs;
BmrRuntimeEnvironment.Initialize(data, logger);
var failures = new List<string>();
var checks = 0;
var coverage = new List<string> { "module,kind,primary_oid,registered,constructed,initial_tick,phases,active_phase,error" };

void Check(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
    ++checks;
}
void Scenario(string name, Action body)
{
    try { body(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add($"{name}: {ex.GetBaseException().Message}"); Console.WriteLine($"FAIL {name}: {ex}"); }
}
string Quote(string text) => $"\"{text.Replace("\"", "\"\"")}\"";
var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
WorldState NewWorld()
{
    var world = new WorldState((ulong)TimeSpan.TicksPerSecond, "BMR runtime regression");
    world.Execute(new WorldState.OpFrameStart(new(start, 0, 1, 0, 0, 1), default, default, default));
    var classes = new[] { Class.PLD, Class.WAR, Class.WHM, Class.SCH, Class.DRG, Class.NIN, Class.BRD, Class.BLM };
    for (var i = 0; i < classes.Length; ++i)
    {
        var id = (ulong)(100 + i);
        world.Execute(new ActorState.OpCreate(id, 0, i * 2, 0, $"Player {i}", 0, ActorType.Player, classes[i], 100,
            new(100 + i, 0, 100, 0), .5f, new(100000, 100000, 0, 10000, 10000), true, true, 0, 0, 0));
        world.Execute(new PartyState.OpModify(i, new(id, id, false, $"Player {i}")));
    }
    return world;
}
Actor AddBoss(WorldState world, uint oid)
{
    world.Execute(new ActorState.OpCreate(1, oid, 16, 0, "Boss", 0, ActorType.Enemy, Class.None, 100,
        new(100, 0, 100, 0), 2, new(1000000, 1000000, 0, 10000, 10000), true, false, 0, 0, 0));
    return world.Actors.Find(1)!;
}
void Tick(WorldState world, BossModule module, double seconds)
{
    var time = start.AddSeconds(seconds);
    var delta = (float)(time - world.CurrentTime).TotalSeconds;
    world.Execute(new WorldState.OpFrameStart(new(time, (ulong)(seconds * TimeSpan.TicksPerSecond),
        world.Frame.Index + 1, delta, delta, 1), default, default, default));
    module.Update();
}

Scenario("source library excludes the plugin entry and all Crucible modules", () =>
{
    var types = typeof(WorldState).Assembly.GetTypes();
    Check(!types.Any(t => typeof(Dalamud.Plugin.IDalamudPlugin).IsAssignableFrom(t)), "A Dalamud plugin entry was imported");
    Check(!types.Any(t => t.Namespace?.Contains(".Crucible") == true), "Crucible is out of scope");
    Check(typeof(WorldState).Assembly.GetManifestResourceNames().Any(n => n.StartsWith("BossModReborn.Pathfinding.ObstacleMaps.")), "Obstacle resources are missing");
    Check(Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(4)?.TerritoryType.RowId == 1036, "Real CFC to territory lookup failed");
});

if (args.Length > 3)
    Scenario("upstream source and obstacle resources have only documented adaptations", () =>
        Check(SourceIntegrity.Validate(root, args[3]) > 0, "No upstream source was verified"));

Scenario("source integrity rejects extra edits inside an already adapted file", () =>
{
    var temporary = Path.Combine(Path.GetTempPath(), "MasterPlayer.BmrIntegrityTests-" + Guid.NewGuid().ToString("N"));
    try
    {
        var original = Path.Combine(temporary, "original");
        var imported = Path.Combine(temporary, "imported");
        var originalFile = Path.Combine(original, "BossMod/Test.cs");
        var importedFile = Path.Combine(imported, "BmrRuntime/Upstream/Test.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(originalFile)!);
        Directory.CreateDirectory(Path.GetDirectoryName(importedFile)!);
        const string sourceText = "class Original;\n";
        const string importedText = "class Adapted;\n";
        string Hash(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        File.WriteAllText(originalFile, sourceText);
        File.WriteAllText(importedFile, importedText);
        File.WriteAllText(Path.Combine(imported, "BmrRuntime/SourceChanges.json"), System.Text.Json.JsonSerializer.Serialize(new
            Dictionary<string, object> { ["Test.cs"] = new { reason = "Reviewed adaptation", originalSha256 = Hash(sourceText), importedSha256 = Hash(importedText) } }));
        Check(SourceIntegrity.Validate(imported, original) == 1, "The reviewed adaptation was rejected");
        void ExpectRejected(string message)
        {
            try { SourceIntegrity.Validate(imported, original); }
            catch (InvalidOperationException) { ++checks; return; }
            throw new InvalidOperationException(message);
        }
        File.WriteAllText(importedFile, importedText + "class ExtraChange;\n");
        ExpectRejected("An extra edit in an adapted file was accepted");
        File.WriteAllText(importedFile, importedText);
        File.WriteAllText(originalFile, sourceText + "class ChangedBaseline;\n");
        ExpectRejected("An unreviewed source baseline was accepted");
        File.WriteAllText(originalFile, sourceText);
        File.WriteAllText(Path.Combine(imported, "BmrRuntime/Upstream/Extra.cs"), "class Extra;\n");
        ExpectRejected("An extra imported source file was accepted");
    }
    finally { Directory.Delete(temporary, true); }
});

var candidates = new List<(string Name, string Kind)>();
using (var csv = new TextFieldParser(Path.Combine(root, "docs/reference/BMRModuleInventory.csv"), Encoding.UTF8))
{
    csv.SetDelimiters(",");
    csv.HasFieldsEnclosedInQuotes = true;
    csv.ReadFields();
    while (!csv.EndOfData)
    {
        var row = csv.ReadFields()!;
        if (!row[0].Contains(".Crucible.")) candidates.Add((row[0], row[1]));
    }
}
Scenario("every inventoried non-Crucible module is compiled", () =>
{
    var expected = typeof(WorldState).Assembly.GetTypes().Where(t => !t.IsAbstract && t != typeof(DemoModule)
        && (t.IsSubclassOf(typeof(BossModule)) || t.IsSubclassOf(typeof(ZoneModule)))).Select(t => t.FullName!).Order().ToArray();
    Check(candidates.Select(c => c.Name).Order().SequenceEqual(expected), "Source inventory and compiled module types differ");
    foreach (var row in candidates)
        Check(typeof(WorldState).Assembly.GetType(row.Name) != null, $"Missing {row.Name}");
});

var bossCount = 0;
var zoneCount = 0;
foreach (var row in candidates)
{
    var registered = false;
    var constructed = false;
    var ticked = false;
    var phases = 0;
    var activePhase = -1;
    var oid = 0u;
    var error = "";
    try
    {
        var type = typeof(WorldState).Assembly.GetType(row.Name)!;
        var world = NewWorld();
        if (row.Kind == "BossModule")
        {
            var info = BossModuleRegistry.FindByType(type) ?? throw new InvalidOperationException("No valid States registration");
            oid = info.PrimaryActorOID;
            registered = oid != 0 && BossModuleRegistry.FindByOID(oid)?.ModuleType == type;
            var boss = AddBoss(world, oid);
            using var module = info.ModuleFactory(world, boss);
            constructed = true;
            phases = module.StateMachine.Phases.Count;
            Check(phases > 0, "State machine has no phases");
            world.Execute(new ActorState.OpMove(boss.InstanceID, new(module.Center.X, 0, module.Center.Z, 0)));
            boss.InCombat = true;
            module.Update();
            Tick(world, module, .1);
            activePhase = module.StateMachine.ActivePhaseIndex;
            ticked = true;
            ++bossCount;
        }
        else
        {
            var attr = type.GetCustomAttribute<ZoneModuleInfoAttribute>() ?? throw new InvalidOperationException("Missing ZoneModuleInfo");
            world.Execute(new WorldState.OpZoneChange((ushort)attr.TerritoryID, (ushort)attr.CFCID));
            using var module = ZoneModuleRegistry.CreateModule(world, attr.CFCID, BossModuleInfo.Maturity.WIP);
            registered = module?.GetType() == type;
            Check(registered, "Zone module is not discoverable by CFC");
            constructed = true;
            module!.Update();
            ticked = true;
            ++zoneCount;
        }
        if (!registered) throw new InvalidOperationException("Primary OID is zero or shadowed by another module");
    }
    catch (Exception ex)
    {
        error = ex.GetBaseException().Message;
        failures.Add($"{row.Name}: {error}");
        Console.WriteLine($"FAIL {row.Name}: {ex}");
    }
    coverage.Add(string.Join(',', Quote(row.Name), row.Kind, $"0x{oid:X}", registered, constructed, ticked, phases, activePhase, Quote(error)));
}
Console.WriteLine($"Module startup coverage: {bossCount}/{candidates.Count(c => c.Kind == "BossModule")} bosses, {zoneCount}/{candidates.Count(c => c.Kind == "ZoneModule")} zones; does not certify full encounter behavior");

Scenario("original Chopper AOE starts, counts hits and clears only on cast end", () =>
{
    var world = NewWorld();
    var boss = AddBoss(world, 0x19B);
    using var module = BossModuleRegistry.CreateModuleForActor(world, boss, BossModuleInfo.Maturity.WIP)!;
    module.Update();
    Check(module.StateMachine.ActivePhaseIndex == -1 && module.Components.Count == 0, "Prepull activated mechanics");
    Check(BmrTimeline.Capture(module).Entries.Count == 0, "Prepull fabricated a running timeline");
    boss.InCombat = true;
    module.Update();
    var aoes = module.Components.OfType<SimpleAOEs>().Single();
    world.Execute(new ActorState.OpCastInfo(1, new() { Action = new(ActionType.Spell, 351u), TotalTime = 3, Location = new(100, 0, 100) }));
    Check(aoes.ActiveAOEs(0, world.Party[0]!).Length == 1, "Expected one Chopper cast");
    var aoe = aoes.ActiveAOEs(0, world.Party[0]!)[0];
    Check(aoe.Shape is AOEShapeCircle { Radius: 6.15f }, "Chopper geometry differs from the original");
    Check(aoe.Activation == start.AddSeconds(3.3), "NPC finish padding must be applied once");
    world.Execute(new ActorState.OpCastEvent(1, new(new(ActionType.Spell, 351u), 1, .1f, 1, new(100, 0, 100), 7, 0, default)));
    Check(aoes.NumCasts == 1 && aoes.ActiveAOEs(0, world.Party[0]!).Length == 1, "An effect packet prematurely removed SimpleAOEs");
    world.Execute(new ActorState.OpCastInfo(1, null));
    Check(aoes.ActiveAOEs(0, world.Party[0]!).IsEmpty, "Cast end did not remove the AOE");
    world.Execute(new ActorState.OpCastInfo(1, new() { Action = new(ActionType.Spell, 351u), TotalTime = 3 }));
    world.Execute(new ActorState.OpDestroy(1));
    Check(aoes.ActiveAOEs(0, world.Party[0]!).IsEmpty, "Actor destruction did not clean up its active cast");
    module.Dispose();
    module.Dispose();
    Check(module.Components.Count == 0, "Disposal left active components");
    Check(BmrTimeline.Capture(module).Entries.Count == 0, "Disposal left a running timeline");
});

Scenario("P1S timeline waits for actual casts and preserves damage flags", () =>
{
    var world = NewWorld();
    var boss = AddBoss(world, 0x3522);
    using var module = BossModuleRegistry.CreateModuleForActor(world, boss, BossModuleInfo.Maturity.WIP)!;
    boss.InCombat = true;
    module.Update();
    var timeline = BmrTimeline.Capture(module);
    var tankbuster = timeline.Entries.First(e => e.Hints.HasFlag(StateMachine.StateHint.Tankbuster));
    Check(tankbuster.Name == "Tankbuster", "Tankbuster state label was lost");
    Check(Math.Abs((tankbuster.ExpectedEnd - start).TotalSeconds - 13.2) < .001, "Original 8.2+5 second timeline was changed");
    Check(BmrTimeline.Capture(module, 1000).Branches.Count == 2, "Unresolved P1S branches must remain alternatives");
    var initial = module.StateMachine.ActiveState!.ID;
    Tick(world, module, 20);
    Check(module.StateMachine.ActiveState!.ID == initial, "Elapsed estimated time advanced an event-driven cast state");
    timeline = BmrTimeline.Capture(module);
    Check(timeline.ElapsedInState == 20 && timeline.Entries[0].ExpectedEnd == world.CurrentTime, "An overdue state's forecast or raw elapsed time was lost");
    Check(timeline.Entries.First(e => e.Hints.HasFlag(StateMachine.StateHint.Tankbuster)).ExpectedEnd == start.AddSeconds(25), "An overdue state left future damage predictions in the past");
    world.Execute(new ActorState.OpCastInfo(1, new() { Action = ActionID.MakeSpell(BossMod.Endwalker.Savage.P1SErichthonios.AID.HeavyHand), TotalTime = 5 }));
    module.Update();
    Check(module.StateMachine.ActiveState!.ID != initial, "Actual cast did not advance the timeline");
    Check(module.StateMachine.ActiveState.EndHint.HasFlag(StateMachine.StateHint.Tankbuster), "Cast-end damage flag was lost");
    var duringCast = module.StateMachine.ActiveState.ID;
    Tick(world, module, 25);
    world.Execute(new ActorState.OpCastInfo(1, null));
    module.Update();
    Check(module.StateMachine.ActiveState!.ID != duringCast, "Cast finish did not advance the timeline");
});

Scenario("Calcabrina resolves a late gaze source without using the initial doll", () =>
{
    var world = NewWorld();
    var boss = AddBoss(world, 0x1503);
    using var module = BossModuleRegistry.CreateModuleForActor(world, boss, BossModuleInfo.Maturity.WIP)!;
    boss.InCombat = true;
    module.Update();
    var bait = module.Components.OfType<BaitAwayIcon>().Single();
    var gaze = module.Components.OfType<GenericGaze>().Single();
    bait.OnEventIcon(world.Party[0]!, 73, 100);
    gaze.OnEventIcon(world.Party[0]!, 73, 100);
    Check(bait.CurrentBaits.Count == 0 && gaze.ActiveEyes(0, world.Party[0]!).IsEmpty, "An absent Calcabrina was replaced with a doll");
    world.Execute(new ActorState.OpCreate(2, 0x1502, 18, 0, "Calcabrina", 0, ActorType.Enemy, Class.None, 60,
        new(100, 0, 90, 0), 2.25f, new(100000, 100000, 0, 0, 0), true, false, 0, 0, 0));
    bait.OnEventIcon(world.Party[0]!, 73, 100);
    Check(bait.CurrentBaits.Single().Source.InstanceID == 2, "Late-created Calcabrina was not used as the bait source");
    Check(gaze.ActiveEyes(0, world.Party[0]!).Length == 1, "Gaze failed after source arrival");
    world.Execute(new ActorState.OpDestroy(2));
    Check(gaze.ActiveEyes(0, world.Party[0]!).IsEmpty, "Gaze retained a destroyed source");
    Check(bait.CurrentBaits.Count == 0 && bait.ActiveBaits.Count == 0, "Bait retained a destroyed source");
    world.Execute(new ActorState.OpCreate(3, 0x1502, 18, 0, "Calcabrina", 0, ActorType.Enemy, Class.None, 60,
        new(100, 0, 90, 0), 2.25f, new(100000, 100000, 0, 0, 0), true, false, 0, 0, 0));
    bait.OnEventIcon(world.Party[0]!, 73, 100);
    Check(bait.CurrentBaits.Single().Source.InstanceID == 3, "A new source inherited the previous bait");
    world.Execute(new ActorState.OpDead(3, true));
    Check(bait.CurrentBaits.Count == 0, "Bait retained a dead source");
    bait.OnEventIcon(world.Party[0]!, 73, 100);
    Check(bait.CurrentBaits.Count == 0, "A late icon revived a dead source");
    world.Execute(new ActorState.OpCreate(4, 0x1502, 20, 0, "Calcabrina", 0, ActorType.Enemy, Class.None, 60,
        new(110, 0, 90, 0), 2.25f, new(100000, 100000, 0, 0, 0), true, false, 0, 0, 0));
    var target = world.Party[0]!;
    bait.OnEventIcon(target, 73, 100);
    gaze.OnEventIcon(target, 73, 100);
    var source = bait.CurrentBaits.Single().Source;
    Check(source.InstanceID == 4, "A lingering dead source displaced the new living source");
    var eyes = gaze.ActiveEyes(0, target);
    Check(eyes.Length == 1 && eyes[0].Position == source.Position, "Bait and gaze used different source positions");
    world.Execute(new ActorState.OpDestroy(target.InstanceID));
    Check(gaze.ActiveEyes(0, target).IsEmpty, "Gaze retained a destroyed player target");
    bait.Update();
    Check(bait.CurrentBaits.Count == 0 && bait.ActiveBaits.Count == 0, "Bait retained a destroyed player target");
});

Scenario("Castrum health hints resolve an absent then arriving second boss", () =>
{
    var world = NewWorld();
    var boss = AddBoss(world, 0x2ECC);
    using var module = BossModuleRegistry.CreateModuleForActor(world, boss, BossModuleInfo.Maturity.WIP)!;
    boss.InCombat = true;
    module.Update();
    var healths = module.Components.Single(c => c.GetType().Name == "BossHealths");
    var hints = new BossComponent.GlobalHints();
    healths.AddGlobalHints(hints);
    Check(hints.Single().Contains("Bottom: unknown"), "Missing second boss was treated as known HP");
    world.Execute(new ActorState.OpCreate(2, 0x2ED3, 18, 0, "Helldiver", 0, ActorType.Enemy, Class.None, 80,
        new(80, 0, -179, 0), 3.64f, new(25000, 100000, 0, 0, 0), true, false, 0, 0, 0));
    hints.Clear();
    healths.AddGlobalHints(hints);
    Check(hints.Single().Contains(25f.ToString("f1")), "Health hints did not recover when the second boss appeared");
});

Scenario("Enuo tower eligibility does not leak between module instances", () =>
{
    var world1 = NewWorld();
    var world2 = NewWorld();
    using var module1 = BossModuleRegistry.CreateModuleForActor(world1, AddBoss(world1, 0x4DC1), BossModuleInfo.Maturity.WIP)!;
    using var module2 = BossModuleRegistry.CreateModuleForActor(world2, AddBoss(world2, 0x4DC1), BossModuleInfo.Maturity.WIP)!;
    var type = typeof(WorldState).Assembly.GetType("BossMod.Modules.Dawntrail.Extreme.Ex8Enuo.EmptyShadowTower")!;
    var tower1 = (CastTowers)Activator.CreateInstance(type, module1)!;
    var tower2 = (CastTowers)Activator.CreateInstance(type, module2)!;
    tower1.OnEventIcon(world1.Party[0]!, 721, 100);
    var cast = new ActorCastInfo { Action = new(ActionType.Spell, 50013), TotalTime = 7, Location = new(100, 0, 100) };
    tower1.OnCastStarted(module1.PrimaryActor, cast);
    tower2.OnCastStarted(module2.PrimaryActor, cast);
    Check(tower1.Towers.Single().ForbiddenSoakers[0], "Icon did not exclude its target from the tower");
    Check(!tower2.Towers.Single().ForbiddenSoakers[0], "Another world inherited the previous tower's forbidden player");
});

Scenario("Thin Ice uses the supplied movement mode and camera without native hooks", () =>
{
    var world = NewWorld();
    using var module = BossModuleRegistry.CreateModuleForActor(world, AddBoss(world, 0x19B), BossModuleInfo.Maturity.WIP)!;
    var ice = new TestThinIce(module);
    ice.Mask.Set(0);
    var player = world.Party[0]!;
    player.PosRot.W = .75f;
    Check(ice.ActiveKnockbacks(0, player)[0].Direction == player.Rotation, "Standard movement ignored actor facing");
    world.Client.LegacyMovement = true;
    world.Client.CameraAzimuth = .25f.Radians();
    Check(ice.ActiveKnockbacks(0, player)[0].Direction == .25f.Radians() + 180f.Degrees(), "Legacy movement ignored the recorded camera");
    ice.Mask.Clear(0);
    Check(ice.ActiveKnockbacks(0, player).IsEmpty, "Ice remained after status removal");
});

Scenario("host session freeze, backward-time rejection and disposal", () =>
{
    using var session = new BmrSession("headless regression");
    session.BeginFrame(start);
    session.BeginFrame(start);
    Check(session.World.Frame.Duration == 0, "A paused frame advanced time");
    try { session.BeginFrame(start.AddSeconds(-1)); throw new InvalidOperationException("Backward time was accepted"); }
    catch (ArgumentOutOfRangeException) { ++checks; }
    session.Dispose();
    try { session.Update(); throw new InvalidOperationException("Disposed session still updates"); }
    catch (ObjectDisposedException) { ++checks; }
});

Scenario("host session discovers boss and zone modules from world operations", () =>
{
    using var session = new BmrSession("headless regression");
    session.BeginFrame(start);
    session.Execute(new WorldState.OpZoneChange(1036, 4));
    Check(session.ZoneModules.ActiveModule is BossMod.RealmReborn.Dungeon.D01Sastasha.D01Sastasha, "CFC did not select Sastasha's zone module");
    session.Execute(new ActorState.OpCreate(100, 0, 0, 0, "Player", 0, ActorType.Player, Class.PLD, 20,
        new(80, 0, -45, 0), .5f, new(10000, 10000, 0, 10000, 10000), true, true, 0, 0, 0));
    session.Execute(new PartyState.OpModify(0, new(100, 100, false, "Player")));
    session.Execute(new ActorState.OpCreate(1, 0x19B, 2, 0, "Chopper", 1204, ActorType.Enemy, Class.None, 20,
        new(80, 0, -45, 0), 2, new(100000, 100000, 0, 0, 0), true, false, 0, 0, 0));
    session.Execute(new ActorState.OpCombat(1, true));
    session.Update();
    var module = session.BossModules.ActiveModule;
    Check(module?.Info?.PrimaryActorOID == 0x19B && module.StateMachine.ActivePhaseIndex == 0, "World operations did not activate Chopper");
    session.Execute(new ActorState.OpDestroy(1));
    session.Update();
    Check(session.BossModules.ActiveModule == null && session.BossModules.LoadedModules.Count == 0, "Destroyed boss remained loaded");
    session.Execute(new WorldState.OpZoneChange(0, 0));
    Check(session.ZoneModules.ActiveModule == null, "Old zone module remained after a zone change");
});

Scenario("host session releases all managers and subscriptions after cleanup failures", () =>
{
    using var session = new BmrSession("cleanup failure regression");
    session.BeginFrame(start);
    var boss = AddBoss(session.World, 0x19B);
    boss.InCombat = true;
    var failingBoss = session.BossModules.PendingModules.Single();
    failingBoss.Update();
    failingBoss.StateMachine.ActiveState!.Exit += () => throw new InvalidOperationException("boss cleanup failure");
    var remainingBoss = BossModuleRegistry.CreateModuleForActor(session.World, boss, BossModuleInfo.Maturity.WIP)!;
    remainingBoss.Update();
    session.BossModules.PendingModules.Add(remainingBoss);
    session.ZoneModules.ActiveModule = new ThrowingZone(session.World);
    try { session.Dispose(); throw new InvalidOperationException("Cleanup failures were silently lost"); }
    catch (AggregateException ex)
    {
        Check(ex.Flatten().InnerExceptions.Select(e => e.Message).Order().SequenceEqual(new[] { "boss cleanup failure", "zone cleanup failure" }), "Not all cleanup failures were reported");
    }
    Check(session.ZoneModules.ActiveModule == null && session.BossModules.ActiveModule == null, "Disposal retained an active module");
    Check(session.BossModules.LoadedModules.Count == 0 && session.BossModules.PendingModules.Count == 0, "Disposal retained registered modules");
    Check(failingBoss.Components.Count == 0 && remainingBoss.Components.Count == 0, "A failed cleanup skipped the remaining components or modules");
    Check(!session.World.CurrentZoneChanged.HaveSubscribers(), "Zone subscriptions survived disposal");
    session.World.Execute(new ActorState.OpDestroy(1));
    AddBoss(session.World, 0x19B);
    session.World.Execute(new WorldState.OpZoneChange(1036, 4));
    Check(session.BossModules.PendingModules.Count == 0 && session.ZoneModules.ActiveModule == null, "A disposed manager reacted to actor or zone changes");
    var previousDemo = BossModuleManager.Config.ShowDemo;
    try
    {
        BossModuleManager.Config.ShowDemo = true;
        BossModuleManager.Config.Modified.Fire();
        Check(session.BossModules.LoadedModules.Count == 0, "Shared configuration reactivated a disposed manager");
    }
    finally
    {
        BossModuleManager.Config.ShowDemo = previousDemo;
        BossModuleManager.Config.Modified.Fire();
    }
    session.Dispose();
    Check(session.BossModules.PendingModules.Count == 0, "A second disposal changed the cleaned session");
});

if (args.Length > 2)
    File.WriteAllLines(args[2], coverage, new UTF8Encoding(false));
foreach (var log in logs.Where(l => l.Contains("ModuleRegistry") || l.Contains("ZoneModuleRegistry")))
    Console.WriteLine(log);
Console.WriteLine($"{(failures.Count == 0 ? "PASS" : "FAIL")}: {checks} assertions; {failures.Count} failures");
return failures.Count == 0 ? 0 : 1;

public class RecordingLog : DispatchProxy
{
    public List<string> Messages { get; set; } = [];
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.ReturnType != typeof(void)) throw new NotSupportedException(method?.Name);
        Messages.Add(string.Join(" ", args?.Select(a => a?.ToString()) ?? []));
        return null;
    }
}

public sealed class TestThinIce(BossModule module) : ThinIce(module, 10);

public sealed class ThrowingZone(WorldState world) : ZoneModule(world)
{
    protected override void Dispose(bool disposing) => throw new InvalidOperationException("zone cleanup failure");
}
