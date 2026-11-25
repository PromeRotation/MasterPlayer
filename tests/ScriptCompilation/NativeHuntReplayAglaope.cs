using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntAglaope(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.hunts.simple";
        const uint territory = 816;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Value(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } property) return property.GetValue(obj)!;
                if (type.GetField(name, flags) is { } field) return field.GetValue(obj)!;
            }
            throw new MissingMemberException(obj.GetType().FullName, name);
        }
        object Script() => Value(manager.Running[id], "Script");
        object[] Group(string prefix) => previews().Where(p => ((string)Value(p, "Id")).Contains(prefix)).ToArray();
        string? Text(string name) => (string?)Value(Script(), name);
        object[] Pending() => ((IEnumerable)Value(Value(Value(Script(), "encounter"), "aglaopePending"), "Pending")).Cast<object>().ToArray();
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        object[] players = [Actor(100, 0, new(10, 0, 0)), Actor(101, 0, new(20, 0, 0))];
        object Incoming(uint sequence, byte index, bool confirmed)
        {
            var actor = JsonSerializer.SerializeToNode(players[0])!;
            actor["IncomingEffects"] = JsonSerializer.SerializeToNode(new[]
                { new { Slot = 3, GlobalSequence = sequence, TargetIndex = index, TargetConfirmed = confirmed, Knockback = false } });
            return actor;
        }
        string Native(double at, uint action, uint sequence, ulong[][]? raw) => JsonSerializer.Serialize(new
        {
            t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 999ul, ActionId = action, MainTargetId = 404ul, GlobalSequence = sequence,
                Targets = new ulong[] { 101, 100 }, RawEffects = raw, Timestamp = Start.AddSeconds(at) }
        });
        ulong[][] Payload()
        {
            ulong[][] raw = [new ulong[8], new ulong[8]];
            raw[1][0] = 14ul | (123ul << 48);
            raw[1][7] = 14ul | (991ul << 48) | (0x11ul << 24);
            return raw;
        }
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", flags)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", flags)!.GetValue(facing)!
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld")
                && !(bool)Value(Script(), "PyreticActive"), "Aglaope invented control or leaked replay execution.");
        }

        load(id,
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x281E, default, combat: true), Actor(999, 0x233C, default), .. players], full: true, party: party),
            Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(16824, 3, default, startedAt: .1))], party: party),
            Frame(territory, 1, [Actor(999, 0x233C, default)], party: party),
            Native(1.2, 16824, 7, Payload()),
            EffectEvent(1.21, 999, 16824, 7, target: 100),
            Frame(territory, 2.5, [Incoming(7, 1, true)], party: party),
            Frame(territory, 2.7, [Incoming(7, 1, false)], party: party),
            Frame(territory, 3.5, [Incoming(7, 1, true)], party: party),
            Frame(territory, 4, [], removed: [1], party: party)
        ]);
        controller.Seek(1.1);
        Check(Group("aglaope/sonata").Length == 1 && dangers() == 1, "Sonata cancel cleared persistent circle.");
        var circle = Value(Group("aglaope/sonata").Single(), "Geometry");
        Check((float)Value(circle, "Radius") == 16.2f, "Sonata R40 substituted for preventive R16.2.");
        controller.Seek(2.6);
        Check(Group("aglaope/sonata").Length == 1 && dangers() == 1,
            "Raw payload target-index/first-any-status ACK or delayed pending wait failed.");
        Check(Pending().Length == 1 && (uint)Value(Pending()[0], "StatusId") == 991,
            "First ACK failed to consume exactly the first non-991 status.");
        SolveAglaopeDanger(runtimeObject, Value);
        controller.Seek(3.4);
        Check(Group("aglaope/sonata").Length == 1 && Pending().Length == 1,
            "Fixed 2.1-second timer replaced actual delayed pending wait.");
        controller.Seek(3.6);
        Check(Group("aglaope/sonata").Length == 0 && dangers() == 0, "Second real ACK failed to release pending991.");
        controller.Seek(1.3);
        Check(Group("aglaope/sonata").Length == 1 && dangers() == 1, "Reverse Seek lost raw pending payload.");
        Isolated();
        controller.Seek(4.1);
        Check(previews().Length == 0 && dangers() == 0, "Aglaope owner removal retained Sonata.");
        stop();
        Console.WriteLine("PASS native hunt Aglaope production raw payload eight slots original target index first any-status ACK beyond2.1 delayed pending wait persistent Sonata shared previews real solver reverse Seek");

        load(id,
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x281E, default, combat: true), Actor(999, 0x233C, default), .. players], full: true, party: party),
            Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(18056, 3, default, startedAt: .1))], party: party),
            Frame(territory, 1, [Actor(100, 0, new(10, 0, 0), stackedStatus: 160)], party: party),
            Frame(territory, 2, [players[0]], party: party),
            Frame(territory, 2.1, [Actor(999, 0x233C, new(25, 0, 0), cast: Cast(18056, 3, default, startedAt: .1))], party: party),
            Frame(territory, 2.2, [Actor(100, 0, new(30.01f, 0, 0))], party: party),
            Frame(territory, 2.4, [players[0]], party: party),
            Frame(territory, 3, [Actor(999, 0x233C, default)], party: party),
            Frame(territory, 4, [], removed: [1], party: party)
        ]);
        controller.Seek(.5);
        Check(Group("knockback/").Length == 2 && dangers() == 0 && Text("PredictedDamageText")?.Contains("2人") == true,
            "Aglaope KB lost projection/prediction or became a dangerous R30 circle.");
        var landing = Value(Group("knockback/").Single(p => ((string)Value(p, "Id")).EndsWith("/landing")), "Geometry");
        Check((Vector3)Value(landing, "Position") == new Vector3(20, 0, 0), "Aglaope radial10 source origin differs.");
        Isolated();
        controller.Seek(1.5);
        Check(Group("knockback/").Length == 0 && dangers() == 0, "Aglaope immunity failed.");
        controller.Seek(2.15);
        Check(Group("knockback/").Length == 2, "Aglaope real immunity Lose failed recovery.");
        landing = Value(Group("knockback/").Single(p => ((string)Value(p, "Id")).EndsWith("/landing")), "Geometry");
        Check((Vector3)Value(landing, "Position") == new Vector3(20, 0, 0), "Aglaope locked origin followed moved helper.");
        controller.Seek(2.3);
        Check(Group("knockback/").Length == 0, "Aglaope range30 did not exclude outside player.");
        controller.Seek(2.5);
        Check(Group("knockback/").Length == 2, "Aglaope projection did not recover inside range.");
        object snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        Check(!((IEnumerable)Value(snapshot, "GoalAreas")).Cast<object>().Any()
            && !((IEnumerable)Value(snapshot, "HardObstacles")).Cast<object>().Any(), "Display-only KB entered another navigation channel.");
        controller.Seek(3.5);
        Check(Group("knockback/").Length == 0 && Text("PredictedDamageText") == null, "Actual Finish retained KB/prediction.");
        controller.Seek(.5);
        Check(Group("knockback/").Length == 2, "Aglaope KB reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native hunt Aglaope independent radial10 range30 knockback immutable source model immunity real Finish raidwide diagnostic no new navigation execution or facing");

        var oldPayload = JsonNode.Parse(Native(1, 16824, 10, null))!;
        oldPayload["data"]!.AsObject().Remove("RawEffects");
        load(id,
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x281E, default, combat: true), Actor(999, 0x233C, default), .. players], full: true, party: party),
            Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(16824, 3, default, startedAt: .1))], party: party),
            Frame(territory, .2, [Actor(100, 0, new(10, 0, 0), stackedStatus: 991)], party: party),
            oldPayload.ToJsonString(),
            Frame(territory, 2, [], party: party)
        ]);
        controller.Seek(1.1);
        Check(Group("aglaope/sonata").Length == 0 && Text("MechanicHintsText")?.Contains("缺少原始状态槽") == true,
            "Old recording silently invented a pending window.");
        stop();
        Console.WriteLine("PASS native hunt Aglaope older recording raw-payload absence explicit degraded diagnostic without current-status or fixed-delay synthesis");

        load(id,
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x281E, default, combat: true), Actor(999, 0x233C, default), .. players], full: true, party: party),
            Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(16824, 3, default, startedAt: .1))], party: party),
            Native(1, 16824, 7, Payload()),
            Frame(territory, 2, [], removed: [1], party: party)
        ]);
        controller.Seek(1.5);
        Check(Group("aglaope/sonata").Length == 1 && Pending().Length == 2, "Owner cleanup fixture was never active.");
        controller.Seek(2.1);
        Check(previews().Length == 0 && dangers() == 0, "Owner removal failed to revoke an active pending Sonata.");
        stop();
        Console.WriteLine("PASS native hunt Aglaope active pending Sonata owner removal clears production previews and navigation");
    }

    private static void SolveAglaopeDanger(object runtimeObject, Func<object, string, object> value)
    {
        dynamic runtime = runtimeObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
        // The live script keeps terrain ownership. Supply only the harness's local
        // search boundary while testing the production navigation snapshot.
        dynamic template = Activator.CreateInstance(assembly.GetType("MasterPlayer.Spatial.SafeZone.CircleTemplate")!, [50f])!;
        runtime.Manager.SetArenaBoundary(template.Resolve(Vector3.Zero, 0f, null));
        try
        {
            dynamic snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
            dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
            dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                [new Vector3(10, 0, 0), 0f, 6f, false, false, 0f, true, (int)snapshot.Version])!;
            dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
            dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
            dynamic solution = solver.Solve(snapshot, player, capability, options, runtime.CurrentTime, null);
            var fallback = (bool)solution.GetType().GetProperty("IsLeastDangerFallback", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue((object)solution)!;
            Check((bool)solution.IsSuccess && !fallback, "Real solver could not escape migrated Sonata.");
            var path = ((IEnumerable)solution.ExecutablePath).Cast<object>().ToArray();
            if (path.Length == 0) path = ((IEnumerable)solution.ContinuationPath).Cast<object>().ToArray();
            Check(path.Length > 0 && ((Vector3)value(path[^1], "Position")).Length() > 16.2f,
                "Real Sonata escape ends inside the preventive circle.");
        }
        finally { runtime.Registry.ClearArenaBoundary(); }
    }
}
