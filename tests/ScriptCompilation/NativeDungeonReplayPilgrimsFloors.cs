using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ECommons.DalamudServices;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunPilgrimsFloors(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.pilgrims.floors";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Read(object obj, string name)
        {
            for (var t = obj.GetType(); t != null; t = t.BaseType)
            {
                if (t.GetProperty(name, flags) is { } p) return p.GetValue(obj)!;
                if (t.GetField(name, flags) is { } f) return f.GetValue(obj)!;
            }
            throw new MissingMemberException(obj.GetType().FullName, name);
        }
        using var game = new Lumina.GameData(Environment.GetEnvironmentVariable("FFXIV_SQPACK")!,
            new Lumina.LuminaOptions { DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified });
        var dataProperty = typeof(Svc).GetProperty(nameof(Svc.Data), BindingFlags.Static | BindingFlags.Public)!;
        var previousData = Svc.Data;
        dataProperty.SetValue(null, new PalaceGameData(game));
        object Script() => Read(manager.Running[id], "Script");
        object Model() => Read(Script(), "encounter");
        object[] Shown(string suffix = "") => previews().Where(p => ((string)Read(p, "Id"))
            .StartsWith("MasterPlayer_Script_masterplayerpilgrimsfloors_", StringComparison.Ordinal)
            && ((string)Read(p, "Id")).Contains(suffix, StringComparison.Ordinal)).ToArray();
        int CountModel(string name) => ((IEnumerable)Read(Model(), name)).Cast<object>().Count();
        var assembly = managerObject.GetType().Assembly;
        object Spell(uint action, double start = .1, float rotation = 0)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 3, Vector3.Zero, rotation, 100, start))!;
            node["IsSpell"] = true;
            return node;
        }
        object Mob(object? cast = null, Vector3? position = null, float rotation = 0, uint oid = 0x4922, ulong target = 0,
            ushort objectIndex = 10)
        {
            var node = JsonSerializer.SerializeToNode(Actor(10, oid, position ?? Vector3.Zero, combat: true,
                cast: cast, objectIndex: objectIndex, subKind: 5, isEnemy: true))!;
            node["Rotation"] = rotation;
            node["TargetId"] = target;
            return node;
        }
        object Status(object actor)
        {
            var node = JsonSerializer.SerializeToNode(actor)!;
            node["Statuses"] = JsonSerializer.SerializeToNode(new[]
            {
                new { Id = 4579u, Param = 0, SourceId = 10ul, ExpiresAt = Start.AddSeconds(30), SlotIndex = 5 },
            });
            return node;
        }
        string Map(ushort territory, double t, IEnumerable<object> actors, int floor, bool full = false)
        {
            var node = JsonNode.Parse(Frame(territory, t, actors, full, party: [new { Slot = 4, EntityId = 100ul }]))!;
            node["data"]!["DeepDungeon"] = JsonSerializer.SerializeToNode(new
            {
                DungeonId = 4, Floor = floor, Tileset = 0, Rooms = Enumerable.Repeat((ushort)128, 25).ToArray(),
            });
            return node.ToJsonString();
        }
        string Native(double t, uint action, uint sequence = 0, uint oid = 0x4922, int objectIndex = 10)
            => JsonSerializer.Serialize(new { t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = 10ul, SourceOid = oid, SourceObjectIndex = objectIndex, ActionId = action,
                    ActionType = 1, GlobalSequence = sequence, Timestamp = Start.AddSeconds(t) } });
        try
        {
            uint[] oids = [0x460D, 0x485C, 0x4609, 0x460B, 0x4872, 0x49DE, 0x484C, 0x485D, 0x48F5, 0x48EA];
            for (var set = 1; set <= 10; ++set)
            {
                var territory = (ushort)(1280 + set);
                var bossId = $"promeradar.pt{(set == 10 ? 99 : set * 10)}";
                manager.SetScriptEnabled(bossId, true);
                load(id,
                [
                    Header(territory), Map(territory, 0, [Actor(100, 0, new(0, 0, 5)), Mob()], set * 10 - 9, true),
                    Map(territory, .1, [Mob(Spell(42091))], set * 10 - 9),
                    Map(territory, 8, [], set * 10 - 9),
                    Map(territory, 8.1, [Mob()], set * 10 - 9),
                    Map(territory, 9, [Actor(1, oids[set - 1], Vector3.Zero, combat: true)], set == 10 ? 99 : set * 10),
                    Map(territory, 10, [], set == 10 ? 99 : set * 10),
                ]);
                controller.Seek(1d);
                Check(Shown("zone/sector").Length == 1 && dangers() > 0, "PT floor precise source sector missing.");
                CheckDeepDungeonBridgeSuppressed(assembly, territory);
                var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
                Check(!((IEnumerable)Read(snapshot, "HardObstacles")).Cast<object>().Any(), "PT borrowed Palace wall coordinates.");
                controller.Seek(8d);
                Check(Shown("zone/sector").Length == 1, "PT source sector expired by prediction.");
                controller.Seek(8.1d);
                Check(Shown("zone/sector").Length == 0, "PT raw Finish failed.");
                controller.Seek(9d);
                Check(Shown().Length == 0, "PT floor failed immediate Boss handoff.");
                CheckDeepDungeonBridgeSuppressed(assembly, territory);
                controller.Seek(1d);
                Check(Shown("zone/sector").Length == 1, "PT reverse Seek failed to rebuild source state.");
                manager.SetScriptEnabled(id, false);
                Check(Shown().Length == 0 && dangers() == 0, "PT owner disable retained geometry.");
                manager.SetScriptEnabled(id, true);
                stop();
                Console.WriteLine($"PASS production PT Zone{set * 10} precise source sector real Finish no borrowed walls Boss handoff reverse Seek owner");
            }
            load(id,
            [
                Header(1287), Map(1287, 0, [Actor(100, 0, new(0, 0, 5)), Mob()], 61, true),
                Map(1287, .1, [Mob(Spell(42214))], 61),
                Native(.5, 42214, 41), Native(.6, 44759, 42),
                Map(1287, 1, [Mob(position: new(10, 0, 0), rotation: 1)], 61),
                Native(1.1, 44760), Native(1.1, 44760), Native(1.1, 44760), Native(1.1, 44760),
                Map(1287, 2, [], 61),
            ]);
            controller.Seek(.2d);
            var eventValue = new PromeRotation.LogSystem.LogSystemCastStartEvent { SourceId = 10, ActionId = 42214 };
            var handles = Script().GetType().BaseType!.BaseType!.GetMethod("HandlesCast", flags)!;
            Check(!(bool)handles.Invoke(Script(), [eventValue])!, "PT floor suppressed existing exact first-hit/follow-up consumer.");
            Check(Shown().Any(v => ((string)Read(v, "Id")).Contains("cast/10/42214")), "PT replay omitted precise first-hit model.");
            controller.Seek(.7d);
            Check(Shown("zone/void").Length == 2, "PT complete source effects missing.");
            controller.Seek(1d);
            var cone = Shown("zone/void").First(v => (float)Read(Read(v, "Geometry"), "Radius") == 30);
            Check((Vector3)Read(Read(cone, "Geometry"), "Position") is { X: > 9 }, "PT source effect stopped following Actor.");
            controller.Seek(1.2d);
            Check(Shown("zone/void").Length == 0, "PT first-source counter or repeated zero complete effects lost.");
            stop();
            Console.WriteLine("PASS production PT precise first hit coexistence dynamic source side effect mixed FIFO three heel packets zero sequence");

            load(id,
            [
                Header(1281), Map(1281, 0, [Actor(100, 0, new(0, 0, 5)), Mob()], 1, true),
                Map(1281, .1, [Mob(Spell(42212))], 1),
                Map(1281, .5, [Status(Mob())], 1),
                Map(1281, 1, [Status(Mob(Spell(42212, 1)))], 1), Map(1281, 2, [Status(Mob())], 1),
                Map(1281, 3, [Mob()], 1),
            ]);
            controller.Seek(2d);
            Check(CountModel("spikes") == 2, "PT observed independent Finish did not append spikes.");
            controller.Seek(3d);
            Check(CountModel("spikes") == 0, "PT true status-slot Lose did not clear all original source spikes.");
            stop();
            Console.WriteLine("PASS production PT actual Finish Spikes sixty-slot Lose all-clear replay control isolation");

            load(id,
            [
                Header(1281), Map(1281, 0, [Actor(100, 0, new(0, 0, 5)), Mob()], 1, true),
                Map(1281, .1, [Mob(oid: 0x4923, objectIndex: 11)], 1),
                Native(.2, 42214, 51), Native(.3, 42214, 52, 0x4923, 11),
                Native(.4, 42215, 53), Native(.5, 42215, 54, 0x4923, 11),
            ]);
            controller.Seek(.25d);
            Check(Shown("zone/void").Length == 0, "Queued old Actor effect rebound to replacement source.");
            controller.Seek(.35d);
            Check(Shown("zone/void").Length == 1, "Current producer identity failed to create effect.");
            controller.Seek(.45d);
            Check(Shown("zone/void").Length == 1, "Old Actor effect cleared replacement's mechanism.");
            controller.Seek(.55d);
            Check(Shown("zone/void").Length == 0, "Current producer identity failed to end effect.");
            controller.Seek(.35d);
            Check(Shown("zone/void").Length == 1, "Source identity receipt failed reverse Seek.");
            stop();
            Console.WriteLine("PASS production PT queued old source identity rejection current source acceptance actual end reverse Seek");

            load(id,
            [
                Header(1281), Map(1281, 0, [Actor(100, 0, new(0, 0, 5)), Mob()], 1, true),
                Map(1281, .1, [Mob(oid: 0x4923, objectIndex: 11)], 1),
                EffectEvent(.2, 10, 42214, 61, target: 100),
                Native(.3, 42214, 61),
                EffectEvent(.4, 10, 42214, 62, target: 100),
                Native(.5, 42214, 62, 0x4923, 11),
                Native(.6, 42215, 63, 0x4923, 11),
            ]);
            controller.Seek(.25d);
            Check(Shown("zone/void").Length == 0
                && ((string?)Read(Script(), "MechanicHintsText"))?.Contains("来源代际") == true,
                "Metadata-free legacy rebound after an already observed ID reuse.");
            controller.Seek(.35d);
            Check(Shown("zone/void").Length == 0, "Late rejected native identity resurrected legacy output.");
            controller.Seek(.55d);
            Check(Shown("zone/void").Length == 1, "Rejected legacy consumed a proven current native packet.");
            controller.Seek(.65d);
            Check(Shown("zone/void").Length == 0, "Current native identity could not end its own effect.");
            controller.Seek(.55d);
            Check(Shown("zone/void").Length == 1, "Cross-stream identity receipts failed reverse Seek.");
            stop();
            Console.WriteLine("PASS production PT legacy-first retired identity rejected before pairing current native retained reverse Seek");

            object NoFacing(uint action)
            {
                var node = (JsonNode)Spell(action);
                node["Rotation"] = null;
                return node;
            }
            foreach (var action in new uint[] { 44690, 44691, 42214, 42216, 44759 })
            {
                load(id,
                [
                    Header(1284), Map(1284, 0, [Actor(100, 0, new(0, 0, 5)), Mob()], 31, true),
                    Map(1284, .1, [Mob(NoFacing(action), rotation: 2)], 31),
                    Map(1284, 1, [Mob(Spell(action, .1, .75f), rotation: 2)], 31),
                ]);
                controller.Seek(.2d);
                Check(Shown().Length == 0, "PT reconstructed missing packet facing from current Actor.");
                Check(((string?)Read(Script(), "MechanicHintsText"))?.Contains("锁定朝向") == true,
                    "PT missing locked direction did not expose the input gap.");
                controller.Seek(1d);
                var geometry = Read(Shown().Single(), "Geometry");
                Check((bool)((dynamic)geometry).Contains(new Vector3(MathF.Sin(.75f) * 4, 0, MathF.Cos(.75f) * 4), 0f)
                    && !(bool)((dynamic)geometry).Contains(new Vector3(-MathF.Sin(.75f) * 4, 0, -MathF.Cos(.75f) * 4), 0f),
                    "PT packet-facing half-plane acquired Actor facing or a second side offset.");
                Check(Read(Script(), "MechanicHintsText") == null, "Resolved cast facing retained stale diagnostic.");
                stop();
            }
            load(id,
            [
                Header(1289), Map(1289, 0, [Actor(100, 0, new(0, 0, 5)), Mob()], 81, true),
                Map(1289, .1, [Mob(Spell(43132), position: new(1, 0, 2), rotation: 1)], 81),
                Map(1289, .5, [Mob(Spell(43132), position: new(3, 0, 4), rotation: 2)], 81),
            ]);
            controller.Seek(.6d);
            var moving = Read(Shown().Single(), "Geometry");
            Check((Vector3)Read(moving, "Position") == new Vector3(3, 0, 4)
                && (bool)((dynamic)moving).Contains(new Vector3(3 + MathF.Sin(2) * 4, 0, 4 + MathF.Cos(2) * 4), 0f),
                "PT actor-facing kick failed to follow current position and heading.");
            stop();
            Console.WriteLine("PASS production PT missing facing withheld packet half-planes no double offset dynamic 43132 pose");

            var playerPosition = new Vector3(0, 0, 5);
            load(id,
            [
                Header(1281), Map(1281, 0, [Actor(100, 0, playerPosition), Mob(oid: 0x491B, target: 100)], 1, true),
            ]);
            controller.Seek(.1d);
            var snapshotTurtle = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
            const string solverNs = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
            dynamic solverTurtle = Activator.CreateInstance(assembly.GetType(solverNs + "SafeZoneSolver")!)!;
            dynamic playerTurtle = Activator.CreateInstance(assembly.GetType(solverNs + "PlayerState")!,
                [playerPosition, 0f, 6f, false, false, 0f, true, (int)Read(snapshotTurtle, "Version")])!;
            dynamic capabilityTurtle = Activator.CreateInstance(assembly.GetType(solverNs + "MovementCapability")!,
                [6f, 6f, 0f, false, 6f, 6f])!;
            dynamic optionsTurtle = Activator.CreateInstance(assembly.GetType(solverNs + "SolveOptions")!)!;
            dynamic solutionTurtle = solverTurtle.Solve((dynamic)snapshotTurtle, playerTurtle, capabilityTurtle,
                optionsTurtle, runtime.CurrentTime, null);
            Check((bool)solutionTurtle.IsSuccess && !(bool)Read((object)solutionTurtle, "IsLeastDangerFallback")
                && solutionTurtle.TargetPosition is Vector3 safe && safe.LengthSquared() > 36,
                "PT turtle production hard danger did not produce a real safe escape.");
            Check(!(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest"),
                "PT replay published PR control.");
            stop();
            Console.WriteLine("PASS production PT turtle current target hard navigation real solver no fallback replay control isolation");
        }
        finally { dataProperty.SetValue(null, previousData); }
    }
}
