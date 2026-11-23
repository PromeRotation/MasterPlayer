using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunAlexandria(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.alexandria"; const uint territory = 1199;
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object Value(object a, string name) => a.GetType().GetProperty(name)!.GetValue(a)!;
        object[] Group(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("alex/" + key)).ToArray();
        bool Nav(object a) => (bool)Value(a, "Navigate");
        string Map(double t, uint index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect",
            type = "PromeRotation.LogSystem.LogSystemMapEffectEvent", data = new { Position = index,
                Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        string IconEvent(double t, ulong target, uint icon) => JsonSerializer.Serialize(new { t, kind = "TargetIcon",
            type = "PromeRotation.LogSystem.LogSystemTargetIconEvent", data = new { SourceId = target, TargetId = target,
                MarkerId = icon, Timestamp = Start.AddSeconds(t) } });
        string TargetCast(double t, ulong source, uint action, double seconds, Vector3 position, ulong target)
            => JsonSerializer.Serialize(new { t, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent", packetFacing = 0,
                data = new { SourceId = source, TargetId = target, ActionId = action, CastTime = seconds,
                    EffectPosition = Pose(position), Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL },
            new { Slot = 2, EntityId = 102UL }, new { Slot = 3, EntityId = 103UL }];
        var c = new Vector3(852, 0, 823);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4173, c, combat: true), Actor(10, 0x233C, c),
                Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 8), Actor(102, 0, c + Vector3.UnitX * 4),
                Actor(103, 0, c - Vector3.UnitX * 4)], full: true, party: party),
            CastEvent(.1, 10, 36379, 6, c),
            Frame(territory, .2, [Actor(10, 0x233C, c, cast: Cast(36379, 6, c))], party: party),
            Frame(territory, 1, [Actor(20, 0x4174, c), Actor(21, 0x4175, c)], party: party),
            IconEvent(2, 101, 344), IconEvent(2, 100, 62),
            Frame(territory, 6.5, [Actor(10, 0x233C, c)], party: party),
            CastEvent(7, 20, 36382, 1, c),
            Frame(territory, 7.1, [Actor(20, 0x4174, c, cast: Cast(36382, 1, c, startedAt: 7))], party: party),
            Frame(territory, 8.5, [Actor(20, 0x4174, c)], party: party),
            EffectEvent(9, 10, 36385, 1, target: 101), EffectEvent(9, 10, 36386, 2, target: 100),
            Map(10, 3, 0x00020001),
        ]);
        controller.Seek(3);
        Check(Group("cast").Length == 1 && Group("queue").Length == 1 && Group("stack").Length > 0, "Alexandria object queue/icons.");
        controller.Seek(6.8); Check(Group("cast").Length == 0 && Group("queue").Count(a => (bool)Value(a, "Display")) == 2, "Alexandria cone finish promotion.");
        controller.Seek(8.6); Check(Group("queue").Length == 1 && Nav(Group("queue")[0]), "Alexandria purge finish FIFO.");
        controller.Seek(10.1); Check(Group("stack").Length == 0 && Group("disinfection").Length == 0, "Alexandria icon resolves.");
        controller.Seek(3); Check(Group("cast").Length == 1 && Group("stack").Length > 0, "Alexandria mixed queue Seek."); stop();

        c = new(-533, 0, -373);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x416A, c, combat: true), Actor(10, 0x233C, c),
                Actor(11, 0x233C, c), Actor(12, 0x233C, c), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 8)], full: true, party: party),
            CastEvent(.1, 10, 39254, 4, c), CastEvent(.1, 11, 39255, 6, c), CastEvent(.1, 12, 39256, 8, c),
            Frame(territory, .2, [Actor(10, 0x233C, c, cast: Cast(39254, 4, c)), Actor(11, 0x233C, c, cast: Cast(39255, 6, c)),
                Actor(12, 0x233C, c, cast: Cast(39256, 8, c))], party: party),
            Frame(territory, 4.5, [Actor(10, 0x233C, c)], party: party),
            Frame(territory, 6.5, [Actor(11, 0x233C, c)], party: party),
            Frame(territory, 8.5, [Actor(12, 0x233C, c)], party: party),
        ]);
        controller.Seek(1); Check(Group("ternary").Length == 1 && previews().Length == 1 && dangers() == 1, "Alexandria ternary initial.");
        controller.Seek(5); Check(Group("ternary").Length == 1 && Value(Group("ternary")[0], "Shape").GetType().Name == "DonutTemplate", "Alexandria second ternary.");
        controller.Seek(7); Check(Group("ternary").Length == 1, "Alexandria third ternary.");
        controller.Seek(9); Check(previews().Length == 0 && dangers() == 0, "Alexandria ternary cleanup.");
        controller.Seek(1); Check(Group("ternary").Length == 1, "Alexandria ternary Seek."); stop();

        c = new(-759, 0, -648);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x41CE, c, combat: true), Actor(10, 0x233C, c), Actor(11, 0x233C, c),
                Actor(12, 0x233C, c), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 8)], full: true, party: party),
            TargetCast(.1, 10, 39243, 10, c, 101), CastEvent(.1, 11, 39616, 5, c),
            TargetCast(.1, 12, 36783, 6, c, 101),
            Frame(territory, .2, [Actor(10, 0x233C, c, cast: Cast(39243, 10, c, target: 101)),
                Actor(11, 0x233C, c, cast: Cast(39616, 5, c)), Actor(12, 0x233C, c, cast: Cast(36783, 6, c, target: 101))], party: party),
            EffectEvent(1, 11, 36778, 1, target: 100), EffectEvent(1, 11, 36786, 2, target: 101),
            Frame(territory, 5.5, [Actor(11, 0x233C, c)], party: party),
            Map(7, 0x2F, 0x00080004), EffectEvent(8, 11, 36780, 3, target: 100),
            Frame(territory, 11, [Actor(10, 0x233C, c), Actor(12, 0x233C, c)], party: party),
        ]);
        controller.Seek(2); Check(Group("spread").Count(a => (bool)Value(a, "Display")) == 1 && !Group("spread").Any(Nav), "Alexandria suppressed spread stays visible.");
        controller.Seek(6); Check(Group("spread").Any(Nav) && Group("salvation").Length > 0, "Alexandria spread resumed and Salvation retained.");
        controller.Seek(7.1); Check(Group("line").Count(a => (bool)Value(a, "Display")) == 1 && Group("salvation").Length == 0, "Alexandria selective map cancellation.");
        controller.Seek(11.1); Check(previews().Length == 0 && dangers() == 0, "Alexandria Eliminator cleanup.");
        controller.Seek(2); Check(!Group("spread").Any(Nav) && Group("line").Count(a => (bool)Value(a, "Display")) == 2, "Alexandria Eliminator Seek."); stop();
        Console.WriteLine("PASS: native Alexandria synthetic replay mixed queue/icons/ternary/spread/salvation/maps/Seek");
    }
}
