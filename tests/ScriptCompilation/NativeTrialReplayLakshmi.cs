using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunLakshmi(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object Value(object a, string name) => a.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(a)!;
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        var party = new object[] { new { Slot = 0, Role = "Tank", EntityId = 100UL }, new { Slot = 1, Role = "Tank", EntityId = 101UL } };
        string DutyFrame(double t, object[] actors, int? charges, bool full = false, ulong[]? removed = null, ulong owner = 100)
        {
            var frame = JsonNode.Parse(Frame(719, t, actors, full, removed, party))!;
            frame["data"]!["DutyActionPlayerId"] = owner;
            frame["data"]!["DutyActions"] = charges.HasValue
                ? JsonSerializer.SerializeToNode(new[] { new { Slot = 0, ActionId = 9345, CurrentCharges = charges.Value, MaxCharges = 1 } }) : null;
            return frame.ToJsonString();
        }
        string IconEvent(double t, ulong source, ulong target, uint icon) => JsonSerializer.Serialize(new { t, kind = "TargetIcon",
            type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = source, TargetId = target, MarkerId = icon, Timestamp = Start.AddSeconds(t) } });
        const string id = "masterplayer.trial.lakshmi";
        load(id,
        [
            Header(719), DutyFrame(0, [Actor(1, 0x1E20, Vector3.Zero, combat: true),
                Actor(100, 0, Vector3.UnitZ * 5), Actor(101, 0, Vector3.UnitX * 10),
                Actor(10, 0x1E22, Vector3.Zero), Actor(11, 0x1E22, Vector3.Zero),
                Actor(20, 0x1E21, Vector3.UnitX * 12, rotation: MathF.PI / 2)], null, full: true),
            DutyFrame(.2, [], 0), DutyFrame(.4, [], 1),
            IconEvent(.5, 100, 101, 14), IconEvent(.6, 101, 100, 107),
            DutyFrame(1, [Actor(1, 0x1E20, Vector3.Zero, targetable: false,
                cast: Cast(9349, 4, Vector3.Zero, startedAt: 1)),
                Actor(10, 0x1E22, Vector3.Zero, cast: Cast(9613, 4, Vector3.Zero, startedAt: 1)),
                Actor(11, 0x1E22, Vector3.Zero, cast: Cast(9361, 4, Vector3.Zero, target: 100, startedAt: 1)),
                Actor(21, 0x1EA76C, Vector3.UnitX * -10, dead: true, eventState: 7)], 0),
            DutyFrame(2, [Actor(100, 0, Vector3.UnitZ * 5, stackedStatus: 1290)], 0),
            DutyFrame(3, [Actor(100, 0, Vector3.UnitZ * 5)], 0),
            EffectEvent(3.5, 1, 9026, 1), EffectEvent(4, 1, 9377, 2),
            EffectEvent(4.5, 1, 9364, 3),
            DutyFrame(5.5, [Actor(1, 0x1E20, Vector3.Zero), Actor(10, 0x1E22, Vector3.Zero),
                Actor(11, 0x1E22, Vector3.Zero)], 0, removed: [21]),
            DutyFrame(6, [], 0, owner: 101),
            DutyFrame(7, [Actor(1, 0x1E20, Vector3.Zero, dead: true)], 0),
            DutyFrame(8, [], 0),
        ]);
        controller.Seek(.1);
        Check(Group("lakshmi/orb").Length == 1, "Lakshmi unknown charge guessed zero.");
        controller.Seek(.3); Check(Group("lakshmi/orb/20/goal").Length == 1, "Lakshmi recorded zero charge lost pickup.");
        controller.Seek(.45); Check(!Group("lakshmi/orb/20/goal").Any(), "Lakshmi stored charge not restored.");
        controller.Seek(1.5);
        Check(Group("lakshmi/bait").Count(a => (bool)Value(a, "Display")) == 2
            && Group("lakshmi/knockback").Length == 2 && Group("lakshmi/gaze").Length == 3
            && Group("lakshmi/stack").Length > 0 && ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count() == 1,
            "Lakshmi hidden boss, independent baits, gaze, KB, stack or obstacle lost.");
        Check((DateTime)Value(Group("lakshmi/bait/1/avoid").Single(), "Activation") == Start.AddSeconds(.5).AddSeconds((double)4.7f),
            "Lakshmi float Path clock changed.");
        controller.Seek(2.5); Check(!Group("lakshmi/orb/20/goal").Any(), "Lakshmi shield status did not suppress pickup.");
        controller.Seek(3.2); Check(Group("lakshmi/orb/20/goal").Any(), "Lakshmi lost shield did not resume pickup.");
        controller.Seek(3.7);
        var executor = Script().GetType().BaseType!.GetField("lakshmiVril", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        Check((int)Value(executor, "Attempts") == 0, "Lakshmi replay executed duty action.");
        controller.Seek(4.2); Check(!Group("lakshmi/bait/1").Any() && Group("lakshmi/bait/2").Any(), "Lakshmi Path effect cleared cross.");
        controller.Seek(4.7); Check(!Group("lakshmi/bait").Any() && Group("lakshmi/stack").Any(), "Lakshmi bait effect cleared stack.");
        controller.Seek(5.7);
        Check(!Group("lakshmi/knockback").Any() && !Group("lakshmi/gaze").Any() && !Group("lakshmi/stack").Any()
            && ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count() == 0, "Lakshmi true cast/zone cleanup failed.");
        controller.Seek(6.2); Check(!Group("lakshmi/orb/20/goal").Any(), "Lakshmi charged owner leaked to another viewpoint.");
        controller.Seek(7.5); Check(previews().Length == 0 && dangers() == 0, "Lakshmi death leaked outputs.");
        controller.Seek(1.5); Check(Group("lakshmi/bait").Any() && Group("lakshmi/gaze").Length == 3, "Lakshmi reverse Seek lost state.");
        manager.SetScriptEnabled(id, false);
        Check(previews().Length == 0 && dangers() == 0, "Lakshmi disabled script leaked."); stop();
        Console.WriteLine("PASS Lakshmi actual DLL replay duty charges, ownership, status, independent icon clocks/effects, gaze, KB, stack, zone, Seek and execution isolation");
    }
}
