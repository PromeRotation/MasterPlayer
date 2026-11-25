using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunAlphaCarnivale(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var plugin = managerObject.GetType().Assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        string guid = "";
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        int Goals() => ((IEnumerable)((dynamic)runtimeObject).Manager.BuildSnapshot(((dynamic)runtimeObject).CurrentTime).GoalAreas).Cast<object>().Count();
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Lines() => ((IEnumerable)Value(plugin, "AoeVisibility").GetType().GetMethod("GetLines")!
            .Invoke(Value(plugin, "AoeVisibility"), null)!).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        object[] Aoes(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(prefix)).ToArray();
        string Director(double t, uint command) => JsonSerializer.Serialize(new
        {
            t, kind = "DirectorUpdate", type = "PromeRotation.LogSystem.LogSystemDirectorUpdateEvent",
            data = new { Command = command, Timestamp = Start.AddSeconds(t) },
        });
        int Oracle(object bounds, Vector3 center, float minX, float maxX, float minZ, float maxZ)
        {
            dynamic arena = Value(Encounter(), "Arena");
            Check(Vector3.Distance((Vector3)arena.Position, center) < .0001f, "Source bounds center changed.");
            var direction = bounds.GetType().Assembly.GetType("BossMod.WDir")!;
            var count = 0;
            for (var x = minX; x < maxX; x += .73f)
            for (var z = minZ; z < maxZ; z += .71f)
            {
                var expected = (bool)bounds.GetType().GetMethod("Contains")!.Invoke(bounds,
                    [Activator.CreateInstance(direction, [x - center.X, z - center.Z])])!;
                Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, $"Source polygon mismatch at {x},{z}.");
                ++count;
            }
            return count;
        }
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", flags)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", flags)!.GetValue(facing)!
                && !((IEnumerable)hints.GetType().GetField("forbiddenTargets", flags)!.GetValue(hints)!).Cast<object>().Any()
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld"),
                "Alpha/Carnivale replay leaked execution controls.");
        }
        var runtimeAssembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MasterPlayer.MechanicRuntime");
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        object[] Tether(uint type, ulong target) => [new { Id = type, TargetId = target }];
        var center = new Vector3(410, 0, -110);
        var middle = new Vector3(403, 0, -105);
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(1244, t, actors, full: full, removed: removed, party: party);
        guid = "masterplayer.quest.warmth-of-the-family";
        load(guid, [Header(1244), At(0,
            [Actor(1, 0x4632, center, combat: true, isEnemy: false),
             Actor(10, 0x4635, middle, tethers: Tether(84, 101)),
             Actor(20, 0x4637, center, isEnemy: true),
             Actor(30, 0x4636, center, cast: Cast(40563, 30, center, rotation: .31f, startedAt: 0)),
             Actor(100, 0, center + Vector3.UnitZ * 2), Actor(101, 0, center + Vector3.UnitZ * 5)], true),
            At(1, [Actor(10, 0x4635, middle, tethers: Tether(17, 101),
                cast: Cast(40562, 8, middle, startedAt: 1))]),
            At(2, [Actor(10, 0x4635, middle, tethers: Tether(17, 101)),
                Actor(1, 0x4632, center, dead: true, combat: false, isEnemy: false)]),
            At(3, [], removed: [1]),
            At(4, [Actor(101, 0, center + new Vector3(.12345f, 0, 8.12345f))]),
            At(5, [Actor(30, 0x4636, center), Actor(10, 0x4635, middle)]),
            At(6, [Actor(20, 0x4637, center, dead: true)]), At(7, [])]);
        controller.Seek(.5);
        Check(dangers() == 1 && Goals() == 1 && Lines().Length == 1 && (bool)Value(Lines().Single(), "Safe")
            && Shown("cast/").Length == 1 && Shown("alpha/intercept").Length == 0,
            "Alpha production display/navigation/line colour differ.");
        var alphaType = runtimeAssembly.GetType("BossMod.Dawntrail.Quest.MSQ.TheWarmthOfTheFamily.AlphaAlligator.AlphaAlligator")!;
        dynamic alphaBounds = alphaType.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var alphaCenter = new Vector3((float)alphaBounds.Center.X, 0, (float)alphaBounds.Center.Z);
        var alphaOracle = Oracle(alphaBounds, alphaCenter, 386.13f, 439, -143.17f, -79);
        controller.Seek(1.5);
        Check(Math.Abs(((DateTime)Value(Encounter(), "AlphaActivation") - Start.AddSeconds(10.5)).TotalSeconds) < .00001
            && Lines().Length == 1 && Aoes("alpha/intercept").Length == 1,
            $"Alpha 40562 shared clock or raw84 to17 replacement failed: {Value(Encounter(), "AlphaActivation"):O}, lines={Lines().Length}, zones={Aoes("alpha/intercept").Length}.");
        controller.Seek(3.5);
        Check(Value(Encounter(), "Active") != null && dangers() == 1 && Goals() == 1, "Alpha primary death/destroy ended independent fight.");
        controller.Seek(4.5);
        Check((Vector3)Value(Lines().Single(), "End") == center + new Vector3(.12345f, 0, 8.12345f),
            "Alpha tether reference failed raw nonquantized movement.");
        controller.Seek(5.5);
        Check(dangers() == 0 && Goals() == 0 && Lines().Length == 0 && previews().All(a => !((string)Value(a, "Id")).Contains("cast/")),
            "Alpha real Finish/untether retained public output.");
        controller.Seek(6.5);
        Check(Value(Encounter(), "Active") == null, "Alpha cached final boss IsDead did not complete.");
        controller.Seek(.5);
        Isolated();
        manager.SetMechanismEnabled(guid, "Alpha Alligator", false);
        controller.Seek(.6);
        Check(dangers() == 0 && previews().Length == 0 && Lines().Length == 0, "Alpha disabled owner leaked lines/navigation.");
        manager.SetMechanismEnabled(guid, "Alpha Alligator", true);
        stop();
        Check(Lines().Length == 0, "Alpha stopped owner leaked actual line registry.");
        Console.WriteLine($"PASS native Alpha Alligator production60-vertex source oracle {alphaOracle} points default shared clocks typed raw84 17 tether old references dual views persistent navigation independent final boss reverse Seek and owner cleanup");

        guid = "masterplayer.carnivale.stage01";
        var carnival = new Vector3(100, 0, 100);
        string C(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed, party: party);
        load(guid, [Header(796), C(0,
            [Actor(1, 0x25BE, carnival), Actor(10, 0x25BD, carnival + Vector3.UnitX * 5),
             Actor(100, 0, carnival - Vector3.UnitZ * 5), Actor(101, 0, carnival - Vector3.UnitZ * 7)], true),
            C(1, [Actor(10, 0x25BD, carnival + Vector3.UnitX * 5, combat: true),
                Actor(1, 0x25BE, carnival, targetable: false, cast: Cast(14199, 20, carnival, startedAt: 1))]),
            C(2, [Actor(1, 0x25BE, carnival, dead: true)]),
            C(3, [], removed: [1]),
            C(4, [Actor(10, 0x25BD, carnival + Vector3.UnitX * 5, dead: true)]),
            C(5, [Actor(10, 0x25BD, carnival + Vector3.UnitX * 5)]), C(6, []),
            Director(7, 0x40000005), Director(8, 0x40000011), C(9,
                [Actor(1, 0x25BE, carnival), Actor(10, 0x25BD, carnival, combat: true)]), C(10, [])]);
        controller.Seek(.5);
        Check(Value(Encounter(), "Active") == null && Value(Encounter(), "Arena") == null && dangers() == 0
            && Shown("carnivale/enemy/").Length == 2 && Shown("carnivale/precombat/arena").Length == 1
            && Value(Encounter(), "CarnivalePrecombatHint") != null, "Stage01 precombat display/hint or unwanted navigation.");
        controller.Seek(1.5);
        Check(Value(Encounter(), "Active") != null && (ulong)Value(Encounter(), "PrimaryId") == 1
            && Value(Encounter(), "CarnivalePrecombatHint") == null && dangers() == 1 && Shown("cast/").Length == 1,
            "Stage01 slime group pull untargetable primary or combat handoff failed.");
        var layouts = runtimeAssembly.GetType("BossMod.Global.MaskedCarnivale.Layouts")!;
        var bounds = layouts.GetField("CircleBig", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        var carnivalOracle = Oracle(bounds, carnival, 73.13f, 127, 73.17f, 127);
        controller.Seek(3.5);
        Check(Value(Encounter(), "Active") != null && (ulong)Value(Encounter(), "PrimaryId") == 1 && dangers() == 0,
            "Stage01 Primary death/destruction ended living group or retained cast.");
        controller.Seek(4.5);
        Check(Value(Encounter(), "Active") == null && previews().Length == 0 && dangers() == 0, "Stage01 all dead did not clear.");
        controller.Seek(5.5);
        Check(Value(Encounter(), "Active") == null, "Stage01 completed primary missing group repulled.");
        controller.Seek(9.5);
        Check(Value(Encounter(), "Active") != null, "Stage01 wipe unlock/new generation failed.");
        controller.Seek(.5);
        Isolated();
        manager.SetMechanismEnabled(guid, "出道战", false);
        controller.Seek(.6);
        Check(previews().Length == 0 && dangers() == 0, "Stage01 disabled precombat retained displays.");
        manager.SetMechanismEnabled(guid, "出道战", true);
        stop();
        Console.WriteLine($"PASS native Carnivale Stage01 production shared796 exact primary precombat display-only polygon64 source oracle {carnivalOracle} points group pull primary death destruction group completion reverse Seek wipe and owner cleanup");

        foreach (var carnivalMode in new[] { false, true })
        {
            guid = carnivalMode ? "masterplayer.carnivale.stage01" : "masterplayer.quest.warmth-of-the-family";
            var territory = carnivalMode ? 796u : 1244u;
            var bossOid = carnivalMode ? 0x25BEu : 0x4632u;
            var action = carnivalMode ? 14199u : 40563u;
            var position = carnivalMode ? carnival : center;
            string R(double t, object cast, bool full = false) => Frame(territory, t,
                [Actor(1, bossOid, position, combat: true, cast: cast), Actor(10, carnivalMode ? 0x25BDu : 0x4637u, position),
                 Actor(100, 0, position - Vector3.UnitZ * 5)], full: full, party: [new { Slot = 0, EntityId = 100ul }]);
            string Cancel(double t) => JsonSerializer.Serialize(new
            {
                t, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                data = new { SourceId = 1ul, ActionId = action, Timestamp = Start.AddSeconds(t) },
            });
            var oldCast = Cast(action, 20, position, startedAt: 0);
            var oldPacket = JsonSerializer.Serialize(new
            {
                t = 2.1, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent", packetFacing = 0f,
                data = new { SourceId = 1ul, ActionId = action, CastTime = 20f, EffectPosition = Pose(position), Timestamp = Start },
            });
            load(guid, [Header(territory), R(0, oldCast, true), Cancel(1),
                R(2, Cast(60000, 20, position, startedAt: 0)), oldPacket,
                R(3, oldCast), Cancel(4), R(5, Cast(action, 20, position, rotation: .71f, startedAt: 0)),
                R(6, Cast(action, 20, position, rotation: .71f, startedAt: 0))]);
            controller.Seek(1.5);
            Check(dangers() == 0, "Cancelled unchanged production raw resurrected cast.");
            controller.Seek(2.5);
            Check(dangers() == 0, "Unclaimed production raw unlocked cancelled old packet.");
            controller.Seek(3.5);
            Check(dangers() == 1 && Shown("cast/").Length == 1, "True production raw from unknown to valid failed restart.");
            controller.Seek(5.5);
            var casts = ((IEnumerable)Value(Encounter(), "Casts")).Cast<object>().ToArray();
            var pending = Value(casts.Single(), "Value");
            Check(dangers() == 1 && MathF.Abs((float)Value(pending, "Rotation") - .71f) < .00001f,
                "True production raw rotation after cancel failed relock.");
            controller.Seek(2.5);
            Check(dangers() == 0, "Production reverse Seek lost cancelled raw identity.");
            stop();
        }
        Console.WriteLine("PASS native Alpha and Carnivale production cancelled cast unchanged raw unknown action late old packet committed real raw restart locked rotation reverse Seek and owner cleanup");

        guid = "masterplayer.carnivale.stage01";
        foreach (var stage in new[] { (Oid: 0x25C8u, Add: 0x25D2u, Name: "怪力铁巨人·第一幕"),
            (Oid: 0x25CCu, Add: 0x25CCu, Name: "金币龟兄弟") })
        {
            string StageFrame(double t, object[] actors, bool full = false, ulong[]? removed = null) =>
                Frame(796, t, actors, full: full, removed: removed, party: party);
            var arenaCenter = new Vector3(100, 0, 100);
            load(guid, [Header(796),
                StageFrame(0, [Actor(100, 0, arenaCenter), Actor(101, 0, arenaCenter + Vector3.UnitZ)], true),
                StageFrame(.1,
                [Actor(1, stage.Oid, arenaCenter), Actor(10, stage.Add, arenaCenter + Vector3.UnitX * 5),
                 Actor(100, 0, arenaCenter), Actor(101, 0, arenaCenter + Vector3.UnitZ)]),
                StageFrame(1, [Actor(10, stage.Add, arenaCenter + Vector3.UnitX * 5, combat: true)]),
                StageFrame(2, [Actor(1, stage.Oid, arenaCenter, dead: true)]),
                StageFrame(3, [], removed: [1]),
                StageFrame(4, [Actor(10, stage.Add, arenaCenter + Vector3.UnitX * 5, dead: true)]), StageFrame(5, []),
                StageFrame(6, [Actor(20, 0x25CC, arenaCenter)]),
                StageFrame(7, [Actor(20, 0x25CC, arenaCenter, combat: true)]), StageFrame(8, [])]);
            controller.Seek(.5);
            Check(Value(Encounter(), "Active") == null && dangers() == 0 && Goals() == 0
                && Shown("carnivale/precombat/arena").Length == 1
                && Shown("carnivale/enemy/").Length == 2
                && Value(Encounter(), "CarnivalePrecombatHint") != null, "New Carnivale preparation output missing.");
            controller.Seek(1.5);
            Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == stage.Oid && dangers() == 0 && Goals() == 0
                && Value(Encounter(), "CarnivalePrecombatHint") == null && Shown("carnivale/enemy/").Length == 2,
                "New Carnivale group pull added AOE/action or lost markers.");
            var stageBounds = layouts.GetField(stage.Oid == 0x25CC ? "CircleSmall" : "CircleBig",
                BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
            var samples = Oracle(stageBounds, arenaCenter, 74.173f, 126, 74.191f, 126);
            controller.Seek(3.5);
            Check(Value(Encounter(), "Active") != null && (ulong)Value(Encounter(), "PrimaryId") == 1
                && Shown("carnivale/enemy/").Length == 1, "New Carnivale rebound destroyed primary or ended group early.");
            controller.Seek(4.5);
            Check(Value(Encounter(), "Active") == null && previews().Length == 0 && Goals() == 0 && dangers() == 0,
                "New Carnivale group completion leaked output.");
            controller.Seek(7.5);
            Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x25CC
                && (ulong)Value(Encounter(), "PrimaryId") == 20,
                "Completed corpse blocked next stage in the same production encounter.");

            controller.Seek(.5);
            Check(Value(Encounter(), "CarnivalePrecombatHint") != null, "New Carnivale reverse Seek lost preparation.");
            Isolated();
            manager.SetMechanismEnabled(guid, stage.Name, false);
            controller.Seek(.6);
            Check(previews().Length == 0 && dangers() == 0 && Goals() == 0, "New Carnivale disabled preparation leaked owner output.");
            manager.SetMechanismEnabled(guid, stage.Name, true);
            stop();
            Console.WriteLine($"PASS native Carnivale {stage.Oid:X} production exact polygon source oracle {samples} points preparation group pull primary death destruction all-dead completion no invented actions reverse Seek disabled owner and cleanup");
        }


        var corpseCenter = new Vector3(100, 0, 100);
        load(guid, [Header(796), Frame(796, 0,
            [Actor(1, 0x25C8, corpseCenter), Actor(10, 0x25D2, corpseCenter, combat: true),
             Actor(100, 0, corpseCenter)], full: true, party: party),
            Frame(796, 1, [Actor(1, 0x25C8, corpseCenter, dead: true),
                Actor(10, 0x25D2, corpseCenter, dead: true), Actor(20, 0x25CC, corpseCenter, combat: true)], party: party),
            Frame(796, 2, [], party: party)]);
        controller.Seek(.5);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x25C8, "Corpse fixture failed initial stage.");
        controller.Seek(1.5);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x25CC
            && (ulong)Value(Encounter(), "PrimaryId") == 20
            && ((IEnumerable)Value(Encounter(), "Actors")).Cast<object>().Any(kv => (ulong)Value(kv, "Key") == 1
                && (bool)Value(Value(kv, "Value"), "Dead")), "Retained primary corpse blocked production stage handoff.");
        controller.Seek(.5);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x25C8, "Corpse stage handoff failed reverse Seek.");
        stop();
        Console.WriteLine("PASS native Carnivale production retained completed primary corpse permits next stage without world reload and reverse Seek");
        CarnivaleSlimeBombBody(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        CarnivaleInterruptBody(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        CarnivaleBombPlantBody(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        CarnivaleBeautyBody(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivaleLastSong(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivaleBallast(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivaleSunset(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivaleClaws(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivaleKegs(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivaleRebekkah(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivaleTyphon(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivaleChimera(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivaleBoom(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivaleBehemoth(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivalePit(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivaleAzulmagia(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivalePapa(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivaleSnorters(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivaleDraugar(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivaleRed(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivaleSiegfried(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivaleGogo(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivaleGoldor(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivalePuddingGolemMock(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunCarnivaleEyesVoltage(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);

    }
}
