using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ECommons.DalamudServices;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHeavenOnHighFloors(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.hoh.floors";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Read(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } property) return property.GetValue(obj)!;
                if (type.GetField(name, flags) is { } field) return field.GetValue(obj)!;
            }
            throw new MissingMemberException(obj.GetType().FullName, name);
        }
        var assembly = ((IEnumerable)manager.Scripts).Cast<object>().First().GetType().Assembly;
        var data = assembly.GetType("MasterPlayer.Mechanics.HeavenOnHigh.HeavenOnHighFloorData")!;
        var traps = (IDictionary)data.GetField("Traps", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        using var game = new Lumina.GameData(Environment.GetEnvironmentVariable("FFXIV_SQPACK")!,
            new Lumina.LuminaOptions { DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified });
        var dataProperty = typeof(Svc).GetProperty(nameof(Svc.Data), BindingFlags.Static | BindingFlags.Public)!;
        var previousData = Svc.Data;
        dataProperty.SetValue(null, new PalaceGameData(game));
        object Script() => Read(manager.Running[id], "Script");
        object[] Shown(string suffix = "") => previews().Where(p => ((string)Read(p, "Id"))
            .StartsWith("MasterPlayer_Script_masterplayerhohfloors_", StringComparison.Ordinal)
            && ((string)Read(p, "Id")).Contains(suffix, StringComparison.Ordinal)).ToArray();
        int Walls() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count();
        object Spell(uint action, double start, Vector3 position, double duration = 3)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, duration, position, target: 100, startedAt: start))!;
            node["IsSpell"] = true;
            return node;
        }
        try
        {
            foreach (var (territory, set, bossId, bossOid) in new[]
            {
                ((ushort)770, 1, "masterplayer.hoh.10", 0x23E1u), ((ushort)771, 2, "masterplayer.hoh.20", 0x23E7u),
                ((ushort)772, 3, "masterplayer.hoh.30", 0x2251u), ((ushort)782, 4, "masterplayer.hoh.40", 0x23E2u),
                ((ushort)773, 5, "masterplayer.hoh.50", 0x23E9u), ((ushort)783, 6, "masterplayer.hoh.60", 0x23E4u),
                ((ushort)774, 7, "masterplayer.hoh.70", 0x23EBu), ((ushort)784, 8, "masterplayer.hoh.80", 0x23ECu),
                ((ushort)775, 9, "masterplayer.hoh.90", 0x23EFu), ((ushort)785, 10, "", 0u),
            })
            {
                if (bossId.Length > 0) manager.SetScriptEnabled(bossId, true);
                var trap = ((Vector2[])traps[territory]!)[0];
                var center = new Vector3(trap.X, 0, trap.Y);
                var source = center + Vector3.UnitZ * 5;
                object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
                string Map(double t, IEnumerable<object> actors, int floor, bool full = false, byte tileset = 0, bool withMap = true)
                {
                    var node = JsonNode.Parse(Frame(territory, t, actors, full, party: party))!;
                    node["data"]!["DeepDungeon"] = withMap ? JsonSerializer.SerializeToNode(new
                    {
                        DungeonId = 2, Floor = floor, Tileset = tileset, Rooms = Enumerable.Repeat((ushort)0x80, 25).ToArray(),
                    }) : null;
                    return node.ToJsonString();
                }
                object Mob(object? cast = null) => Actor(10, 0x22AF, source, combat: true, cast: cast, subKind: 5, isEnemy: true);
                var gaze = Spell(6351, .1, source);
                load(id,
                [
                    Header(territory),
                    Map(0, [Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX), Mob()], set * 10 - 9, true),
                    Map(.1, [Mob(gaze)], set * 10 - 9),
                    CancelHoH(2, 10, 6351),
                    Map(3, [Mob(gaze)], set * 10 - 9),
                    Map(8, [Mob(gaze)], set * 10 - 9),
                    Map(8.1, [Mob()], set * 10 - 9),
                    Map(8.2, [Mob(gaze)], set * 10 - 9),
                    Map(9, bossOid == 0 ? [Mob()] : [Actor(1, bossOid, center, combat: true)], set * 10, withMap: false),
                    Map(9.1, bossOid == 0 ? [Mob()] : [Actor(1, bossOid, center, combat: true, dead: true)], set * 10, withMap: false),
                    Map(10, [Mob()], set * 10 - 9, tileset: 2),
                    Map(11, [Mob()], set * 10),
                ]);
                controller.Seek(3d);
                Check(Shown("/eye").Length == 1 && Walls() > 0 && dangers() > 0,
                    $"HoH{set} missing real consumers: shown={Shown().Length}, eyes={Shown("/eye").Length}, walls={Walls()}, danger={dangers()}.");
                Check(Read(Script(), "PlannedFacing") is float,
                    "Zone gaze never reached production PR facing planner.");
                var hints = Read(Script(), "Hints");
                Check(!(bool)Read(Read(hints, "_facing"), "_hasRequest")
                    && !(bool)Read((object)runtime.Setting, "RuntimeMovementHeld"),
                    "Replay sent native facing/movement hold.");
                controller.Seek(8d);
                Check(Shown("/eye").Length == 1, "Overdue raw gaze expired.");
                controller.Seek(8.1d);
                Check(Shown("/eye").Length == 0, "Real raw Finish retained gaze.");
                controller.Seek(9d);
                if (bossOid != 0)
                    Check(Shown().Length == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary != null,
                        "Native Boss did not retire floor output without metadata.");
                controller.Seek(10d);
                Check(Walls() == 0 && dangers() > 0, "Tileset2 removed traps or retained walls.");
                if (territory == 770)
                {
                    var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
                    const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
                    dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
                    dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                        [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
                    dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
                    dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
                    dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
                    Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
                        && result.TargetPosition is Vector3, "HoH real solver failed native trap escape.");
                    var destination = (Vector3)result.TargetPosition;
                    Check(((Vector2[])traps[territory]!).All(p => Vector2.Distance(p, new(destination.X, destination.Z)) > 2),
                        "Real solver destination entered source trap.");
                    Console.WriteLine("PASS production HoH native trap escape real solver no fallback source distances");
                }
                controller.Seek(11d);
                Check(Walls() == 0 && Shown().Length == 0, "Boss floor published ordinary Zone hazards.");
                controller.Seek(3d);
                Check(Shown("/eye").Length == 1 && Walls() > 0, "Reverse Seek failed native HoH reconstruction.");
                manager.SetScriptEnabled(id, false);
                Check(Shown().Length == 0 && Walls() == 0, "Disabled Zone leaked owner areas.");
                manager.SetScriptEnabled(id, true);
                stop();
                Console.WriteLine($"PASS production HoH Zone{set * 10} real wall/trap consumers dynamic raw gaze PR facing replay isolation Boss handoff reverse Seek owner");
            }

            // Live action-sheet guessing, including Source=0 HintDisabled behavior.
            var origin = new Vector3(0, 0, 0);
            object[] team = [new { Slot = 0, EntityId = 100ul }];
            string FrameWithMap(double t, IEnumerable<object> actors, bool full = false, int floor = 1)
            {
                var node = JsonNode.Parse(Frame(770, t, actors, full, party: team))!;
                node["data"]!["DeepDungeon"] = JsonSerializer.SerializeToNode(new { DungeonId = 2, Floor = floor, Tileset = 2, Rooms = new ushort[25] });
                return node.ToJsonString();
            }
            object Caster(uint action, double start) => Actor(10, 0x22FF, origin, combat: true,
                cast: Spell(action, start, origin), subKind: 5, isEnemy: true);
            load(id,
            [
                Header(770), FrameWithMap(0, [Actor(100, 0, new(5, 0, 0)), Actor(10, 0x22FF, origin)], true),
                FrameWithMap(.1, [Caster(12415, .1)]), FrameWithMap(1, [Caster(12432, 1)]),
                FrameWithMap(2, [Actor(10, 0x22FF, origin)]), FrameWithMap(3, []),
            ]);
            controller.Seek(.2d);
            Check(Shown().Length == 2 && dangers() == 2, "Action sheet lost ordinary 3..41 or Zone 9.5..41 donut.");
            controller.Seek(1d);
            Check(Shown().Length == 2 && dangers() == 2, "Non-null replacement lost old Zone donut or invented ordinary raidwide.");
            controller.Seek(2d);
            Check(Shown().Length == 0 && dangers() == 0, "Raw null did not clear both Zone lists.");
            stop();
            Console.WriteLine("PASS production HoH Action sheet ordinary donut Source0 HintDisabled independent non-null Zone replacement actual Finish");

            var coldGaze = Spell(6351, 0, origin);
            var hotGaze = Spell(6351, .6, origin);
            load(id,
            [
                Header(770), FrameWithMap(0, [Actor(100, 0, new(0, 0, 5)),
                    Actor(10, 0x22AF, origin, cast: coldGaze, subKind: 5, isEnemy: true)], true),
                JsonSerializer.Serialize(new { t = .5, kind = "SystemLog",
                    type = "MasterPlayer.Replay.ReplaySystemLogEvent",
                    data = new { MessageId = 7248u, Timestamp = Start.AddSeconds(.5) } }),
                FrameWithMap(.6, [Actor(11, 0x22AF, origin, cast: hotGaze, subKind: 5, isEnemy: true),
                    Actor(12, 0x1EA9A3, new(1, 0, 1))], floor: 2),
                FrameWithMap(1, [] , floor: 2),
            ]);
            controller.Seek(.1d);
            // Replay starts scripts before the first frame; test an actual cold
            // enable against the already populated world, separately from feed startup.
            void ColdEnable()
            {
                var pair = ((IEnumerable)Read(controllerObject, "_routers")).Cast<object>()
                    .Single(p => (string)Read(p, "Item1") == id);
                var router = Read(pair, "Item2");
                manager.SetScriptEnabled(id, false);
                manager.SetScriptEnabled(id, true);
                var start = managerObject.GetType().GetMethods(flags)
                    .Single(m => m.Name == "StartScript" && m.GetParameters().Length == 2);
                Check((bool)start.Invoke(managerObject, [id, router])!, "Cold replay script did not start.");
            }
            ColdEnable();
            int ZoneEyes() => ((IEnumerable)Read(Read(Script(), "encounter"), "gazes")).Cast<object>().Count();
            Check(ZoneEyes() == 0, "Cold Zone replayed historical gaze.");
            controller.Seek(.55d);
            Check(Shown().Length == 0, "Transfer retained Zone output.");
            controller.Seek(.7d);
            Check(ZoneEyes() == 1 && Shown("/eye").Length >= 1, "Hot new-floor cast was treated as cold activation.");
            Check(((IEnumerable)Read(Read(Script(), "floor"), "ignoredTraps")).Cast<object>().Count() == 1,
                "Hot new-floor Beacon Created failed to reach trap consumer.");
            controller.Seek(.1d);
            ColdEnable();
            Check(ZoneEyes() == 0, "Cold enable after reverse Seek manufactured Zone history.");
            stop();
            Console.WriteLine("PASS production HoH cold baseline transference hot Created and gaze reverse Seek no history replay");

            var nullableRaw = JsonSerializer.SerializeToNode(Cast(12415, 3, origin, target: 100, startedAt: .1))!;
            nullableRaw["IsSpell"] = null;
            load(id,
            [
                Header(770), FrameWithMap(0, [Actor(100, 0, new(5, 0, 0)),
                    Actor(10, 0x22FF, origin, subKind: 5, isEnemy: true)], true),
                JsonSerializer.Serialize(new { t = .1, kind = "CastStart",
                    type = "PromeRotation.LogSystem.LogSystemCastStartEvent",
                    data = new { SourceId = 10ul, TargetId = 100ul, ActionId = (ushort)12415,
                        CastTime = 3f, EffectPosition = origin, SourcePosition = origin,
                        SourceRotation = 0f, Timestamp = Start.AddSeconds(.1) } }),
                FrameWithMap(.2, [Actor(10, 0x22FF, origin, cast: nullableRaw, subKind: 5, isEnemy: true)]),
                FrameWithMap(1, []),
            ]);
            controller.Seek(.3d);
            Check(Shown().Length == 2 && dangers() == 2,
                "Packet-first nullable raw skipped ordinary action-sheet initialization.");
            stop();
            Console.WriteLine("PASS production HoH packet-first nullable raw ordinary action-sheet consumer and Zone receipt");

            string SystemLog(double t, uint message) => JsonSerializer.Serialize(new { t, kind = "SystemLog",
                type = "MasterPlayer.Replay.ReplaySystemLogEvent",
                data = new { MessageId = message, Timestamp = Start.AddSeconds(t) } });
            object Generation(object actor, ulong generation)
            {
                var node = JsonSerializer.SerializeToNode(actor)!;
                node["GameObjectId"] = generation;
                return node;
            }
            var oldPacket = JsonNode.Parse(CastEvent(.35, 10, 6351, 3, origin, target: 100))!;
            oldPacket["data"]!["Timestamp"] = JsonSerializer.SerializeToNode(Start.AddSeconds(.1));
            load(id,
            [
                Header(770), FrameWithMap(0, [Actor(100, 0, new(0, 0, 5)),
                    Generation(Actor(10, 0x22AF, origin, subKind: 5, isEnemy: true), 1)], true),
                CastEvent(.1, 10, 6351, 3, origin, target: 100), SystemLog(.15, 7248),
                FrameWithMap(.2, [Generation(Caster(6351, .1), 1)], floor: 2),
                FrameWithMap(.3, [Generation(Actor(10, 0x22AF, origin, subKind: 5, isEnemy: true), 2)], floor: 2),
                oldPacket.ToJsonString(), FrameWithMap(.4, [Generation(Caster(6351, .32), 2)], floor: 2),
                FrameWithMap(1, [], floor: 2),
            ]);
            controller.Seek(.1d);
            var continuous = Script();
            controller.Play();
            controller.Update(.11f);
            Check(ReferenceEquals(continuous, Script()) && Shown("/eye").Length == 1,
                "ClearState lost first raw or restarted the script to recover.");
            controller.Update(.16f);
            Check(ReferenceEquals(continuous, Script()) && Shown("/eye").Length == 0,
                "Late packet resurrected gaze on reused actor ID.");
            controller.Update(.05f);
            Check(ReferenceEquals(continuous, Script()) && Shown("/eye").Length == 1,
                "Reused actor raw did not produce one real start.");
            controller.Pause();
            stop();
            Console.WriteLine("PASS production HoH same-instance packet ClearState raw confirmation late old-generation packet real replacement raw");

            var firstTrap = ((Vector2[])traps[(ushort)770]!)[0];
            var trapPoint = new Vector3(firstTrap.X, 0, firstTrap.Y);
            string NullableMap(double t, IEnumerable<object> actors, bool full = false, bool valid = false)
            {
                var node = JsonNode.Parse(FrameWithMap(t, actors, full))!;
                if (!valid) node["data"]!["DeepDungeon"] = null;
                return node.ToJsonString();
            }
            load(id,
            [
                Header(770), NullableMap(0, [Actor(100, 0, trapPoint)], true),
                NullableMap(.1, [Generation(Actor(12, 0x1EA9A3, trapPoint), 1)]),
                NullableMap(.2, [Generation(Actor(12, 0x1EA9A3, trapPoint + Vector3.UnitX * 10), 1)]),
                NullableMap(.3, [], valid: true), SystemLog(.4, 7248),
                FrameWithMap(.5, [], floor: 2), FrameWithMap(1, [], floor: 2),
            ]);
            controller.Seek(.3d);
            Check(((IEnumerable)Read(Read(Script(), "floor"), "ignoredTraps")).Cast<object>().Count() == 1,
                "Consecutive null maps erased hot Beacon history.");
            Check(!Shown("floor/trap").Any(p => (Vector3)Read(Read(p, "Geometry"), "Position") == trapPoint),
                "Published static trap ignored the frozen birth position.");
            controller.Seek(.5d);
            Check(((IEnumerable)Read(Read(Script(), "floor"), "ignoredTraps")).Cast<object>().Count() == 0,
                "Actual transfer failed to clear indicator history.");
            stop();
            Console.WriteLine("PASS production HoH null map hot Beacon birth-position history actual transference clear");
        }
        finally { dataProperty.SetValue(null, previousData); }
    }
}
