using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTactical(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string guid = "masterplayer.novice.tactical";
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => ((IEnumerable)manager.Running.Values).Cast<object>().Select(v => Value(v, "Script"))
            .Single(v => v.GetType().Name == "NoviceTactical");
        object Boss(object? cast = null) => Actor(1, 0x463B, default, cast: cast, targetable: false);
        object Helper(ulong id, object? cast = null) => Actor(id, 0x233C, default, cast: cast);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        object[] Initial() => [Boss(), Helper(10), Helper(11), Helper(12),
            Actor(100, 0, new(0, 0, 8)), Actor(101, 0, new(10, 0, 8))];
        string FrameAt(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(1127, t, actors, full: full, removed: removed, party: party);
        object[] Tactical(string part) => previews().Where(p => ((string)Value(p, "Id")).Contains("tactical/" + part)).ToArray();
        string Icon(double t, uint icon, ulong source, ulong target) => JsonSerializer.Serialize(new
        {
            t, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = source, TargetId = target, MarkerId = icon, Timestamp = Start.AddSeconds(t) },
        });
        string Native(double t, uint action, uint seq, ulong target = 100) => JsonSerializer.Serialize(new
        {
            t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 10ul, ActionId = action, MainTargetId = target, GlobalSequence = seq,
                Targets = new ulong[] { 100, 101 }, Timestamp = Start.AddSeconds(t) },
        });
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", flags)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", flags)!.GetValue(facing)!
                && !((IEnumerable)hints.GetType().GetField("forbiddenTargets", flags)!.GetValue(hints)!).Cast<object>().Any()
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld"),
                "Tactical replay leaked PR native execution.");
        }
        foreach (var (territory, entry) in new[]
        {
            (1127u, guid), (1128u, "masterplayer.novice.tactical-floor"), (1129u, "masterplayer.novice.tactical-advanced"),
        })
        {
            load(entry, [Header(territory), Frame(territory, 0, [Boss(), Actor(100, 0, new(0, 0, 8))], full: true),
                Frame(territory, 1, [])]);
            controller.Seek(.2);
            Check(runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary != null,
                $"Tactical {territory} requires combat or targetability.");
            stop();
        }
        Console.WriteLine("PASS native novice Tactical production three entries untargetable precombat exact R20 arena owner cleanup");

        load(guid, [Header(1127), FrameAt(0, Initial(), true),
            FrameAt(.1, [Helper(10, Cast(40697, 8, new(2, 0, 3)))]),
            FrameAt(1, [Helper(10)]), FrameAt(2, [])]);
        controller.Seek(.5);
        Check(dangers() == 1 && previews().Any(p => ((string)Value(p, "Id")).Contains("cast/")),
            "Ordinary Tactical AOE missing native navigation/dual preview.");
        controller.Seek(1.2);
        Check(dangers() == 0, "Tactical true cancellation retained ordinary AOE.");
        controller.Seek(.5);
        manager.SetMechanismEnabled(guid, "NoviceTactical", false);
        controller.Seek(.6);
        Check(dangers() == 0 && previews().Length == 0, "Tactical mechanism off leaked owner outputs.");
        manager.SetMechanismEnabled(guid, "NoviceTactical", true);
        controller.Seek(.5);
        Isolated();
        stop();
        Console.WriteLine("PASS native novice Tactical production ordinary AOE dual preview navigation true cancel mechanism off reverse Seek replay isolation");

        load(guid, [Header(1127), FrameAt(0, Initial(), true), Icon(.1, 197, 100, 101),
            FrameAt(.2, [Helper(10, Cast(40689, 5, new(2, 0, 0), startedAt: .2)),
                Helper(11, Cast(40702, 5, new(3, 0, 0), startedAt: .2))]),
            FrameAt(1, [Helper(10), Helper(11)]), Native(2, 40690, 1, 101), Native(3, 40690, 2, 101),
            Native(4, 40690, 3, 101), Native(5, 40690, 4, 101), Native(6, 40690, 5, 101), FrameAt(7, [])]);
        controller.Seek(.5);
        Check(Tactical("chaser").Count(p => ((string)Value(p, "Id")).EndsWith("/display")) == 2 && dangers() == 4,
            "Tactical independent chasers missing display, personal R9 or extra positioning.");
        controller.Seek(6.2);
        Check(Tactical("chaser").Count(p => ((string)Value(p, "Id")).EndsWith("/display")) == 1 && dangers() == 2,
            "Tactical Native packet consumption multiplied by target count or crossed component groups.");
        controller.Seek(.5);
        Isolated();
        stop();
        Console.WriteLine("PASS native novice Tactical production independent chasers first hit raw target actor five Native packets dual views navigation reverse Seek");

        load(guid, [Header(1127), FrameAt(0, Initial(), true),
            Icon(.1, 139, 100, 100), Icon(.2, 375, 101, 101), Icon(.3, 318, 100, 100), Icon(.4, 317, 101, 101),
            Native(1, 40692, 1, 101), Native(2, 40682, 2, 100), Native(3, 40695, 3, 101), FrameAt(4, [])]);
        controller.Seek(.5);
        Check(Tactical("mark").Length == 4 && dangers() >= 3, "Tactical four independent mark previews/navigation missing.");
        controller.Seek(1.2);
        Check(Tactical("mark").Length == 2, "Tactical native spread global/FIFO did not preserve stacks.");
        controller.Seek(3.2);
        Check(Tactical("mark").Length == 0 && dangers() == 0, "Tactical main-target stack clearing leaked.");
        controller.Seek(.5);
        Isolated();
        stop();
        Console.WriteLine("PASS native novice Tactical production four mark components original target identity Native main target consumption dual previews navigation Seek");

        load(guid, [Header(1127), FrameAt(0, Initial(), true),
            FrameAt(.1, [Helper(10, Cast(40716, 8, new(-8, 0, 0))),
                Helper(11, Cast(40716, 9, new(8, 0, 0))), Helper(12, Cast(40727, 10, default))]),
            FrameAt(1, [Helper(10), Helper(12)]), FrameAt(2, [])]);
        controller.Seek(.5);
        Check(Tactical("tower").Length == 3 && dangers() == 2,
            "Tower group unions merged components or required each empty tower.");
        controller.Seek(1.2);
        Check(Tactical("tower").Length == 1 && dangers() == 1, "Tower true Finish source cleanup differs.");
        stop();
        Console.WriteLine("PASS native novice Tactical production two independent tower unions actual danger registration dual previews and source Finish");

        load(guid, [Header(1127), FrameAt(0, Initial(), true),
            FrameAt(.1, [Helper(10, Cast(40743, 8, new(0, 0, 500))),
                Helper(11, Cast(40757, 8, new(500, 0, 8)))]), FrameAt(9, [Helper(10), Helper(11)]), FrameAt(10, [])]);
        controller.Seek(7.9);
        Check(Tactical("gaze").Length == 5 && dangers() == 0, "Tactical two 10000 gazes lost eye/pupil/arc or became invented dangers.");
        Isolated();
        controller.Seek(9.2);
        Check(Tactical("gaze").Length == 0, "Tactical gaze true Finish retained eye.");
        stop();
        Console.WriteLine("PASS native novice Tactical production two independent default10000 gaze eye pupil view arc no invented navigation PR replay isolation Finish");

        string TetherEvent(double t, ulong source, ulong target, uint tether, bool present) => JsonSerializer.Serialize(new
        {
            t, kind = present ? "Tether" : "TetherCancel",
            type = "PromeRotation.LogSystem.LogSystemTether" + (present ? "" : "Cancel") + "Event",
            data = new { SourceId = source, TargetId = target, TetherId = tether, Timestamp = Start.AddSeconds(t) },
        });
        var bomb = Actor(20, 0x4640, new(10, 0, 0));
        load(guid, [Header(1127), FrameAt(0, [.. Initial(), bomb], true),
            FrameAt(.1, [Actor(1, 0x463B, default, targetable: false, tethers: [new { Id = 84u, TargetId = 20ul }])]),
            TetherEvent(.2, 1, 20, 84, true), TetherEvent(1, 1, 20, 84, true),
            TetherEvent(2, 1, 20, 84, false), FrameAt(3, [])]);
        controller.Seek(.5);
        Check(Tactical("intercept").Length == 1 && dangers() == 1,
            "Tactical raw/event bomb confirmation duplicated source transition.");
        controller.Seek(1.2);
        Check(Tactical("intercept").Length == 0 && dangers() == 0, "Real duplicate bomb tether did not disable Count1 outputs.");
        controller.Seek(2.2);
        Check(Tactical("intercept").Length == 1 && dangers() == 1, "Tactical bomb untether failed first reference removal.");
        controller.Seek(.5);
        Isolated();
        stop();
        Console.WriteLine("PASS native novice Tactical production bomb raw event confirmation real duplicate count untether navigation dual line preview Seek isolation");

        load(guid, [Header(1127), FrameAt(0, Initial(), true),
            FrameAt(.1, [Helper(10, Cast(40706, 8, default)), Helper(11, Cast(40709, 8, default)),
                Actor(100, 0, new(1, 0, 0))]),
            StatusEvent(1, 100, 160, 0, true), StatusEvent(2, 100, 160, 0, false),
            FrameAt(3, [Helper(10), Helper(11)]), FrameAt(4, [])]);
        controller.Seek(.5);
        var landings = Tactical("kb").Where(p => ((string)Value(p, "Id")).EndsWith("/player")).ToArray();
        Check(landings.Select(p => ((Vector3)Value(Value(p, "Geometry"), "Position")).X).Order().SequenceEqual(new[] { 11f, 16f })
            && dangers() == 1, $"Tactical independent display-only knockbacks lost exact landing or added displacement navigation: " +
                $"{string.Join(',', landings.Select(p => Value(Value(p, "Geometry"), "Position")))}; dangers={dangers()}.");
        controller.Seek(1.2);
        Check(Tactical("kb").Length == 0 && dangers() == 1, "Tactical immunity removed source extra standing rule or failed hide projection.");
        controller.Seek(2.2);
        Check(Tactical("kb").Length == 4 && dangers() == 1, "Tactical status loss before snapshot failed restore projection.");
        Isolated();
        stop();
        Console.WriteLine("PASS native novice Tactical production independent radial knockbacks exact landings display only source positioning status event overlay replay isolation");

        load(guid, [Header(1127), FrameAt(0, Initial(), true),
            FrameAt(.1, [Helper(10, Cast(40694, 2, default)), Helper(11, Cast(40760, 2, default)),
                Helper(12, Cast(40732, 8, default))]),
            FrameAt(1, [Helper(10), Helper(11)]), Native(2, 40693, 1),
            FrameAt(3, [Helper(12)]), Native(4, 40759, 2), FrameAt(5, [])]);
        controller.Seek(.5);
        Check(Tactical("aoe").Length == 2 && dangers() == 2, "Ring head and surge navigation missing.");
        controller.Seek(1.2);
        Check(Tactical("aoe").Length == 2, "Ring consumed on visual Finish.");
        controller.Seek(3.2);
        Check(Tactical("aoe").Length == 1 && dangers() == 1, "Surge Finish did not remove source head independently.");
        controller.Seek(4.2);
        Check(Tactical("aoe").Length == 0 && dangers() == 0, "Native ring FIFO retained tail.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native novice Tactical production consumed-clock ring sequence visual Finish preservation Native FIFO independent surge true Finish dual views navigation Seek");

        string Map(double t, uint index, uint state) => JsonSerializer.Serialize(new
        {
            t, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = index, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) },
        });
        load(guid, [Header(1127), FrameAt(0, Initial(), true), Map(.1, 0, 0x20001),
            Map(1, 0, 0x80004), FrameAt(2, [])]);
        controller.Seek(.5);
        Check(Tactical("ready").Length == 1 && dangers() == 1, "Map ready circle display/navigation missing.");
        controller.Seek(1.2);
        Check(Tactical("ready").Length == 0 && dangers() == 0, "Ready map clear retained owner outputs.");
        stop();
        Console.WriteLine("PASS native novice Tactical production map preparation circle display enter constraint real clear and owner cleanup");
    }
}
