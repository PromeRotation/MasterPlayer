using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCoil(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script()
        {
            var context = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return Value(context, "Script");
        }
        object Encounter()
        {
            var type = Script().GetType();
            while (type.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance) == null) type = type.BaseType!;
            return type.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        }
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(prefix)).ToArray();
        bool Active() => Value(Encounter(), "Active") != null;
        int Stage() => (int)Encounter().GetType().GetField("gauntletStage", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Encounter())!;
        void Assign(string id)
        {
            var descriptor = ((IEnumerable)manager.Scripts).Cast<object>().Single(d => (string)Value(Value(d, "Metadata"), "Id") == id);
            dynamic store = managerObject.GetType().GetMethod("GetScriptSettings", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(managerObject, [descriptor])!;
            var roles = new[] { "MT", "OT", "H1", "H2", "M1", "M2", "R1", "R2" };
            var assignmentType = Script().GetType().GetProperty("Slot1")!.PropertyType;
            for (var i = 0; i < 8; ++i) store.SetPropertyValue("Slot" + (i + 1), Enum.Parse(assignmentType, roles[i]));
        }
        string Cancel(double t, ulong source, uint action) => JsonSerializer.Serialize(new
        {
            t, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
            data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(t) }
        });
        string Director(double t, uint command) => JsonSerializer.Serialize(new
        {
            t, kind = "DirectorUpdate", type = "PromeRotation.LogSystem.LogSystemDirectorUpdateEvent",
            data = new { Command = command, Timestamp = Start.AddSeconds(t) }
        });
        string Status(double t, ulong actor, uint status, double duration) => JsonSerializer.Serialize(new
        {
            t, kind = "StatusAdd", type = "PromeRotation.LogSystem.LogSystemStatusAddEvent",
            data = new { TargetId = actor, StatusId = status, Duration = (float)duration, Timestamp = Start.AddSeconds(t) }
        });
        object[] party = Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        object[] Players(Vector3 center) => Enumerable.Range(0, 8).Select(i =>
            Actor((ulong)(100 + i), 0, center + new Vector3(i + 1, 0, 0), classJob: 19)).ToArray();

        // Each independent node must start with exactly its own capability set.
        var centers = new[] { new Vector3(0, 0, 77), new Vector3(0, 0, 112), new Vector3(-44, 0, 94),
            new Vector3(-43, 0, 52), new Vector3(0, 0, 39), new Vector3(46, 0, 52), new Vector3(41, 0, 94) };
        for (uint i = 0; i < 7; ++i)
        {
            var center = centers[i];
            load("masterplayer.raid.coil2",
            [
                Header(242), Frame(242, 0, [Actor(1, 0x7D9 + i, center, combat: true), .. Players(center),
                    Actor(10, 0x8E2, center, cast: Cast(1217, 3, center, startedAt: 0))], full: true, party: party),
                Frame(242, 4, [Actor(10, 0x8E2, center)], party: party), Frame(242, 5, [], party: party),
            ]);
            controller.Seek(.5);
            Check(Active() && (uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x7D9 + i
                && previews().Length == 1 && dangers() == 1, "Node actual DLL identity/cannon.");
            controller.Seek(4.1); Check(previews().Length == 0 && dangers() == 0, "Node cast finish cleanup.");
            stop();
        }
        var c = centers[0];
        load("masterplayer.raid.coil2",
        [
            Header(242), Frame(242, 0, [Actor(1, 0x7D9, c, combat: true), .. Players(c),
                Actor(10, 0x8E2, c), Actor(11, 0x8E2, c), Actor(12, 0x8E2, c)], full: true, party: party),
            Frame(242, .1, [Actor(10, 0x8E2, c, cast: Cast(1343, 6, c)),
                Actor(11, 0x8E2, c, cast: Cast(1222, 6, c)),
                Actor(12, 0x8E2, c, cast: Cast(1223, 6, c))], party: party),
            Status(.2, 101, 333, 6),
            Frame(242, .3, [Actor(101, 0, c + Vector3.UnitX * 2, stackedStatus: 333, statusExpiresAt: Start.AddSeconds(6.2))], party: party),
            EffectEvent(.4, 10, 1343, 1), Cancel(.5, 11, 1222),
            Frame(242, .6, [Actor(11, 0x8E2, c)], party: party),
            Frame(242, .7, [Actor(10, 0x8E2, c), Actor(12, 0x8E2, c),
                Actor(13, 0x8E2, c, cast: Cast(1220, 3, c, target: 101, startedAt: .7))], party: party),
            EffectEvent(.8, 13, 1220, 2, target: 101),
            Frame(242, .9, [Actor(20, 0x1E8728, c + Vector3.UnitX * 2, eventState: 7)], party: party),
            Frame(242, 1, [Actor(100, 0, c + Vector3.UnitX, dead: true)], party: party),
            Director(1.1, 0x40000005), Frame(242, 1.2, [], party: party),
            Director(1.3, 0x40000011),
            Frame(242, 1.4, [Actor(100, 0, c + Vector3.UnitX), Actor(101, 0, c + Vector3.UnitX * 2), Actor(13, 0x8E2, c)], removed: [20], party: party),
            Frame(242, 2, [], party: party),
        ]);
        Assign("masterplayer.raid.coil2");
        controller.Seek(.35);
        Check(previews().Length == 4 && dangers() == 4 && Group("coil/rot/personal").Length == 1, "Ballast and rot snapshot outputs.");
        controller.Seek(.65); Check(previews().Length == 3 && dangers() == 3, "Effect versus one helper finish.");
        controller.Seek(.95);
        Check(Group("coil/gravity/live").Length == 1 && !Group("coil/gravity/event").Any(), "Gravity live EventState7 matching.");
        controller.Seek(1.05); Check(Active() && Group("coil/gravity").Length > 0, "Personal death erased fight.");
        controller.Seek(1.25); Check(!Active() && previews().Length == 0 && dangers() == 0, "Wipe owner leak.");
        controller.Seek(1.5); Check(Active() && !Group("coil/rot").Any(), "Recommence retained rot.");
        controller.Seek(.35); Check(previews().Length == 4 && Group("coil/rot/holder").Length == 1, "Reverse Seek did not reconstruct rot.");
        manager.SetScriptEnabled("masterplayer.raid.coil2", false);
        Check(previews().Length == 0 && dangers() == 0, "Node disabled owner leak.");
        stop();

        c = new(-3, 0, 27);
        load("masterplayer.raid.coil1",
        [
            Header(241), Frame(241, 0, [Actor(1, 0x887, c, combat: true), .. Players(c),
                Actor(10, 0x889, c + Vector3.UnitX * 4, cast: Cast(1220, 5, c + Vector3.UnitX * 4, startedAt: 0))], full: true, party: party),
            Frame(241, 1, [Actor(1, 0x887, c, dead: true), Actor(2, 0x7D7, new(-26, 0, -407), combat: true)], party: party),
            Frame(241, 2, [], party: party),
        ]);
        controller.Seek(.5); Check(Active() && Group("coil/gravity/cast").Length == 1 && dangers() == 1, "T01 ADS.");
        controller.Seek(1.1); Check(Active() && Group("caduceus/platform/outline").Length == 13
            && !Group("coil/gravity").Any(), "T01 ADS to Caduceus handoff.");
        stop();

        object WithHp(object actor, uint hp, int pending = 0)
        {
            var values = JsonSerializer.SerializeToElement(actor).EnumerateObject()
                .ToDictionary(p => p.Name, p => (object)p.Value);
            values["Hp"] = hp;
            values["IncomingEffects"] = new[] { new { Slot = 0, GlobalSequence = 2, TargetIndex = 0,
                TargetConfirmed = false, Knockback = false, HpDelta = pending } };
            return values;
        }
        var bossPosition = new Vector3(-14.988456f, .5f, -387.5f);
        var clonePosition = new Vector3(-46.16537f, .5f, -401);
        var platform = new Vector3(.6f, 4.5f, -374);
        c = new(-30.576914f, .1f, -401);
        load("masterplayer.raid.coil1",
        [
            Header(241), Frame(241, 0, [Actor(1, 0x7D7, bossPosition, combat: true, targetId: 101), .. Players(c),
                Actor(20, 0x1E8729, platform)], full: true, party: party),
            Frame(241, 10, [Actor(20, 0x1E8729, platform, eventState: 2)], party: party),
            EffectEvent(10.1, 1, 1210, 1, target: 100),
            Frame(241, 10.2, [WithHp(Actor(1, 0x7D7, bossPosition, combat: true, targetId: 101), 500),
                WithHp(Actor(2, 0x7D7, clonePosition, combat: true, targetId: 101), 500),
                WithHp(Actor(10, 0x7D8, c, combat: true, targetId: 100), 201, -1)], party: party),
            Frame(241, 10.4, [Actor(21, 0x1E8B20, c, eventState: 7)], party: party),
            Frame(241, 10.6, [Actor(21, 0x1E8B20, c)], party: party),
            Frame(241, 11, [Actor(1, 0x7D7, bossPosition, dead: true)], party: party),
            Frame(241, 12, [], removed: [1], party: party),
            Frame(241, 13, [Actor(2, 0x7D7, clonePosition, targetable: false, dead: true)], party: party),
            Frame(241, 14, [], party: party),
        ]);
        controller.Seek(.1); Assign("masterplayer.raid.coil1");
        controller.Seek(10.15);
        Check(Group("caduceus/platform/outline").Length == 13 && Group("caduceus/platform/personal").Length == 1
            && Group("caduceus/puddle/event").Length == 1, "Caduceus active platform and Regorge.");
        controller.Seek(10.45); Check(Group("caduceus/puddle/event").Length == 1 && !Group("caduceus/puddle/live").Any(),
            "Caduceus retired puddle matched event prediction.");
        controller.Seek(10.65); Check(!Group("caduceus/puddle/event").Any() && Group("caduceus/puddle/live").Length == 1,
            "Caduceus live puddle transition.");
        controller.Seek(12.1); Check(Active() && Group("caduceus/platform/outline").Length == 13, "Clone lost on original destruction.");
        controller.Seek(13.1); Check(!Active() && previews().Length == 0 && dangers() == 0, "Caduceus phase cleanup.");
        controller.Seek(10.45); Check(Active() && Group("caduceus/puddle/event").Length == 1, "Caduceus reverse Seek.");
        var actors = (IEnumerable)Value(Encounter(), "Actors");
        var slime = actors.Cast<object>().Select(a => Value(a, "Value")).Single(a => (ulong)Value(a, "Id") == 10);
        var incoming = ((IEnumerable)Value(slime, "IncomingEffects")).Cast<object>().Single();
        Check((uint?)Value(slime, "MaxHp") == 1000 && (int)Value(incoming, "HpDelta") == -1,
            "Caduceus replay lost maximum or pending HP.");
        manager.SetScriptEnabled("masterplayer.raid.coil1", false);
        Check(previews().Length == 0 && dangers() == 0, "Caduceus disabled owner leak.");
        stop();

        c = Vector3.Zero;
        load("masterplayer.raid.coil4",
        [
            Header(244), Frame(244, 0, [Actor(1, 0x1E86F9, c), .. Players(c)], full: true, party: party),
            Frame(244, .1, [Actor(1, 0x1E86F9, c, targetable: false)], party: party),
            Frame(244, .2, [Actor(10, 0x7E1, c, combat: true)], party: party),
            Frame(244, .3, [Actor(11, 0x7E4, c, combat: true, targetId: 101)], party: party),
            Frame(244, .4, [Actor(12, 0x7E3, c, combat: true, cast: Cast(1237, 5, c, target: 100, startedAt: .4))], party: party),
            Frame(244, .6, [Actor(13, 0x7E2, c, combat: true)], party: party),
            Frame(244, .8, [Actor(14, 0x7E0, c, combat: true)], party: party),
            EffectEvent(.9, 11, 1258, 1),
            Frame(244, 1, [Actor(100, 0, Vector3.UnitX, dead: true)], party: party),
            Frame(244, 1.2, [], removed: [1], party: party),
            Frame(244, 1.4, [Actor(2, 0x1E86FA, c)], party: party), Frame(244, 2, [], party: party),
        ]);
        controller.Seek(.05); Check(!Active(), "Clickable start terminal pulled.");
        controller.Seek(.15); Check(Active() && Stage() == 0, "Unclickable terminal did not pull.");
        controller.Seek(.35); Check(Stage() == 2 && Group("coil/cleave").Length == 1, "Dread appearance/activation.");
        controller.Seek(.45); Check(Stage() == 3 && Group("coil4/rook").Length == 1 && dangers() == 2 && previews().Length == 2,
            "Personal rook and cleave double view output.");
        controller.Seek(.85); Check(Stage() == 5, "Final mixed wave.");
        controller.Seek(1.25); Check(Active() && Stage() == 5, "Death or removed terminal prematurely finished.");
        controller.Seek(1.5); Check(!Active() && previews().Length == 0 && dangers() == 0, "End terminal cleanup.");
        controller.Seek(.45); Check(Stage() == 3 && Group("coil4/rook").Length == 1, "Gauntlet reverse Seek.");
        manager.SetScriptEnabled("masterplayer.raid.coil4", false);
        Check(previews().Length == 0 && dangers() == 0, "Gauntlet disabled cleanup.");
        stop();
        Console.WriteLine("PASS Coil actual DLL T01 ADS and Caduceus platforms, clone death, pending HP, Regorge state7, all seven T02 nodes, real statuses and effects, continuous Ballast, gravity EventState7, eight-slot settings, six-wave T04 and terminal lifecycle, personal death, wipe, Seek and owner cleanup");
    }
}
