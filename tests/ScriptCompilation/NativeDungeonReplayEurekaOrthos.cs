using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunEurekaOrthos(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        RunEurekaTiamatNavigation(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunEurekaKaliya(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunEurekaFinalBosses(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(-300, 0, -300);
        object Value(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } property) return property.GetValue(obj)!;
                if (type.GetField(name, flags) is { } field) return field.GetValue(obj)!;
            }
            throw new MissingMemberException(obj.GetType().Name, name);
        }
        object Encounter(string id) => Value(Value(manager.Running[id], "Script"), "_encounter");
        object[] Items(object obj, string name) => ((IEnumerable)Value(obj, name)).Cast<object>().ToArray();
        object[] Group(string prefix) => previews().Where(p => ((string)Value(p, "Id")).Contains(prefix)).ToArray();
        var plugin = managerObject.GetType().Assembly.GetType("MasterPlayer.MasterPlayerPlugin")!
            .GetProperty("Instance")!.GetValue(null)!;
        object[] Lines() => ((IEnumerable)Value(plugin, "AoeVisibility").GetType().GetMethod("GetLines")!
            .Invoke(Value(plugin, "AoeVisibility"), null)!).Cast<object>().ToArray();
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        var runtimeAssembly = Assembly.Load("MasterPlayer.MechanicRuntime");
        var wdir = runtimeAssembly.GetType("BossMod.WDir")!;
        var angle = Activator.CreateInstance(runtimeAssembly.GetType("BossMod.Angle")!)!;
        foreach (var (territory, oid, floor) in new[] { (1099u, 0x3D54u, 10), (1100u, 0x3E77u, 20), (1101u, 0x3D9Au, 30),
            (1102u, 0x3D1Du, 40), (1103u, 0x3D9Cu, 50), (1104u, 0x3DA1u, 60), (1105u, 0x3D1Bu, 70),
            (1107u, 0x3D23u, 90), (1108u, 0x3CFCu, 99) })
        {
            var id = $"masterplayer.eureka.{floor}";
            var arenaCenter = floor is 40 or 60 or 99 ? new Vector3(-600, 0, -300) : center;
            load(id, [Header(territory),
                Frame(territory, 0, [Actor(1, oid, arenaCenter), Actor(100, 0, arenaCenter)], full: true, party: party),
                Frame(territory, .1, [Actor(1, oid, arenaCenter, combat: true)], party: party)]);
            controller.Seek(.01);
            Check(dangers() == 0 && Group("eureka/precombat/arena").Length == 1, "Eureka precombat scope expanded.");
            controller.Seek(.2);
            dynamic arena = Value(Encounter(id), "Arena");
            var sourceBounds = floor is 10 or 30 or 90
                ? Activator.CreateInstance(runtimeAssembly.GetType("BossMod.ArenaBoundsSquare")!, [floor == 90 ? 20f : 19.5f, angle, .5f, false])!
                : Activator.CreateInstance(runtimeAssembly.GetType("BossMod.ArenaBoundsCircle")!, [floor is 40 or 60 or 70 ? 20f : 19.5f, .5f, false])!;
            var contains = sourceBounds.GetType().GetMethod("Contains", flags)!;
            var count = 0;
            for (var x = -20.25f; x <= 20.25f; x += .5f)
                for (var z = -20.25f; z <= 20.25f; z += .5f)
                {
                    var expected = (bool)contains.Invoke(sourceBounds, [Activator.CreateInstance(wdir, [x, z])!])!;
                    Check((bool)arena.Contains(arenaCenter + new Vector3(x, 0, z), 0f) == expected, "Eureka source boundary differs.");
                    ++count;
                }
            Check((Vector3)Value((object)arena, "Position") == arenaCenter, "Eureka source center moved.");
            stop();
            Console.WriteLine($"PASS native Eureka {floor} actual territory primary-only ownership precombat shared previews no navigation source square/circle {count} point oracle");
        }
        string NativeEffect(double t, ulong source, uint action, ulong target = 0) => JsonSerializer.Serialize(new
        {
            t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = source, ActionId = action, ActionType = 1, MainTargetId = target, GlobalSequence = 7u, Targets = Array.Empty<ulong>() },
        });
        {
            const string id = "masterplayer.eureka.70";
            object[] roster = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
            object[] tether(uint type) => [new { Id = type, TargetId = 100ul }];
            load(id,
            [
                Header(1105),
                Frame(1105, 0, [Actor(1, 0x3D1B, center, combat: true), Actor(2, 0x233C, center),
                    Actor(20, 0x3D1C, center + Vector3.UnitX * 10), Actor(21, 0x3D1C, center - Vector3.UnitX * 10),
                    Actor(100, 0, center + Vector3.UnitZ * 10), Actor(101, 0, center + Vector3.UnitX * 5)],
                    full: true, party: roster),
                Frame(1105, .1, [Actor(2, 0x233C, center, cast: Cast(31436, 6, center, startedAt: .1))], party: roster),
                Frame(1105, .2, [Actor(2, 0x233C, center)], party: roster),
                Frame(1105, .3, [Actor(2, 0x233C, center, cast: Cast(31445, 5, center, target: 101, startedAt: .3))], party: roster),
                Frame(1105, .4, [Actor(2, 0x233C, center)], party: roster),
                NativeEffect(.5, 2, 31445, 100), NativeEffect(.6, 2, 31445, 101),
                Frame(1105, .7, [Actor(2, 0x233C, center, cast: Cast(31439, 3, center, startedAt: .7))], party: roster),
                CancelHoH(.75, 2, 31439),
                Frame(1105, .8, [Actor(2, 0x233C, center)], party: roster),
                Frame(1105, .9, [Actor(1, 0x3D1B, center, combat: true, tethers: tether(57))], party: roster),
                Frame(1105, 1, [Actor(1, 0x3D1B, center, combat: true, tethers: tether(1))], party: roster),
                Frame(1105, 1.1, [Actor(2, 0x233C, center, cast: Cast(31442, 5, center, startedAt: 1.1))], party: roster),
                Frame(1105, 1.2, [Actor(2, 0x233C, center)], party: roster),
                Frame(1105, 1.3, [Actor(2, 0x233C, center, cast: Cast(31437, 6, center, startedAt: 1.3))], party: roster),
                Frame(1105, 1.4, [Actor(2, 0x233C, center)], party: roster),
                Frame(1105, 1.5, [Actor(2, 0x233C, center, cast: Cast(31437, 6, center, startedAt: 1.5))], party: roster),
                Frame(1105, 1.6, [Actor(2, 0x233C, center)], party: roster),
                Frame(1105, 2, [], removed: [1], party: roster),
            ]);
            controller.Seek(.45);
            var encounter = Encounter(id);
            var shatters = Items(encounter, "EurekaShatters");
            Check(shatters.Length == 2 && Items(encounter, "EurekaSteelClaws").Length == 1
                && Group("eureka/shatter/").Length == 2, "Eureka70 real Finish removed frozen Shatter or delayable claw.");
            Check((DateTime)Value(shatters[0], "Activation")
                == ((DateTime)Value(Value(shatters[0], "Trigger"), "FinishAt")).AddSeconds(.5),
                "Eureka70 Shatter trigger clock changed.");
            controller.Seek(.55);
            Check(Items(Encounter(id), "EurekaSteelClaws").Length == 1, "Eureka70 wrong main target consumed a claw.");
            controller.Seek(.65);
            Check(Items(Encounter(id), "EurekaSteelClaws").Length == 0, "Eureka70 complete effect failed claw consumption.");
            controller.Seek(.77);
            Check(Items(Encounter(id), "EurekaShatters").Length == 2,
                "Eureka70 queued Cancel consumed Shatter ahead of unchanged raw CastInfo.");
            controller.Seek(.85);
            Check(Items(Encounter(id), "EurekaShatters").Length == 0 && Group("eureka/shatter/").Length == 0,
                "Eureka70 any Shatter Finish failed all-clear.");
            controller.Seek(1.05);
            var line = Lines().Single(p => ((string)Value(p, "Id")).Contains("eureka/tether/ferocity/line/"));
            Check((bool)Value(line, "Safe") && dangers() > 0, "Eureka70 good line lost source-independent navigation.");
            controller.Seek(1.35);
            Check(Items(Encounter(id), "EurekaShatters").Length == 0, "Eureka70 Ferocity Finish reset the latch.");
            controller.Seek(1.55);
            Check(Items(Encounter(id), "EurekaShatters").Length == 2
                && Value(Items(Encounter(id), "EurekaShatters")[0], "Shape").GetType().Name == "CircleTemplate",
                "Eureka70 Turn Finish failed latch reset and circular crystal prediction.");
            controller.Seek(.15);
            Check(Items(Encounter(id), "EurekaShatters").Length == 2
                && Items(Encounter(id), "EurekaSteelClaws").Length == 0 && Lines().Length == 0,
                "Eureka70 reverse Seek retained later personal state.");
            stop();
            Check(previews().Length == 0 && Lines().Length == 0 && dangers() == 0, "Eureka70 owner cleanup leaked.");
            Console.WriteLine("PASS native Eureka70 frozen Shatter source clocks any Finish all-clear Ferocity latch good-line shared display navigation delayable SteelClaw complete main-target FIFO reverse Seek owner cleanup");
        }
        {
            const string id = "masterplayer.eureka.70";
            object[] oldParty = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
            object[] newParty = [new { Slot = 0, EntityId = 100ul }, new { Slot = 5, EntityId = 101ul }];
            load(id,
            [
                Header(1105),
                Frame(1105, 0, [Actor(1, 0x3D1B, center, combat: true), Actor(2, 0x233C, center), Actor(3, 0x233C, center),
                    Actor(100, 0, center + Vector3.UnitZ * 10), Actor(101, 0, center + Vector3.UnitX * 5)],
                    full: true, party: oldParty),
                Frame(1105, .1, [Actor(2, 0x233C, center,
                    cast: Cast(31445, 5, center, target: 101, startedAt: .1)),
                    Actor(3, 0x233C, center, cast: Cast(31445, 5, center, target: 101, startedAt: .1))], party: newParty),
                Frame(1105, .2, [Actor(2, 0x233C, center), Actor(3, 0x233C, center)], removed: [100, 101], party: []),
                NativeEffect(.25, 1, 31445, 101),
                Frame(1105, .3, [Actor(100, 0, center + Vector3.UnitZ * 10),
                    Actor(101, 0, center - Vector3.UnitX * 8)], party: newParty),
                Frame(1105, .4, [Actor(2, 0x233C, center,
                    cast: Cast(31445, 5, center, target: 101, startedAt: .4))], party: newParty),
                NativeEffect(.5, 1, 31445, 101), NativeEffect(.6, 1, 31445, 101),
                Frame(1105, 1, [], party: newParty),
            ]);
            controller.Seek(.15);
            Check(Items(Encounter(id), "EurekaSteelClaws").Length == 2
                && Items(Encounter(id), "EurekaSteelClaws").All(c => (int)Value(c, "Slot") == 1),
                "Eureka70 same-frame raw captured uncommitted party slot.");
            controller.Seek(.22);
            Check(Items(Encounter(id), "EurekaSteelClaws").Length == 2
                && Items(Encounter(id), "EurekaSteelClaws").All(c => (bool)Value(Value(c, "Target"), "Removed"))
                && previews().Length == 0,
                "Eureka70 missing viewer cleared global claw or retained personal drawing.");
            controller.Seek(.28);
            Check(Items(Encounter(id), "EurekaSteelClaws").Length == 1,
                "Eureka70 missing-view complete effect failed main-target consumption.");
            controller.Seek(.45);
            var claws = Items(Encounter(id), "EurekaSteelClaws");
            Check(claws.Length == 2, "Eureka70 same-ID reconstruction lost old or new claw.");
            var oldTarget = Value(claws[0], "Target");
            Check(claws.Length == 2 && (int)Value(claws[0], "Slot") == 1 && (int)Value(claws[1], "Slot") == 5
                && !ReferenceEquals(oldTarget, Value(claws[1], "Target")) && (bool)Value(oldTarget, "Removed")
                && (Vector3)Value(Value(oldTarget, "Actor"), "Position") == center + Vector3.UnitX * 5,
                "Eureka70 replacement actor rebound old reference or missed committed slot.");
            controller.Seek(.65);
            Check(Items(Encounter(id), "EurekaSteelClaws").Length == 0,
                "Eureka70 repeated full main-target effects failed old/new generation FIFO.");
            controller.Seek(.15);
            Check(Items(Encounter(id), "EurekaSteelClaws").All(c => (int)Value(c, "Slot") == 1),
                "Eureka70 reverse Seek retained replacement roster slot.");
            stop();
            Check(previews().Length == 0 && Lines().Length == 0 && dangers() == 0,
                "Eureka70 missing-view owner cleanup leaked.");
            Console.WriteLine("PASS native Eureka70 old-party start slot missing-view Finish history full effect same-ID replacement reference new-party slot reverse Seek owner cleanup");
        }
        {
            const string id = "masterplayer.eureka.50";
            object[] roster = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
            object[] tether = [new { Id = (ushort)17, TargetId = 100ul }];
            load(id,
            [
                Header(1103),
                Frame(1103, 0, [Actor(1, 0x3D9C, center, combat: true), Actor(2, 0x233C, center),
                    Actor(20, 0x3D9D, center + Vector3.UnitX * 4, tethers: tether),
                    Actor(100, 0, center + Vector3.UnitZ * 10), Actor(101, 0, center + Vector3.UnitX * 10)],
                    full: true, party: roster),
                Frame(1103, .1, [Actor(20, 0x3D9D, center + Vector3.UnitX * 4, modelState: 4, tethers: tether)], party: roster),
                Frame(1103, .2, [Actor(2, 0x233C, center, cast: Cast(31851, 5, center, startedAt: .2))], party: roster),
                Frame(1103, .3, [Actor(2, 0x233C, center)], party: roster),
                NativeEffect(.4, 2, 31854), NativeEffect(.5, 2, 31853),
                Frame(1103, .6, [Actor(2, 0x233C, center, cast: Cast(31855, 5, center, target: 101, startedAt: .6))], party: roster),
                Frame(1103, .7, [Actor(101, 0, center + Vector3.UnitX * 14)], party: roster),
                NativeEffect(.8, 20, 31865),
                Frame(1103, 1, [Actor(2, 0x233C, center)], party: roster),
                Frame(1103, 2, [], removed: [1], party: roster),
            ]);
            controller.Seek(.15);
            Check(Items(Encounter(id), "EurekaVoiceModels").Length == 2 && Items(Encounter(id), "EurekaOrbs").Length == 1
                && Group("eureka/orb/").Length == 1 && Lines().Count(p => ((string)Value(p, "Id")).Contains("eureka/tether/orb/line/")) == 1,
                "Eureka50 activation raw tether model transition and shared orb/line display missing.");
            controller.Seek(.35);
            Check(Items(Encounter(id), "EurekaVoiceCasts").Length == 2
                && Items(Encounter(id), "EurekaVoiceModels").Length == 2, "Eureka50 Finish erased voices or merged model history.");
            controller.Seek(.55);
            Check(Items(Encounter(id), "EurekaVoiceCasts").Length == 0 && Group("eureka/voice/").Length == 2,
                "Eureka50 complete effects failed FIFO/reveal.");
            controller.Seek(.75);
            var charge = Items(Encounter(id), "EurekaCharges").Single();
            Check(MathF.Abs((float)Value(charge, "Length") - 14) < .01f && (float)Value(charge, "HalfWidth") == 4
                && Group("eureka/charge/").Length == 1 && dangers() > 0, "Eureka50 original-target dynamic charge or navigation missing.");
            controller.Seek(.9);
            Check(Items(Encounter(id), "EurekaOrbs").Length == 0 && Group("eureka/orb/").Length == 0,
                "Eureka50 actual original caster effect failed orb remove.");
            controller.Seek(1.1);
            Check(Items(Encounter(id), "EurekaCharges").Length == 0 && Group("eureka/charge/").Length == 0,
                "Eureka50 observed charge Finish retained component.");
            controller.Seek(.15);
            Check(Items(Encounter(id), "EurekaOrbs").Length == 1 && Items(Encounter(id), "EurekaCharges").Length == 0,
                "Eureka50 reverse Seek retained future effects.");
            stop();
            Check(previews().Length == 0 && Lines().Length == 0 && dangers() == 0, "Eureka50 owner cleanup leaked.");
            Console.WriteLine("PASS native Eureka50 raw tether activation model/raw voice isolation complete packet FIFO original dynamic charge circle/capsule line display navigation true Finish reverse Seek owner cleanup");
        }
        {
            const string id = "masterplayer.eureka.60";
            var middle = new Vector3(-600, 0, -300);
            load(id,
            [
                Header(1104),
                Frame(1104, 0, [Actor(1, 0x3DA1, middle, combat: true), Actor(2, 0x233C, middle),
                    Actor(100, 0, middle + Vector3.UnitZ * 8)], full: true, party: party),
                Frame(1104, .1, [Actor(2, 0x233C, middle, cast: Cast(31867, 1, middle, startedAt: .1))], party: party),
                Frame(1104, .2, [Actor(2, 0x233C, middle), Actor(20, 0x3DA2, middle + Vector3.UnitX * 8)], party: party),
                Frame(1104, .3, [Actor(2, 0x233C, middle, cast: Cast(31867, 1, middle, .9f, startedAt: .3))], party: party),
                Frame(1104, .4, [Actor(2, 0x233C, middle)], party: party),
                NativeEffect(.5, 2, 31872), NativeEffect(.6, 2, 31868),
                Frame(1104, .7, [], removed: [20], party: party),
                Frame(1104, .8, [Actor(2, 0x233C, middle, cast: Cast(31876, 5, middle, startedAt: .8))], party: party),
                Frame(1104, 1, [Actor(2, 0x233C, middle)], party: party),
                Frame(1104, 1.1, [Actor(2, 0x233C, middle, cast: Cast(31874, 2.5, middle, startedAt: 1.1))], party: party),
                Frame(1104, 1.2, [Actor(2, 0x233C, middle)], party: party),
                Frame(1104, 2, [], removed: [1], party: party),
            ]);
            controller.Seek(.45);
            Check(Items(Encounter(id), "EurekaSwipes").Length == 2 && Items(Encounter(id), "EurekaShocks").Length == 1
                && Group("eureka/swipe/").Length == 2 && Group("eureka/shock/").Length == 1, "Eureka60 first two source previews missing.");
            var shock = Items(Encounter(id), "EurekaShocks").Single();
            var born = (DateTime)Value(shock, "PreparedAt");
            Check(born >= Start.AddSeconds(.2) && born < Start.AddSeconds(.3)
                && (DateTime)Value(shock, "Activation") == born.AddSeconds(13d),
                $"Eureka60 birth processing clock changed: {born:O}, {Value(shock, "Activation"):O}.");
            controller.Seek(.75);
            Check(Items(Encounter(id), "EurekaSwipes").Length == 0 && Items(Encounter(id), "EurekaShocks").Length == 1,
                "Eureka60 repeated full effects or destroyed Shock history differs.");
            controller.Seek(.9);
            Check(Items(Encounter(id), "EurekaGroans").Length == 1 && Group("eureka/groan/").Length > 0 && dangers() > 0,
                "Eureka60 original knockback model preview or independent navigation missing.");
            controller.Seek(1.3);
            Check(Items(Encounter(id), "EurekaShocks").Length == 0 && Items(Encounter(id), "EurekaGroans").Length == 0,
                "Eureka60 actual Shock global Finish or Groan withdrawal missing.");
            controller.Seek(.45);
            Check(Items(Encounter(id), "EurekaSwipes").Length == 2 && Items(Encounter(id), "EurekaShocks").Length == 1,
                "Eureka60 reverse Seek failed independent source histories.");
            stop();
            Check(previews().Length == 0 && dangers() == 0, "Eureka60 owner cleanup leaked.");
            Console.WriteLine("PASS native Eureka60 source FIFO first-two clocks repeated complete packets birth quantization destroy history independent knockback navigation actual Shock Finish reverse Seek owner cleanup");
        }
        {
            const string id = "masterplayer.eureka.40";
            var middle = new Vector3(-600, 0, -300);
            object[] roster = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
            string Timeline(double t, uint value) => JsonSerializer.Serialize(new { t, kind = "PlayActionTimeline",
                type = "PromeRotation.LogSystem.LogSystemPlayActionTimelineEvent",
                data = new { SourceId = 1ul, TimelineId = value } });
            load(id,
            [
                Header(1102),
                Frame(1102, 0, [Actor(1, 0x3D1D, middle, combat: true), Actor(2, 0x233C, middle),
                    Actor(100, 0, middle + Vector3.UnitZ * 8), Actor(101, 0, middle - Vector3.UnitX * 8)],
                    full: true, party: roster),
                Frame(1102, .1, [Actor(2, 0x233C, middle, cast: Cast(31468, 5, middle, startedAt: .1))], party: roster),
                Frame(1102, 5.16, [Actor(100, 0, middle + Vector3.UnitZ * 11.137f)], party: roster),
                Frame(1102, 5.5, [Actor(2, 0x233C, middle)], party: roster),
                Frame(1102, 6, [Actor(20, 0x1E8910, middle, eventState: 7)], party: roster),
                Frame(1102, 6.1, [Actor(2, 0x233C, middle, cast: Cast(31471, 5, middle, startedAt: 6.1))], party: roster),
                Frame(1102, 12, [Actor(2, 0x233C, middle)], party: roster),
                Timeline(12.1, 0x1E3A), Timeline(12.2, 0x1E43),
                Frame(1102, 12.3, [Actor(1, 0x3D1D, middle + Vector3.UnitX, combat: true, rotation: .7f)], party: roster),
                Timeline(12.4, 0x1E43),
                Frame(1102, 12.5, [Actor(2, 0x233C, middle, cast: Cast(31471, 5, middle, startedAt: 12.5))], party: roster),
                Frame(1102, 12.6, [Actor(2, 0x233C, middle)], party: roster),
                Frame(1102, 13, [Actor(2, 0x233C, middle, cast: Cast(31467, 6, middle, startedAt: 13))], party: roster),
                Frame(1102, 14, [], removed: [20], party: roster),
            ]);
            controller.Seek(5.17);
            var state = Value(Encounter(id), "EurekaTwister");
            Check(Items(state, "Positions").Length == 2
                && (Vector3)Items(state, "Positions")[0] == middle + Vector3.UnitZ * 11.137f,
                "Eureka40 actual frame sample lost current party position.");
            var activation = (DateTime)Value(state, "Activation");
            Check(activation > Start.AddSeconds(5.75) && activation < Start.AddSeconds(5.9),
                "Eureka40 actual Update sample clock changed.");
            controller.Seek(6.05);
            Check(Items(Value(Encounter(id), "EurekaTwister"), "Positions").Length == 0
                && Group("eureka/twister/").Length == 0, "Eureka40 State7 Created did not clear or incorrectly displayed.");
            controller.Seek(11.5);
            Check(Items(Value(Encounter(id), "EurekaDiveTwister"), "Positions").Length == 0,
                "Eureka40 old State7 failed blocking Update.");
            controller.Seek(12.05);
            Check(Items(Value(Encounter(id), "EurekaDiveTwister"), "Positions").Length == 2,
                "Eureka40 Finish fallback lost history beside old State7.");
            controller.Seek(12.45);
            Check(Group("eureka/dive").Length == 1 && (float)Value(Value(Encounter(id), "eurekaDive"), "Rotation") == .7f,
                "Eureka40 Timeline failed current orientation replacement.");
            controller.Seek(12.7);
            Check(Group("eureka/dive").Length == 0, "Eureka40 helper true Dive Finish did not withdraw Timeline.");
            controller.Seek(13.1);
            Check(Items(Encounter(id), "EurekaTurbines").Length == 1 && dangers() > 0
                && Group("eureka/turbine/").Length > 0, "Eureka40 actual adapter lost knockback model projection or navigation.");
            controller.Seek(5.17);
            Check(Items(Value(Encounter(id), "EurekaTwister"), "Positions").Length == 2
                && Items(Encounter(id), "EurekaTurbines").Length == 0, "Eureka40 reverse Seek retained later models.");
            stop();
            Check(previews().Length == 0 && dangers() == 0, "Eureka40 owner cleanup failed.");
            Console.WriteLine("PASS native Eureka40 per-frame party sample normal and fallback clocks independent twisters State7 Created and blocking Timeline overwrite helper Finish production knockback navigation reverse Seek owner cleanup");

            var wpos = runtimeAssembly.GetType("BossMod.WPos")!;
            var angleType = runtimeAssembly.GetType("BossMod.Angle")!;
            var origin = Activator.CreateInstance(wpos, [middle.X, middle.Z])!;
            var adapt = runtimeObject.GetType().Assembly.GetType("MasterPlayer.Mechanics.Dungeons.DungeonOutput")!
                .GetMethod("EurekaTurbineDistance", BindingFlags.Static | BindingFlags.NonPublic)!;
            var inverted = runtimeAssembly.GetType("BossMod.SDInvertedCircle")!;
            var cone = runtimeAssembly.GetType("BossMod.SDCone")!;
            var shape = runtimeAssembly.GetType("BossMod.ShapeDistance")!;
            var intersection = runtimeAssembly.GetType("BossMod.SDIntersection")!;
            var count = 0;
            foreach (var winds in new Vector3[][] { [], [middle + Vector3.UnitZ * 10],
                [middle + Vector3.UnitZ * 10, middle - Vector3.UnitZ * 10] })
            {
                var shapes = Array.CreateInstance(shape, winds.Length + 1);
                shapes.SetValue(Activator.CreateInstance(inverted, [origin, 5f]), winds.Length);
                for (var i = 0; i < winds.Length; ++i)
                {
                    var direction = Activator.CreateInstance(angleType, [MathF.Atan2(winds[i].X - middle.X, winds[i].Z - middle.Z)])!;
                    var half = Activator.CreateInstance(angleType, [20f * (MathF.PI / 180f)])!;
                    shapes.SetValue(Activator.CreateInstance(cone, [origin, 20f, direction, half]), i);
                }
                var source = Activator.CreateInstance(intersection, [shapes])!;
                var distance = intersection.GetMethod("Distance")!;
                for (var x = -20; x <= 20; ++x)
                    for (var z = -20; z <= 20; ++z)
                    {
                        var p = Activator.CreateInstance(wpos, [middle.X + x, middle.Z + z])!;
                        var expected = (float)distance.Invoke(source, [p])!;
                        var actual = (float)adapt.Invoke(null, [middle, winds, middle.X + x, middle.Z + z])!;
                        Check(MathF.Abs(actual - expected) < .00001f, "Eureka40 source intersection distance differs.");
                        ++count;
                    }
            }
            Console.WriteLine($"PASS native Eureka40 {count} direct source intersection inverted-circle and cone distance oracle points no-wind single-wind opposite-wind");
        }
        {
            const string id = "masterplayer.eureka.30";
            object[] chaseParty = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
            string Native(double at, uint action, ulong target, Vector3? position = null) => JsonSerializer.Serialize(new
            {
                t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = 2ul, ActionId = action, MainTargetId = target, TargetPosition = position,
                    GlobalSequence = 7u, Targets = Array.Empty<ulong>() }
            }, new JsonSerializerOptions { IncludeFields = true });
            load(id,
            [
                Header(1101),
                Frame(1101, 0, [Actor(1, 0x3D9A, center, combat: true), Actor(2, 0x233C, center),
                    Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitZ * 6)], full: true, party: chaseParty),
                JsonSerializer.Serialize(new { t = .1, kind = "TargetIcon",
                    type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                    data = new { SourceId = 101ul, TargetId = 100ul, MarkerId = 197u } }),
                CastEvent(.2, 2, 31847, 5, center),
                Frame(1101, .3, [Actor(2, 0x233C, center, cast: Cast(31847, 5, center, startedAt: .2))], party: chaseParty),
                Frame(1101, .4, [Actor(2, 0x233C, center)], party: chaseParty),
                Native(1, 31847, 101, center - Vector3.UnitZ * 10),
                EffectEvent(1.01, 2, 31847, 7, target: 101),
                Native(2, 31848, 999),
                Native(3, 31848, 999, center),
                Native(4, 31848, 2, center - Vector3.UnitZ * 10),
                Native(5, 31848, 999, center),
                Native(6, 31848, 999, center),
                Frame(1101, 7, [Actor(20, 0x3D9B, center + Vector3.UnitX * 10,
                    dead: true, targetable: false, eventState: 7, animationState1: 1)], party: chaseParty),
                Frame(1101, 8, [], removed: [20], party: chaseParty)
            ]);
            controller.Seek(.35);
            var first = Items(Encounter(id), "EurekaChasers").Single();
            Check((ulong)Value(Value(Value(first, "Target"), "Actor"), "Id") == 101
                && (int)Value(first, "Remaining") == 5 && (float)Value(first, "MoveDistance") == 0,
                "Eureka30 source icon identity or first raw activation differs.");
            Check(Group("eureka/whei/").Length >= 1 && dangers() == 1, "Eureka30 shared preview/navigation missing.");
            controller.Seek(.5);
            Check(Items(Encounter(id), "EurekaChasers").Length == 1, "Eureka30 cancellation erased source chase history.");
            controller.Seek(1.1);
            var chaser = Items(Encounter(id), "EurekaChasers").Single();
            Check((int)Value(chaser, "Remaining") == 4
                && (Vector3)Value(chaser, "Previous") == center + Vector3.UnitZ * 6
                && (DateTime)Value(chaser, "Activation") == Start.AddSeconds(3),
                "Eureka30 complete main target precedence or consumption clock differs.");
            controller.Seek(2.1);
            Check((int)Value(Items(Encounter(id), "EurekaChasers").Single(), "Remaining") == 4,
                "Eureka30 old recording fabricated effect position.");
            controller.Seek(4.1);
            chaser = Items(Encounter(id), "EurekaChasers").Single();
            var quantizedCenter = (Vector3)Encounter(id).GetType().GetMethod("QuantizedDungeonPosition", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [center])!;
            Check((int)Value(chaser, "Remaining") == 2 && (Vector3)Value(chaser, "Previous") == quantizedCenter,
                $"Eureka30 repeated sequence or caster self-target position differs: remaining={Value(chaser, "Remaining")}, previous={Value(chaser, "Previous")}.");
            controller.Seek(6.1);
            Check(Items(Encounter(id), "EurekaChasers").Length == 0 && Group("eureka/whei/").Length == 0,
                "Eureka30 five real hits failed final removal.");
            controller.Seek(7.1);
            var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
            Check(Group("eureka/head/").Length == 1 && dangers() >= 30
                && Items(snapshot, "HardObstacles").Length == 0
                && Items(snapshot, "DangerAreas").All(a => Items(a, "DangerWindows").All(w =>
                    Value(w, "DangerUntil") is DateTime && (bool)Value(w, "HasMotionEnvelope"))),
                "Eureka30 head source filter or finite motion windows lost.");
            controller.Seek(8.1);
            Check(Group("eureka/head/").Length == 0 && dangers() == 0, "Eureka30 live head removal retained navigation.");
            controller.Seek(1.1);
            Check((int)Value(Items(Encounter(id), "EurekaChasers").Single(), "Remaining") == 4, "Eureka30 reverse Seek retained future consumption.");
            stop();
            Check(previews().Length == 0 && dangers() == 0, "Eureka30 owner withdrawal retained output.");
            Console.WriteLine("PASS native Eureka30 icon source raw-only creation cancellation history main target and caster precedence missing-position guard repeated five-hit packets finite head windows obstacle reverse Seek owner cleanup");
        }
        {
            const string id = "masterplayer.eureka.10";
            object[] mines = Enumerable.Range(10, 8).Select(i => Actor((ulong)i, 0x3D55, center + Vector3.UnitX * (i - 14),
                cast: Cast(32700, 30 - i, center + Vector3.UnitX * (i - 14), startedAt: .1))).ToArray();
            load(id, [Header(1099),
                Frame(1099, 0, [Actor(1, 0x3D54, center, combat: true), Actor(100, 0, center)], full: true, party: party),
                Frame(1099, .1, mines, party: party),
                Frame(1099, 2, [Actor(11, 0x3D55, center)], party: party),
                Frame(1099, 3, [], removed: [1], party: party)]);
            controller.Seek(1);
            Check(dangers() == 6 && Group("cast/").Length == 6 && Group("cast/16").Length == 0,
                "Eureka10 actual DLL did not limit display/navigation to insertion-order first six.");
            controller.Seek(2.1);
            Check(dangers() == 6 && Group("cast/16").Length == 1 && Group("cast/11").Length == 0, "Eureka10 hidden tail failed promotion.");
            controller.Seek(1);
            Check(Group("cast/16").Length == 0 && dangers() == 6, "Eureka10 reverse Seek retained promoted tail.");
            controller.Seek(3.1);
            Check(previews().Length == 0 && dangers() == 0, "Eureka10 active primary removal retained mines.");
            stop();
            Console.WriteLine("PASS native Eureka10 eight stored mines insertion six shared display and actual danger hidden tail promotion reverse Seek active owner cleanup");
        }
        {
            const string id = "masterplayer.eureka.20";
            var sourceActorType = runtimeAssembly.GetType("BossMod.Actor")!;
            var sourceActor = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(sourceActorType);
            var sourceFlame = runtimeAssembly.GetType("BossMod.Endwalker.DeepDungeon.EurekaOrthos.DD20CloningNode.FlameBreath")!;
            var project = sourceFlame.GetMethod("CalculatePosition", BindingFlags.Static | BindingFlags.NonPublic)!;
            var records = new List<string>
            {
                Header(1100),
                Frame(1100, 0, [Actor(1, 0x3E77, center, combat: true), Actor(100, 0, center)], full: true, party: party)
            };
            var expectedOrigins = new List<Vector3>();
            var index = 0;
            foreach (var degrees in new[] { -45.003f, 44.998f, 134.999f, -135.005f })
                foreach (var sign in new[] { -1, 1 })
                    foreach (var shift in new[] { -MathF.Tau, 0, MathF.Tau })
                    {
                        const float unit = MathF.PI / 180f;
                        var boundary = degrees * unit + sign * unit + shift;
                        foreach (var rotation in new[] { MathF.BitDecrement(boundary), boundary, MathF.BitIncrement(boundary) })
                        {
                            var position = new Vector3(-331.25f, 0, -330.75f);
                            sourceActorType.GetField("PosRot")!.SetValue(sourceActor, new Vector4(position, rotation));
                            var projected = project.Invoke(null, [sourceActor])!;
                            var quantized = projected.GetType().GetMethod("Quantized")!.Invoke(projected, null)!;
                            expectedOrigins.Add(new((float)Value(quantized, "X"), 0, (float)Value(quantized, "Z")));
                            var at = ++index * .1;
                            records.Add(Frame(1100, at, [Actor(2, 0x3E78, position, rotation: rotation)], party: party));
                            records.Add(JsonSerializer.Serialize(new { t = at + .01, kind = "ActionEffect",
                                type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                                data = new { SourceId = 2ul, ActionId = 32552u, Targets = Array.Empty<ulong>() } }));
                        }
                    }
            load(id, records);
            controller.Seek(index * .1 + .02);
            var actual = Items(Encounter(id), "EurekaBreaths");
            Check(actual.Length == expectedOrigins.Count, "Angle oracle lost packets.");
            for (var i = 0; i < actual.Length; ++i)
                Check((Vector3)Value(actual[i], "Origin") == expectedOrigins[i], $"Source angle/float projection differs at boundary {i}.");
            stop();
            Console.WriteLine($"PASS native Eureka20 {expectedOrigins.Count} source CalculatePosition and Quantized oracle cases at inclusive one-degree bit-neighbours wrapped angles");
        }
        {
            const string id = "masterplayer.eureka.20";
            load(id,
            [
                Header(1100),
                Frame(1100, 0, [Actor(1, 0x3E77, center, combat: true), Actor(2, 0x233C, center),
                    Actor(100, 0, center + Vector3.UnitZ * 5)], full: true, party: party),
                JsonSerializer.Serialize(new { t = .1, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                    data = new { SourceId = 2ul, ActionId = 32552u, Targets = Array.Empty<ulong>() } }),
                JsonSerializer.Serialize(new { t = .2, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                    data = new { SourceId = 2ul, ActionId = 32552u, Targets = Array.Empty<ulong>() } }),
                CastEvent(.3, 2, 32864, 8, center + Vector3.UnitX),
                Frame(1100, .4, [Actor(2, 0x233C, center, cast: Cast(32864, 3.5f, center, startedAt: .3))], party: party),
                CastEvent(.42, 2, 32864, 8, center + Vector3.UnitX),
                Frame(1100, .5, [Actor(2, 0x233C, center)], party: party),
            ]);
            controller.Seek(.45);
            Check(Items(Encounter(id), "EurekaBreaths").Length == 2 && dangers() == 2,
                "First raw confirmation consumed Flame Breath FIFO.");
            var hintsMethod = Encounter(id).GetType().GetMethod("Hints", flags)!;
            var hints = ((IEnumerable<string>)hintsMethod.Invoke(Encounter(id), [center + Vector3.UnitZ * 5])!).ToArray();
            Check(hints.Contains("离开范围！"), "Flame Breath warning missing from actual DLL.");
            controller.Seek(.55);
            Check(Items(Encounter(id), "EurekaBreaths").Length == 1, "Real Finish after raw confirmation failed FIFO.");
            stop();
            Console.WriteLine("PASS native Eureka20 packet-first raw correction preserves FIFO and actual source danger text real cancellation consumes once");
        }
        {
            const string id = "masterplayer.eureka.20";
            string Director(double at, uint command) => JsonSerializer.Serialize(new
            {
                t = at, kind = "DirectorUpdate", type = "PromeRotation.LogSystem.LogSystemDirectorUpdateEvent",
                data = new { Command = command, Timestamp = Start.AddSeconds(at) }
            });
            string Native(double at) => JsonSerializer.Serialize(new
            {
                t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = 2ul, ActionId = 32552u, Targets = Array.Empty<ulong>() }
            });
            load(id,
            [
                Header(1100),
                Frame(1100, 0, [Actor(1, 0x3E77, center, combat: true), Actor(2, 0x233C, center),
                    Actor(100, 0, center)], full: true, party: party).Replace("\"LocalPlayerId\":100", "\"LocalPlayerId\":999"),
                Native(.1),
                Frame(1100, .2, [], party: party),
                Director(.3, 0x40000005),
                Director(.4, 0x40000011),
                Frame(1100, .5, [Actor(1, 0x3E77, center, combat: true)], party: party),
                Frame(1100, .6, [], removed: [1], party: party),
                Frame(1100, .7, [Actor(1, 0x3E77, center, combat: true)], party: party),
                Native(.8),
            ]);
            controller.Seek(.15);
            Check(Items(Encounter(id), "EurekaBreaths").Length == 1 && dangers() == 0,
                "Cold missing perspective dropped source mechanism or published personal output.");
            controller.Seek(.25);
            Check(Items(Encounter(id), "EurekaBreaths").Length == 1 && dangers() == 1, "Viewer restoration lost source history.");
            controller.Seek(.55);
            Check(Items(Encounter(id), "EurekaBreaths").Length == 0 && previews().Length == 0 && dangers() == 0,
                "Director resume recreated old Actor module.");
            controller.Seek(.85);
            Check(Items(Encounter(id), "EurekaBreaths").Length == 1 && dangers() == 1, "New same-ID actor failed after unload.");
            stop();
            Console.WriteLine("PASS native Eureka 20 source Party0 cold activation missing perspective restore Director unload no old actor revival genuine new generation owner cleanup");
        }
        {
            const string id = "masterplayer.eureka.20";
            string Native(double at, uint sequence) => JsonSerializer.Serialize(new
            {
                t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = 2ul, ActionId = 32552u, GlobalSequence = sequence,
                    Targets = Array.Empty<ulong>(), Timestamp = Start.AddSeconds(at - 10) }
            });
            var caster = Actor(2, 0x3E78, new(-331, 0, -331), rotation: MathF.PI / 4);
            load(id,
            [
                Header(1100),
                Frame(1100, 0, [Actor(1, 0x3E77, center, combat: true), caster, Actor(9, 0x233C, center),
                    Actor(100, 0, center + Vector3.UnitZ * 5)], full: true, party: party),
                Native(1, 7), EffectEvent(1.5, 2, 32552, 7, target: 100), Native(1.99, 7), Native(2, 0),
                Frame(1100, 2.2, [Actor(9, 0x233C, center, cast: Cast(32864, 3.5f, center, startedAt: 2.2))], party: party),
                Frame(1100, 2.4, [Actor(9, 0x233C, center)], party: party),
                Frame(1100, 3, [Actor(9, 0x233C, center, cast: Cast(32864, 3.5f, center, startedAt: 3))], party: party),
                Frame(1100, 3.2, [], removed: [9], party: party),
                Frame(1100, 4, [], removed: [2], party: party),
                Frame(1100, 5, [Actor(1, 0x3E77, center, dead: true)], party: party)
            ]);
            controller.Seek(2.1);
            var encounter = Encounter(id);
            Check(Items(encounter, "EurekaBreaths").Length == 3 && Group("eureka/breath/").Length == 2 && dangers() == 2,
                "Eureka20 native repeated packet, legacy exclusion or strict first+1 prefix differs.");
            var breaths = Items(encounter, "EurekaBreaths");
            Check((DateTime)Value(breaths[0], "Activation") == Start.AddSeconds(10.6)
                && (Vector3)Value(breaths[0], "CasterPosition") == new Vector3(-331, 0, -331),
                "Eureka20 lost original event input or used packet timestamp.");
            var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
            Check(Items(snapshot, "GoalAreas").Length == 0 && Items(snapshot, "HardObstacles").Length == 0,
                "Eureka20 added non-source navigation.");
            var assembly = runtimeObject.GetType().Assembly;
            const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
            dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
            dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                [center + Vector3.UnitZ * 5, 0f, 6f, false, false, 0f, true, (int)Value(snapshot, "Version")])!;
            dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
            dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
            dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
            Check((bool)result.IsSuccess && !(bool)result.GetType().GetProperty("IsLeastDangerFallback", flags)!.GetValue((object)result)!,
                "Eureka20 production solver failed breath escape.");
            controller.Seek(2.5);
            Check(Items(Encounter(id), "EurekaBreaths").Length == 2 && Group("eureka/breath/3").Length == 1, "True Finish failed FIFO/promotion.");
            controller.Seek(3.3);
            Check(Items(Encounter(id), "EurekaBreaths").Length == 1, "Destroy-casting helper failed global Finish.");
            controller.Seek(4.1);
            Check(Items(Encounter(id), "EurekaBreaths").Length == 1 && dangers() == 1, "Source destroy cleared frozen no-cast prediction.");
            controller.Seek(2.1);
            Check(Items(Encounter(id), "EurekaBreaths").Length == 3 && dangers() == 2, "Eureka20 reverse Seek lost event FIFO.");
            controller.Seek(5.1);
            Check(dangers() == 0 && Group("eureka/breath/").Length == 0, "Eureka20 phase completion retained predictions.");
            stop();
            Check(previews().Length == 0 && dangers() == 0, "Eureka owner stop retained output.");
            Console.WriteLine("PASS native Eureka20 complete event input consumption9.6 clock repeated and zero sequence legacy exclusion strict batch FIFO helper cancellation destroy frozen source actual solver reverse Seek owner cleanup");
        }
    }

    private static void RunEurekaTiamatNavigation(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.eureka.30";
        var center = new Vector3(-300, 0, -300);
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object[] Items(object obj, string name) => ((IEnumerable)Value(obj, name)).Cast<object>().ToArray();
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        object[] Goals() => Items(Snapshot(), "GoalAreas");
        string Native(double at, uint action) => JsonSerializer.Serialize(new
        {
            t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 2ul, ActionId = action, TargetPosition = center, Targets = Array.Empty<ulong>() },
        }, new JsonSerializerOptions { IncludeFields = true });
        load(id,
        [
            Header(1101),
            Frame(1101, 0, [Actor(1, 0x3D9A, center, combat: true), Actor(2, 0x233C, center),
                Actor(100, 0, center + Vector3.UnitZ * 6)], full: true, party: party),
            JsonSerializer.Serialize(new { t = .1, kind = "TargetIcon",
                type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                data = new { SourceId = 100ul, TargetId = 101ul, MarkerId = 197u } }),
            CastEvent(.2, 2, 31847, 5, center),
            Frame(1101, .3, [Actor(2, 0x233C, center, cast: Cast(31847, 5, center, startedAt: .2))], party: party),
            Frame(1101, .4, [Actor(2, 0x233C, center)], party: party),
            Native(1, 31847),
            Frame(1101, 2, [Actor(100, 0, center + Vector3.UnitX * 6, rotation: 1.7f)], party: party),
            Native(3, 31848), Native(5, 31848), Native(7, 31848), Native(9, 31848),
            Frame(1101, 21, [], party: party),
        ]);
        controller.Seek(.35);
        Check(Goals().Length == 0 && !(bool)Value((object)runtime.Setting, "RuntimePreferSafeRefuge"),
            "Eureka30 bait began early or replay enabled a movement owner.");
        controller.Seek(3.4);
        var goal = Goals().Single();
        var destination = (Vector3)Value(Value(goal, "Geometry"), "Position");
        var window = Items(goal, "GoalWindows").Single();
        // AddSeconds(4.8) can truncate one tick below AddSeconds(4).AddSeconds(.8).
        Check(Math.Abs(((DateTime)Value(window, "GoalUntil") - Start.AddSeconds(9).AddSeconds(.8)).Ticks) <= 1
            && !(bool)Value(window, "OverrideDangerMargin"),
            $"Eureka30 DLL bait expiry or margin differs: until={Value(window, "GoalUntil"):O}, override={Value(window, "OverrideDangerMargin")}.");
        controller.Seek(5.4);
        Check((Vector3)Value(Value(Goals().Single(), "Geometry"), "Position") == destination,
            "Eureka30 DLL target movement changed the fixed endpoint.");
        controller.Seek(9.1);
        Check(dangers() == 0 && (Vector3)Value(Value(Goals().Single(), "Geometry"), "Position") == center
            && (DateTime)Value(Items(Goals()[0], "GoalWindows").Single(), "GoalUntil") == Start.AddSeconds(19),
            "Eureka30 DLL fifth hit failed timed central recovery.");
        controller.Seek(19.1);
        Check(Goals().Length == 0, "Eureka30 DLL recovery expired without withdrawal.");
        controller.Seek(3.4);
        Check((Vector3)Value(Value(Goals().Single(), "Geometry"), "Position") == destination,
            "Eureka30 reverse Seek retained future recovery instead of original bait.");
        stop();
        Check(previews().Length == 0 && dangers() == 0 && Goals().Length == 0
            && !(bool)Value((object)runtime.Setting, "RuntimePreferSafeRefuge"), "Eureka30 stop leaked personal goals or refuge owner.");
        Console.WriteLine("PASS native Eureka30 fixed bait target movement finite goal timing fifth hit recovery reverse Seek owner cleanup");
    }
}
