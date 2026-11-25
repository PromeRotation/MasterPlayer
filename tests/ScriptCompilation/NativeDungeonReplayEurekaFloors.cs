using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ECommons.DalamudServices;

internal static partial class NativeDungeonReplayRegression
{
    private static void CheckDeepDungeonBridgeSuppressed(Assembly assembly, ushort territory)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = assembly.GetType("MasterPlayer.MechanicHost.MechanicHostController")!;
        var host = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
        type.GetField("_replayTerritory", flags)!.SetValue(host, territory);
        var field = type.GetField("_replaySession", flags)!;
        field.SetValue(host, System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(field.FieldType));
        Check(!(bool)type.GetProperty("ModulesEnabled", flags)!.GetValue(host)!
            && !(bool)type.GetProperty("HasActiveModule", flags)!.GetValue(host)!
            && (bool)type.GetMethod("IsReplaySuppressed", flags)!.Invoke(host, null)!,
            $"Native deep dungeon region {territory} retained bridge output or generic suppression.");
    }

    private static void RunEurekaFloors(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.eureka.floors";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Read(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } p) return p.GetValue(obj)!;
                if (type.GetField(name, flags) is { } f) return f.GetValue(obj)!;
            }
            throw new MissingMemberException(obj.GetType().FullName, name);
        }
        var assembly = ((IEnumerable)manager.Scripts).Cast<object>().First().GetType().Assembly;
        var data = assembly.GetType("MasterPlayer.Mechanics.EurekaOrthos.EurekaFloorData")!;
        var traps = (IDictionary)data.GetField("Traps", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        using var game = new Lumina.GameData(Environment.GetEnvironmentVariable("FFXIV_SQPACK")!,
            new Lumina.LuminaOptions { DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified });
        var dataProperty = typeof(Svc).GetProperty(nameof(Svc.Data), BindingFlags.Static | BindingFlags.Public)!;
        var previousData = Svc.Data;
        dataProperty.SetValue(null, new PalaceGameData(game));
        object Script() => Read(manager.Running[id], "Script");
        object Model() => Read(Script(), "encounter");
        object[] Shown(string suffix = "") => previews().Where(p => ((string)Read(p, "Id"))
            .StartsWith("MasterPlayer_Script_masterplayereurekafloors_", StringComparison.Ordinal)
            && ((string)Read(p, "Id")).Contains(suffix, StringComparison.Ordinal)).ToArray();
        int CountModel(string field) => ((IEnumerable)Read(Model(), field)).Cast<object>().Count();
        object Spell(uint action, double start, Vector3 position, double duration = 3)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, duration, position, target: 100, startedAt: start))!;
            node["IsSpell"] = true;
            return node;
        }
        object Status(object actor, uint status, int slot = 4, double expiry = 30)
        {
            var node = JsonSerializer.SerializeToNode(actor)!;
            node["Statuses"] = JsonSerializer.SerializeToNode(new[]
            {
                new { Id = status, Param = 0, SourceId = 100ul, ExpiresAt = Start.AddSeconds(expiry), SlotIndex = slot },
            });
            return node;
        }
        string Map(ushort territory, double t, IEnumerable<object> actors, int floor, bool full = false, byte tileset = 2,
            IEnumerable<object>? party = null, bool withMap = true, bool withWalls = false)
        {
            var node = JsonNode.Parse(Frame(territory, t, actors, full, party: party?.ToArray() ?? [new { Slot = 0, EntityId = 100ul }]))!;
            node["data"]!["DeepDungeon"] = withMap ? JsonSerializer.SerializeToNode(new
            {
                DungeonId = 3, Floor = floor, Tileset = tileset,
                Rooms = Enumerable.Repeat((ushort)(withWalls ? 128 : 0), 25).ToArray(),
            }) : null;
            return node.ToJsonString();
        }
        string Native(double t, uint action, ulong source = 10, uint sequence = 0)
            => JsonSerializer.Serialize(new { t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = source, ActionId = action, ActionType = 1, GlobalSequence = sequence, Timestamp = Start.AddSeconds(t) } });
        string Log(double t, uint message)
            => JsonSerializer.Serialize(new { t, kind = "SystemLog", type = "MasterPlayer.Replay.ReplaySystemLogEvent",
                data = new { MessageId = message, Timestamp = Start.AddSeconds(t) } });
        try
        {
            uint[] oids = [0x3D54, 0x3E77, 0x3D9A, 0x3D1D, 0x3D9C, 0x3DA1, 0x3D1B, 0x3D18, 0x3D23, 0x3CFC];
            for (var set = 1; set <= 10; ++set)
            {
                var territory = (ushort)(1098 + set);
                var bossId = $"masterplayer.eureka.{(set == 10 ? 99 : set * 10)}";
                manager.SetScriptEnabled(bossId, true);
                var trap = ((Vector2[])traps[territory]!)[0];
                var center = new Vector3(trap.X, 0, trap.Y);
                var source = center + Vector3.UnitZ * 4;
                object Mob(object? cast = null) => Actor(10, 0x3DF1, source, combat: true, cast: cast, subKind: 5, isEnemy: true);
                load(id,
                [
                    Header(territory), Map(territory, 0, [Actor(100, 0, center), Mob()], set * 10 - 9, true, 0, withWalls: true),
                    Map(territory, .1, [Mob(Spell(32437, .1, source))], set * 10 - 9, tileset: 0, withWalls: true),
                    Map(territory, 8, [], set * 10 - 9, tileset: 0, withWalls: true),
                    Map(territory, 8.1, [Mob()], set * 10 - 9, tileset: 0, withWalls: true),
                    Map(territory, 9, [Actor(1, oids[set - 1], center, combat: true)], set == 10 ? 99 : set * 10, withMap: false),
                    Map(territory, 9.1, [Actor(1, oids[set - 1], center, dead: true)], set == 10 ? 99 : set * 10, withMap: false),
                    Map(territory, 10, [], set * 10 - 9),
                    Map(territory, 11, [], set == 10 ? 99 : set * 10),
                ]);
                controller.Seek(3d);
                CheckDeepDungeonBridgeSuppressed(assembly, territory);
                var snap = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
                // This action has both AutoClear's explicit gaze and the
                // action sheet's VFX25 generic gaze, with separate lifetimes.
                Check(Shown("/eye").Length == 2 && ((IEnumerable)Read(snap, "HardObstacles")).Cast<object>().Any() && dangers() > 0,
                    $"EO{set * 10} lacks floor mechanism consumers: eyes={Shown("/eye").Length}, shown={Shown().Length}, " +
                    $"walls={((IEnumerable)Read(snap, "HardObstacles")).Cast<object>().Count()}, dangers={dangers()}, " +
                    $"enabled={Read(Model(), "ZoneEnabled")}, active={Read(Model(), "BossActive")}, errors={controller.RouterErrorCount}.");
                Check(Read(Script(), "PlannedFacing") is float, "EO gaze missing production PR facing planner.");
                Check(!(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest"), "EO replay published PR control.");
                controller.Seek(8d);
                Check(Shown("/eye").Length == 2, "EO gaze expired by predicted clock.");
                controller.Seek(8.1d);
                Check(Shown("/eye").Length == 0, "EO raw Finish failed to remove gaze.");
                controller.Seek(9d);
                CheckDeepDungeonBridgeSuppressed(assembly, territory);
                Check(Shown().Length == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary != null,
                    "EO native Boss handoff failed without metadata.");
                controller.Seek(11d);
                Check(Shown().Length == 0, "EO Boss floor received trash output.");
                controller.Seek(1d);
                Check(Shown("/eye").Length == 2, "EO reverse Seek lost raw gaze.");
                manager.SetScriptEnabled(id, false);
                Check(Shown().Length == 0 && dangers() == 0, "EO owner disable leaked geometry.");
                manager.SetScriptEnabled(id, true);
                stop();
                Console.WriteLine($"PASS production EO Zone{set * 10} walls traps raw gaze PR facing Boss handoff 99 isolation reverse Seek owner");
            }

            var zero = Vector3.Zero;
            object MobAt(uint? action = null, double start = .1, Vector3? p = null, bool dead = false)
                => Actor(10, 0x3E13, p ?? zero, combat: true, dead: dead,
                    cast: action == null ? null : Spell(action.Value, start, p ?? zero), subKind: 5, isEnemy: true);
            load(id,
            [
                Header(1099), Map(1099, 0, [Actor(100, 0, new(0, 0, 5)), MobAt()], 1, true),
                CastEvent(.1, 10, 33180, 3, zero), Log(.15, 7248),
                Map(1099, .2, [MobAt()], 2),
                Map(1099, 1, [MobAt(33180, 1)], 2), Map(1099, 2, [MobAt()], 2),
            ]);
            controller.Seek(.3d);
            Check(CountModel("spikes") == 0, "Packet staging rejection manufactured actual Finish Spikes.");
            controller.Seek(2d);
            Check(CountModel("spikes") == 1, "Observed Finish failed after staging rejection.");
            stop();
            Console.WriteLine("PASS production EO packet-only staging transference rejection versus observed actual Finish Spikes");

            load(id,
            [
                Header(1099), Map(1099, 0, [Actor(100, 0, new(0, 0, 5)), MobAt()], 1, true),
                Map(1099, .1, [MobAt(33180)], 1),
                Map(1099, 1, [Status(MobAt(32952, 1), 198)], 1),
                Map(1099, 2, [Status(MobAt(), 198)], 1),
                Map(1099, 3, [MobAt()], 1),
                Native(4, 33178), Native(4.1, 33195),
                Native(4.2, 32412),
                Map(1099, 5, [MobAt(p: new(10, 0, 0))], 1),
                Log(6, 7248), Map(1099, 6.1, [], 2),
                Map(1099, 7, [MobAt(dead: true)], 2),
                Map(1099, 12, [], 2),
            ]);
            controller.Seek(.5d);
            Check(CountModel("spikes") == 0, "EO Spikes started before Finish.");
            controller.Seek(2d);
            Check(CountModel("spikes") == 2, "EO actual Finish did not append separate spikes.");
            controller.Seek(3d);
            Check(CountModel("spikes") == 1, "EO raw status Lose did not consume first source.");
            controller.Seek(4.15d);
            Check(Shown("zone/void").Length == 2, "EO complete zero-sequence effects were deduplicated/lost.");
            controller.Seek(4.3d);
            Check(Shown("zone/void").Length == 1, "EO first-source FIFO matched wrong action.");
            controller.Seek(5d);
            Check(Shown("zone/void").Length == 1 && ((Vector3)Read(Read(Shown("zone/void")[0], "Geometry"), "Position")).X > 9,
                "EO persistent effect did not follow source.");
            controller.Seek(6.2d);
            Check(Shown("zone/void").Length == 1 && CountModel("spikes") == 0, "EO transference lost persistent effect or retained spikes.");
            controller.Seek(7d);
            Check(Shown("zone/void").Length == 0, "EO death did not expire persistent effect.");
            stop();
            Console.WriteLine("PASS production EO actual Finish Spikes status slot FIFO complete native effect dynamic voidzones asymmetric ClearState");

            load(id,
            [
                Header(1099), Map(1099, 0, [Actor(100, 0, new(0, 0, 5)), MobAt()], 1, true),
                Map(1099, .1, [Status(Actor(100, 0, new(0, 0, 5)), 160), MobAt(32413)], 1),
                Map(1099, .5, [Actor(100, 0, new(0, 0, 5))], 1),
                Log(1, 7248), Map(1099, 1.1, [], 2), Map(1099, 2, [MobAt()], 2),
            ]);
            controller.Seek(.6d);
            Check(Shown("zone/knockback").Length == 0, "EO early Lose cleared immunity.");
            controller.Seek(1.2d);
            Check(Shown("zone/knockback").Length == 1, "EO ClearState failed to clear cached immunity or erased KB model.");
            controller.Seek(2d);
            Check(Shown("zone/knockback").Length == 0, "EO real raw Finish retained KB circle.");
            stop();
            Console.WriteLine("PASS production EO true status slot immunity Gain Lose transference clear and real raw finish");

            object[] roleSlots = [new { Slot = 5, EntityId = 101ul }, new { Slot = 4, EntityId = 100ul }];
            load(id,
            [
                Header(1099), Map(1099, 0, [Actor(100, 0, new(0, 0, 5)), Actor(101, 0, new(0, 0, 4)), MobAt()],
                    1, true, party: roleSlots),
                Map(1099, .1, [Status(Actor(100, 0, new(0, 0, 5)), 160), MobAt(32413)], 1, party: roleSlots),
                Map(1099, 1, [], 1, party: roleSlots),
            ]);
            controller.Seek(.5d);
            var normalized = ((IEnumerable)Read(Model(), "party")).Cast<object>().ToArray();
            Check(normalized.Length == 2 && (int)Read(normalized[0], "Slot") == 0
                && (ulong)Read(normalized[0], "EntityId") == 100 && Shown("zone/knockback").Length == 0,
                "PR D1/D2 slots did not reach continuous source immunity slots.");
            stop();
            Console.WriteLine("PASS production EO recorded PR role slots normalized source local first immunity consumer");

            load(id,
            [
                Header(1099), Map(1099, 0, [Actor(100, 0, new(0, 0, 5)), MobAt()], 1, true),
                EffectEvent(.1, 10, 33178, 41, target: 100), EffectEvent(.1, 10, 33178, 41, target: 101),
                Native(.2, 33195, sequence: 42), EffectEvent(.21, 10, 33195, 42, target: 100),
                EffectEvent(.3, 10, 32412, 43, target: 100), Native(.31, 32412, sequence: 43),
                EffectEvent(.4, 10, 32412, 0, target: 100), EffectEvent(.4, 10, 32412, 0, target: 101),
                Native(.5, 33178), Native(.5, 33178), Map(1099, 1, [], 1),
            ]);
            controller.Seek(.15d);
            Check(Shown("zone/void").Length == 1, "Legacy expanded multi-target cast duplicated persistent list.");
            controller.Seek(.25d);
            Check(Shown("zone/void").Length == 2, "Native-first dual capture duplicated callback.");
            controller.Seek(.35d);
            Check(Shown("zone/void").Length == 1, "Legacy-first dual capture consumed two source FIFO entries.");
            controller.Seek(.45d);
            Check(Shown("zone/void").Length == 0, "Legacy zero sequence expanded targets did not consume one entry.");
            controller.Seek(.6d);
            Check(Shown("zone/void").Length == 2, "Complete identical zero sequence packets were collapsed.");
            controller.Seek(.15d);
            Check(Shown("zone/void").Length == 1, "Reverse Seek retained old packet receipts.");
            stop();
            Console.WriteLine("PASS production EO legacy expanded packet FIFO two-way native receipts independent complete zero packets reverse Seek");

            var caster = new Vector3(300, 0, -300);
            var playerPosition = new Vector3(280, 0, -300);
            load(id,
            [
                Header(1101), Map(1101, 0, [Actor(100, 0, playerPosition), MobAt(p: caster)], 21, true),
                Map(1101, .1, [MobAt(32470, .1, caster)], 21),
                Map(1101, .5, [MobAt(32470, .1, caster + Vector3.UnitX * 6)], 21),
                Map(1101, 2, [MobAt(p: caster)], 21), Map(1101, 3, [], 21),
            ]);
            ((dynamic)Script()).AutoLOS = true;
            ((dynamic)Script()).TrapHints = false;
            controller.Seek(.3d);
            var visible = Shown("zone/los");
            Check(visible.Length > 0 && dangers() >= visible.Length, "EO AutoLOS lost production areas.");
            bool Hit(Vector3 p) => Shown("zone/los").Any(v => (bool)((dynamic)Read(v, "Geometry")).Contains(p, 0f));
            Check(Hit(playerPosition) && !Hit(new(281, 0, -305)), "Real source bitmap shadow filled/ignored.");
            const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
            var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
            dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
            dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                [playerPosition, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
            dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
            dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
            dynamic solution = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
            Check((bool)solution.IsSuccess && !(bool)Read((object)solution, "IsLeastDangerFallback")
                && solution.TargetPosition is Vector3 point && !Hit(point), "EO AutoLOS real solver failed to enter shadow.");
            controller.Seek(.6d);
            Check(Hit(playerPosition) && !Hit(new(281, 0, -305)), "LOS recomputed after source moved.");
            controller.Seek(2d);
            Check(Shown("zone/los").Length == 0, "LOS outlived real raw Finish.");
            controller.Seek(.3d);
            Check(Hit(playerPosition), "LOS reverse Seek failed to rebuild start snapshot.");
            ((dynamic)Script()).AutoLOS = false;
            ((dynamic)Script()).TrapHints = true;
            stop();
            Console.WriteLine("PASS production EO AutoLOS exact embedded source bitmap locked shadow real solver no fallback reverse Seek owner");
        }
        finally { dataProperty.SetValue(null, previousData); }
    }
}
