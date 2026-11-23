using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunClyteum(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.clyteum"; const uint territory = 1345;
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object Value(object a, string name) => a.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(a)!;
        bool Flag(string name) => (bool)Value(Encounter(), name);
        object[] Group(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("cly/" + key)).ToArray();
        void Isolated()
        {
            var hints = Script().GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
            var facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!,
                "Clyteum replay issued host control.");
        }
        var party = new object[] { new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL } };
        var p = new Vector3(-615, 0, 575);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4C2C, p, combat: true), Actor(100, 0, p), Actor(101, 0, p + Vector3.UnitX * 5)], full: true, party: party),
            JsonSerializer.Serialize(new { t = .1, kind = "StatusAdd", type = "PromeRotation.LogSystem.LogSystemStatusAddEvent",
                data = new { TargetId = 100, StatusId = 5191, DurationMilliseconds = 5000, Timestamp = Start.AddSeconds(.1) } }),
            Frame(territory, .2, [Actor(100, 0, p, stackedStatus: 5191, statusExpiresAt: Start.AddSeconds(5.1))], party: party),
            CastEvent(.3, 10, 50177, 8.5, p), CastEvent(.4, 11, 50178, 8.5, p),
            Frame(territory, .5, [Actor(10, 0x233C, p, cast: Cast(50177, 8.5, p, startedAt: .3)),
                Actor(11, 0x233C, p, cast: Cast(50178, 8.5, p, startedAt: .4))], party: party),
            Frame(territory, 5, [Actor(30, 0x4C2D, p, renderFlags: 0)], party: party),
            Frame(territory, 6, [Actor(100, 0, p + Vector3.UnitX * 21)], party: party),
            Frame(territory, 7, [Actor(30, 0x4C2D, p, renderFlags: 16384)], party: party),
            StatusEvent(7.1, 100, 5191, 0, false),
            Frame(territory, 10, [Actor(10, 0x233C, p), Actor(11, 0x233C, p)], party: party),
        ]);
        controller.Seek(1); Check(!Flag("RequireAllStop") && previews().Length == 2 && dangers() == 2, "Clyteum early status/beam casts.");
        controller.Seek(4.2); Check(Flag("RequireAllStop"), "Clyteum status threshold."); Isolated();
        controller.Seek(5.5); Check(Flag("RequireAllStop") && Group("beam").Length == 1, "Clyteum scanner state.");
        controller.Seek(6.5); Check(!Flag("RequireAllStop"), "Clyteum scanner outside override.");
        controller.Seek(10.5); Check(previews().Length == 0 && dangers() == 0 && !Flag("RequireAllStop"), "Clyteum first boss cleanup.");
        controller.Seek(5.5); Check(Flag("RequireAllStop") && Group("beam").Length == 1, "Clyteum scanner Seek."); stop();

        p = new(660, 0, -141);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4C3F, p, combat: true), Actor(100, 0, p + Vector3.UnitX * 10),
                Actor(101, 0, p)], full: true, party: party),
            CastEvent(.1, 10, 48882, 9, p + Vector3.UnitX * 5), CastEvent(.2, 11, 48882, 6, p - Vector3.UnitX * 5),
            CastEvent(.3, 12, 48878, 5, p),
            Frame(territory, .4, [Actor(10, 0x233C, p, cast: Cast(48882, 9, p + Vector3.UnitX * 5)),
                Actor(11, 0x233C, p, cast: Cast(48882, 6, p - Vector3.UnitX * 5, startedAt: .2)),
                Actor(12, 0x233C, p, cast: Cast(48878, 5, p, startedAt: .3))], party: party),
            JsonSerializer.Serialize(new { t = 1, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                data = new { SourceId = 100, TargetId = 100, MarkerId = 558, Timestamp = Start.AddSeconds(1) } }),
            Frame(territory, 5.7, [Actor(12, 0x233C, p)], party: party),
            EffectEvent(6, 1, 48885, 1, target: 100),
            Frame(territory, 6.7, [Actor(11, 0x233C, p)], party: party),
            Frame(territory, 10, [Actor(10, 0x233C, p)], party: party),
        ]);
        controller.Seek(2);
        Check(Group("tower").Length == 2 && Group("kb/avoid").Length == 1 && Group("spread").Any(a => (bool)Value(a, "Display")), "Clyteum Chort combination.");
        Check((Vector3)Value(Group("tower").Single(a => (bool)Value(a, "Display")), "Position") == p - Vector3.UnitX * 5, "Clyteum next tower.");
        controller.Seek(6.8); Check(Group("kb").Length == 0 && Group("spread").Length == 0
            && (Vector3)Value(Group("tower").Single(a => (bool)Value(a, "Display")), "Position") == p + Vector3.UnitX * 5, "Clyteum tower advance.");
        controller.Seek(10.5); Check(previews().Length == 0 && dangers() == 0, "Clyteum Chort clear.");
        controller.Seek(2); Check(Group("kb/avoid").Length == 1, "Clyteum Chort Seek."); stop();

        p = new(760, 0, -803);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4C28, p, combat: true), Actor(100, 0, p), Actor(101, 0, p + Vector3.UnitX * 5)], full: true, party: party),
            CastEvent(.1, 1, 48931, 5, p),
            Frame(territory, .2, [Actor(1, 0x4C28, p, combat: true, cast: Cast(48931, 5, p))], party: party),
            CastEvent(.3, 10, 48943, 14.5, p), CastEvent(.4, 11, 48940, 14.5, p + Vector3.UnitX * 8),
            Frame(territory, .5, [Actor(10, 0x233C, p, cast: Cast(48943, 14.5, p, startedAt: .3)),
                Actor(11, 0x233C, p, cast: Cast(48940, 14.5, p + Vector3.UnitX * 8, startedAt: .4))], party: party),
            Frame(territory, 4.5, [Actor(100, 0, p + Vector3.UnitX * 2)], party: party),
            Frame(territory, 5.5, [Actor(1, 0x4C28, p, combat: true)], party: party),
            Frame(territory, 16, [Actor(10, 0x233C, p), Actor(11, 0x233C, p)], party: party),
        ]);
        controller.Seek(1); Check(!Flag("RequireClyteumMovement") && Group("simple").Length == 2 && dangers() == 2, "Clyteum long cast immediate risk.");
        controller.Seek(4.2); Check(Flag("RequireClyteumMovement") && Group("move").Length == 1, "Clyteum move threshold."); Isolated();
        var anchor = (Vector3)Value(Group("move")[0], "Position");
        controller.Seek(4.6); Check((Vector3)Value(Group("move")[0], "Position") != anchor, "Clyteum rolling move.");
        controller.Seek(5.7); Check(!Flag("RequireClyteumMovement") && Group("move").Length == 0, "Clyteum move release.");
        controller.Seek(16.5); Check(previews().Length == 0 && dangers() == 0, "Clyteum Malphas clear.");
        controller.Seek(4.2); Check(Flag("RequireClyteumMovement") && Group("simple").Length == 2, "Clyteum Malphas Seek."); stop();
        Console.WriteLine("PASS: Clyteum three encounters actual DLL replay scanner/status stop, sequential towers, icon spreads, knockback, StringUp movement, long casts and Seek isolation");
    }
}
