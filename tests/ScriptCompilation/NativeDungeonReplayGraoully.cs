using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunGraoully(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic controller = controllerObject;
        dynamic runtime = runtimeObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        var center = new Vector3(-515, 0, -305);
        var player = center + Vector3.UnitZ * 10;
        var buddy = center + Vector3.UnitX * 10;
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        object Buddy(bool prey = false) => new
        {
            Id = 200ul, Oid = 0x10BB, Kind = 2, SubKind = 5, Targetable = true, Hostile = false, IsEnemy = false, Position = new { buddy.X, buddy.Y, buddy.Z },
            Statuses = prey ? new object[] { new { Id = 904u, ExpiresAt = Start.AddSeconds(30) } } : [],
        };
        object Player(ushort? stacks = null) => new
        {
            Id = 100ul, Oid = 0u, Kind = 1, Targetable = true, Hp = 1000, MaxHp = 1000, Position = Pose(player),
            Statuses = stacks is { } value
                ? new object[] { new { Id = 904u, Param = (ushort)0, ExpiresAt = Start.AddSeconds(30) },
                    new { Id = 905u, Param = value, ExpiresAt = Start.AddSeconds(30) } } : [],
        };
        string Status(double t, ulong id, uint status, bool present, ushort param = 0) => JsonSerializer.Serialize(new
        {
            t, kind = present ? "StatusAdd" : "StatusRemove",
            type = present ? "PromeRotation.LogSystem.LogSystemStatusAddEvent" : "PromeRotation.LogSystem.LogSystemStatusRemoveEvent",
            data = new { TargetId = id, StatusId = status, Param = param, Duration = 30f, Timestamp = Start.AddSeconds(t) },
        });
        var records = new List<string>
        {
            Header(477),
            Frame(477, 0, [Actor(1, 0x10B9, center, combat: true), Actor(100, 0, player), Buddy()], full: true, party: party),
            Status(1, 200, 904, true),
            Frame(477, 1.1, [Buddy(true)], party: party),
            Status(2, 100, 904, true), Status(2.1, 200, 904, false), Status(2.2, 100, 905, true, 0x103),
            Frame(477, 2.23, [Player(0x103), Buddy()], party: party),
            Status(3, 100, 905, true, 4), Frame(477, 3.1, [Player(4)], party: party),
            Status(4, 100, 904, false), Frame(477, 4.1, [Player()], party: party),
            Status(5, 100, 904, true), Status(5.01, 100, 905, true, 0x103),
            Status(8, 100, 904, false), Frame(477, 8.1, [Player()], party: party),
        };
        uint[] actions = [3095, 4259, 4260, 4261];
        foreach (var (action, index) in actions.Select((a, i) => (a, i)))
        {
            var time = 10 + index * 5;
            records.Add(Frame(477, time, [Actor(3, action == 3095 || action == 4259 ? 0x10BCu : 0x10BAu, center,
                cast: Cast(action, 2, center, startedAt: time))], party: party));
            records.Add(CastEvent(time, 3, action, 2, center));
            records.Add(Frame(477, time + 3, [Actor(3, 0x10BC, center)], party: party));
        }
        records.Add(Frame(477, 35, [Actor(1, 0x10B9, center, dead: true)], party: party));
        records.Add(Frame(477, 36, [], party: party));
        load("masterplayer.quest.dragoons-fate", records);
        int Goals() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Count();
        controller.Seek(1.25);
        Check(dangers() == 2 && previews().Count(p => (bool)Value(p, "OutlineOnly")) == 1,
            "Graoully Prey did not separate two navigation constraints from its directional outline.");
        controller.Seek(2.25);
        Check(dangers() == 0 && Goals() == 1 && previews().Count(p => (bool)Value(p, "OutlineOnly")) == 1,
            "Graoully handoff lost new prey after old holder's late loss or failed low-byte three-stack goal.");
        controller.Seek(3.25);
        Check(dangers() == 2 && Goals() == 0, "Graoully treated four stacks as exactly three.");
        controller.Seek(4.25);
        Check(dangers() == 0 && Goals() == 0 && previews().Length == 1, "Graoully current prey loss retained output.");
        controller.Seek(6.5);
        Check(dangers() == 0 && Goals() == 1, "Graoully expired status events by re-reading the same stale world frame.");
        foreach (var index in Enumerable.Range(0, 4))
        {
            controller.Seek(10.5 + index * 5);
            Check(dangers() == 1 && previews().Length == 2, $"Graoully AOE {actions[index]} navigation or dual-view preview missing.");
            controller.Seek(13.5 + index * 5);
            Check(dangers() == 0, "Graoully observed AOE end retained danger.");
        }
        controller.Seek(35.5);
        Check(previews().Length == 0 && dangers() == 0, "Graoully death retained output.");
        controller.Seek(2.25);
        Check(Goals() == 1 && dangers() == 0, "Graoully reverse Seek failed to rebuild prey handoff.");
        stop();
        Check(Goals() == 0 && dangers() == 0 && previews().Length == 0, "Graoully stop leaked owner output.");
        Console.WriteLine("PASS production Graoully: four AOE rules, exact quadrilateral, current prey and late old-holder loss, three low-byte ice stacks, partner soft goal, dual-view outline, observed finishes, Seek and cleanup");
    }
}
