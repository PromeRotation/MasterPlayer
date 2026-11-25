using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using ECommons.DalamudServices;

internal static partial class NativeDungeonReplayRegression
{
    // Shared by the Heaven-on-High source-session oracle.
    private static Lumina.GameData? floorGameData;

    private static void RunHeavenswardFloor(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object obj, string name) => obj.GetType()
            .GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        var assembly = ((object)((IEnumerable)manager.Scripts).Cast<dynamic>().First()).GetType().Assembly;
        var hostType = assembly.GetType("MasterPlayer.MechanicHost.MechanicHostController")!;
        var host = RuntimeHelpers.GetUninitializedObject(hostType);
        var palaceType = assembly.GetType("MasterPlayer.Mechanics.PalaceOfTheDead.PalaceScript")!;
        var encounterField = palaceType.GetField("encounter", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var floorField = palaceType.GetField("floor", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var dataType = assembly.GetType("MasterPlayer.Mechanics.PalaceOfTheDead.PalaceFloorData")!;
        var traps = (IDictionary)dataType.GetField("Traps", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        using var game = new Lumina.GameData(Environment.GetEnvironmentVariable("FFXIV_SQPACK")!,
            new Lumina.LuminaOptions { DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified });
        var originalData = Svc.Data;
        var dataProperty = typeof(Svc).GetProperty(nameof(Svc.Data), BindingFlags.Static | BindingFlags.Public)!;
        dataProperty.SetValue(null, new PalaceGameData(game));
        try
        {
        manager.SetScriptEnabled("promeradar.potd1-150", true);
        foreach (var (id, territory, oid, floorSet) in new[]
        {
            ("masterplayer.palace.deathgaze", (ushort)561, 0x1692u, 1),
            ("masterplayer.palace.spurge", (ushort)562, 0x169Fu, 2),
            ("masterplayer.palace.ningishzida", (ushort)563, 0x16ACu, 3),
            ("masterplayer.palace.ixtab", (ushort)564, 0x16B9u, 4),
            ("masterplayer.palace.edda", (ushort)565, 0x16C6u, 5),
            ("masterplayer.palace.blackrider", (ushort)593, 0x1814u, 6),
            ("masterplayer.palace.yaquaru", (ushort)594, 0x1815u, 7),
            ("masterplayer.palace.gudanna", (ushort)595, 0x1816u, 8),
            ("masterplayer.palace.godmother", (ushort)596, 0x1817u, 9),
            ("masterplayer.palace.nybeth", (ushort)597, 0x1808u, 10),
            ("masterplayer.palace.alicanto", (ushort)598, 0x1818u, 11),
            ("masterplayer.palace.kirtimukha", (ushort)599, 0x1819u, 12),
            ("masterplayer.palace.alfard", (ushort)600, 0x181Au, 13),
            ("masterplayer.palace.ah-puch", (ushort)601, 0x181Bu, 14),
            ("masterplayer.palace.tisiphone", (ushort)602, 0x181Cu, 15),
        })
        {
            var trap = ((Vector2[])traps[territory]!)[0];
            var center = new Vector3(trap.X, 0, trap.Y);
            var rooms = Enumerable.Repeat((ushort)0x80, 25).ToArray();
            object[] party = [new { Slot = 0, EntityId = 100ul }];
            string MapFrame(double t, IEnumerable<object> actors, int floor, bool full = false, bool map = true)
            {
                var node = JsonNode.Parse(Frame(territory, t, actors, full, party: party))!;
                node["data"]!["DeepDungeon"] = map
                    ? JsonSerializer.SerializeToNode(new { DungeonId = 1, Floor = floor, Tileset = 0, Rooms = rooms })
                    : null;
                return node.ToJsonString();
            }
            object Mob(object? cast = null) => Actor(10, 0x1763, center, combat: true, cast: cast,
                stackedStatus: 197, statusExpiresAt: Start.AddSeconds(30), subKind: 5, isEnemy: true);
            object Spell(uint action)
            {
                var node = JsonSerializer.SerializeToNode(Cast(action, 3, center, startedAt: 12))!;
                node["IsSpell"] = true;
                return node;
            }
            var gaze = Cast(6953, 3.7, center, target: 100);
            load(id,
            [
                Header(territory),
                MapFrame(0, [Actor(100, 0, center + Vector3.UnitZ * 5, rotation: MathF.PI), Mob()], floorSet * 10 - 9, full: true),
                MapFrame(.1, [Mob(gaze)], floorSet * 10 - 9),
                MapFrame(3.5, [Mob(gaze)], floorSet * 10 - 9),
                MapFrame(8, [Mob(gaze)], floorSet * 10 - 9),
                MapFrame(8.1, [Mob()], floorSet * 10 - 9),
                MapFrame(8.2, [Mob(gaze)], floorSet * 10 - 9),
                // Missing floor metadata still uses the other native script's
                // current Boss lifecycle; it never consults a bridge session.
                MapFrame(9, [Actor(1, oid, center, combat: true)], floorSet * 10, map: false),
                MapFrame(9.1, [Actor(1, oid, center, combat: true, dead: true)], floorSet * 10, map: false),
                MapFrame(10, [Mob()], floorSet * 10 - 9),
                MapFrame(11, [Mob()], floorSet * 10),
                MapFrame(12, [Actor(1, oid, center, combat: true, subKind: 5, isEnemy: true,
                    cast: Spell(6418))], floorSet * 10),
                MapFrame(12.2, [Mob(Spell(6386))], floorSet * 10),
            ]);
            object PalaceScript() => ((IEnumerable)manager.Running.Values).Cast<dynamic>()
                .Select(r => (object)r.Script).Single(palaceType.IsInstanceOfType);
            object Encounter() => encounterField.GetValue(PalaceScript())!;
            bool Spikes() => ((IEnumerable)Encounter().GetType().GetMethod("ForbiddenAutoAttackTargets",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Encounter(), [(object)runtime.CurrentTime])!)
                .Cast<ulong>().Contains(10ul);
            int ZonePreviews() => previews().Count(p => ((string)Value(p, "Id")).StartsWith("MasterPlayer_Script_promeradarpotd1150_", StringComparison.Ordinal));
            int Walls() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count();
            controller.Seek(3.5d);
            Check(ZonePreviews() > 3 && Walls() > 0 && dangers() > 0 && Spikes(),
                $"Palace{floorSet * 10} omitted native walls, trap hazards, gaze or Spikes: zone={ZonePreviews()}, total={previews().Length}, walls={Walls()}, danger={dangers()}, spikes={Spikes()}, running={string.Join(',', ((IEnumerable)manager.Running.Keys).Cast<object>())}.");
            Check((bool)hostType.GetMethod("IsReplaySuppressed", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(host, null)!,
                "A migrated Palace territory still accepts old bridge output.");
            controller.Seek(8d);
            Check(previews().Any(p => ((string)Value(p, "Id")).EndsWith("/eye")), "Overdue raw gaze expired.");
            controller.Seek(8.1d);
            Check(!previews().Any(p => ((string)Value(p, "Id")).EndsWith("/eye")), "Raw Finish retained gaze.");
            var running = (IDictionary)manager.GetType().GetField("_running", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
            var entries = running.Keys.Cast<string>().OrderByDescending(key => key == "promeradar.potd1-150")
                .Select(key => (Key: key, Value: running[key])).ToArray();
            running.Clear();
            foreach (var entry in entries) running.Add(entry.Key, entry.Value);
            controller.Seek(9d);
            Check(ZonePreviews() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary != null,
                $"Palace{floorSet * 10} duplicated Boss output or failed to use native Boss state without floor metadata.");
            controller.Seek(9.1d);
            Check(previews().Any(p => ((string)Value(p, "Id")).EndsWith("/eye")),
                "Native Boss completion did not restore independent common hints.");
            controller.Seek(10d);
            Check(!Spikes(), "Boss transition refreshed the original ten-second Spikes clock.");
            controller.Seek(11d);
            Check(Walls() == 0, "Real Boss floor published ordinary room walls.");
            controller.Seek(3.5d);
            Check(ZonePreviews() > 3 && Walls() > 0 && Spikes(), "Reverse Seek failed to rebuild native Zone mechanisms.");
            var floorModel = floorField.GetValue(PalaceScript())!;
            Check((bool)floorModel.GetType().GetProperty("HasMap", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(floorModel)!, "Native floor model was not consumed.");
            if (territory == 561)
            {
                var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
                const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
                dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
                dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                    [center, 0f, 6f, false, false, 0f, true, (int)Value(snapshot, "Version")])!;
                dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!,
                    [6f, 6f, 0f, false, 6f, 6f])!;
                dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
                dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
                Check((bool)result.IsSuccess && !(bool)Value((object)result, "IsLeastDangerFallback")
                    && result.TargetPosition is Vector3,
                    "Early Palace production solver failed to escape its native trap hazards.");
                var destination = (Vector3)result.TargetPosition;
                var allTraps = (Vector2[])traps[territory]!;
                Check(allTraps.All(p => Vector2.Distance(p, new(destination.X, destination.Z)) > 2)
                    && ((IEnumerable)Value(snapshot, "HardObstacles")).Cast<dynamic>()
                        .All(o => !o.Geometry.Contains(destination, .001f)),
                    "Early Palace solver destination entered a source trap or native room wall.");
                Console.WriteLine("PASS production Palace early native trap escape: real solver success no fallback source trap distances and wall exclusion");

                // Apply the new world without running OnUpdate: the CastStart
                // callback itself must retire existing ordinary-floor output.
                var world = (object)controller.World;
                var worldFrameType = assembly.GetType("MasterPlayer.Replay.ReplayWorldFrame")!;
                var bossFrame = JsonNode.Parse(MapFrame(3.5, [Actor(1, oid, center, combat: true)],
                    floorSet * 10, map: false))!["data"]!.ToJsonString();
                world.GetType().GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(world, [JsonSerializer.Deserialize(bossFrame, worldFrameType)]);
                var bossPacket = JsonNode.Parse(CastEvent(3.5, 1, 6418, 3, center))!["data"]!.ToJsonString();
                palaceType.GetMethod("HandleEvent", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(PalaceScript(), [JsonSerializer.Deserialize<PromeRotation.LogSystem.LogSystemCastStartEvent>(bossPacket)]);
                Check(ZonePreviews() == 0 && Walls() == 0
                    && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary != null,
                    "Rejecting the Boss packet retained the previous ordinary-floor output until OnUpdate.");
                Console.WriteLine("PASS production Palace Boss packet handoff: existing floor output removed synchronously before next OnUpdate");

                manager.SetScriptEnabled(id, false);
                controller.Seek(12d);
                Check(dangers() == 0 && ZonePreviews() == 0,
                    "The ordinary-floor layer took over a disabled native Boss's raw Bombination.");
                controller.Seek(12.2d);
                Check(dangers() == 1 && ZonePreviews() == 1,
                    "Excluding the disabled Boss also removed independent common raw AOE on its floor.");
                manager.SetScriptEnabled(id, true);
            }
            stop();
            Check(Walls() == 0, "Stopping Palace retained owner obstacles.");
            Console.WriteLine($"PASS production Palace{floorSet * 10} native Zone: real wall/trap consumers raw gaze overdue/Finish Spikes original clock native Boss isolation without map reverse Seek and owner cleanup");
        }
        }
        finally { dataProperty.SetValue(null, originalData); }
    }
}
