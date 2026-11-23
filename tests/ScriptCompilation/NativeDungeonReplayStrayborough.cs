using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunStrayborough(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.strayborough"; const uint territory = 1204;
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object Value(object a, string name) => a.GetType().GetProperty(name)!.GetValue(a)!;
        object[] Group(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("stray/" + key)).ToArray();
        string Map(double t, uint index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect",
            type = "PromeRotation.LogSystem.LogSystemMapEffectEvent", data = new { Position = index,
                Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        string IconEvent(double t, ulong target, uint icon) => JsonSerializer.Serialize(new { t, kind = "TargetIcon",
            type = "PromeRotation.LogSystem.LogSystemTargetIconEvent", data = new { SourceId = target, TargetId = target,
                MarkerId = icon, Timestamp = Start.AddSeconds(t) } });
        string TetherEvent(double t, ulong source, ulong target, uint tether) => JsonSerializer.Serialize(new { t, kind = "Tether",
            type = "PromeRotation.LogSystem.LogSystemTetherEvent", data = new { SourceId = source, TargetId = target,
                TetherId = tether, Timestamp = Start.AddSeconds(t) } });
        string TargetCast(double t, ulong source, uint action, double seconds, Vector3 position, ulong target)
            => JsonSerializer.Serialize(new { t, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent", packetFacing = 0,
                data = new { SourceId = source, TargetId = target, ActionId = action, CastTime = seconds,
                    EffectPosition = Pose(position), Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var c = new Vector3(0, 0, 150);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4183, c, combat: true), Actor(10, 0x233C, c),
                Actor(11, 0x233C, c), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 8)], full: true, party: party),
            IconEvent(.1, 100, 197), CastEvent(1, 10, 39686, 2, c),
            Frame(territory, 1.1, [Actor(10, 0x233C, c, cast: Cast(39686, 2, c, startedAt: 1))], party: party),
            Frame(territory, 3.5, [Actor(10, 0x233C, c)], party: party),
            EffectEvent(3.6, 10, 39686, 1, target: 10),
            EffectEvent(5.2, 10, 39687, 2, target: 10),
            EffectEvent(6.8, 10, 39687, 3, target: 10),
            EffectEvent(8.4, 10, 39687, 4, target: 10),
            EffectEvent(10, 10, 39687, 5, target: 10),
            CastEvent(11, 11, 39683, 2, c),
            Frame(territory, 11.1, [Actor(11, 0x233C, c, cast: Cast(39683, 2, c, startedAt: 11))], party: party),
            Frame(territory, 13.5, [Actor(11, 0x233C, c)], party: party),
            EffectEvent(15, 11, 39683, 6, target: 11),
        ]);
        controller.Seek(.5); Check(Group("chase/edge").Length == 1, "Strayborough icon prehint.");
        controller.Seek(2); Check(Group("chaser").Length >= 2 && dangers() > 0 && previews().Length > 0, "Strayborough first chaser.");
        controller.Seek(10.5); Check(Group("chaser").Length == 0, "Strayborough five effects.");
        controller.Seek(14); Check(Group("exa").Length == 5 && dangers() == 5, "Strayborough exaflare finish retained.");
        controller.Seek(15.1); Check(Group("exa").Length == 4, "Strayborough exaflare advance.");
        controller.Seek(14); Check(Group("exa").Length == 5, "Strayborough exaflare Seek."); stop();

        c = new(17, 0, -170);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x41CA, c, combat: true), Actor(10, 0x41D2, c),
                Actor(20, 0x41D5, new(17, 0, -163)), Actor(21, 0x41D5, new(17, 0, -177)), Actor(100, 0, c)], full: true),
            TetherEvent(.1, 20, 1, 276), TetherEvent(.1, 21, 1, 276), Map(.2, 0x23, 0x02000100),
            CastEvent(12, 10, 36720, 3, c),
            Frame(territory, 12.1, [Actor(10, 0x41D2, c, cast: Cast(36720, 3, c, startedAt: 12))]),
            Frame(territory, 15.5, [Actor(10, 0x41D2, c)]),
        ]);
        controller.Seek(1); Check(Group("tea").Length == 2 && dangers() == 2, "Strayborough tether map tea.");
        controller.Seek(14); Check(Group("tea").Length == 2, "Strayborough tea not cast-driven.");
        controller.Seek(16); Check(previews().Length == 0 && dangers() == 0, "Strayborough tea finish.");
        controller.Seek(1); Check(Group("tea").Length == 2, "Strayborough tea Seek."); stop();

        c = new(148, 0, -433);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x421F, c, combat: true), Actor(10, 0x233C, c),
                Actor(20, 0x4221, c - Vector3.UnitZ * 5), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 8)], full: true, party: party),
            Map(.1, 0x0B, 0x00800040), Map(.1, 0x0C, 0x20001),
            TetherEvent(.2, 20, 100, 57), TargetCast(.3, 10, 37146, 8, c, 101),
            Frame(territory, .4, [Actor(10, 0x233C, c, cast: Cast(37146, 8, c, target: 101, startedAt: .3))], party: party),
            EffectEvent(1, 10, 37144, 1, target: 101),
            EffectEvent(6, 10, 37143, 2, target: 101),
            Frame(territory, 9, [Actor(10, 0x233C, c)], party: party),
            Map(10, 0x0B, 0x00080004),
        ]);
        controller.Seek(2); Check(Group("spread").Length == 2 && Group("platform").Length == 4 && Group("stack/join").Length == 1
            && Group("stretch").Length == 1, "Strayborough ghost mechanics display/navigation.");
        controller.Seek(9.5); Check(Group("spread").Length == 0 && Group("stack").Length == 0 && Group("platform/enter").Length == 1, "Strayborough remaining stretch demands transformation.");
        controller.Seek(2); Check(Group("spread").Length == 2 && Group("stack/join").Length == 1, "Strayborough final boss Seek."); stop();
        Console.WriteLine("PASS: native Strayborough three encounters synthetic replay chase/exaflare/tea/ghost/spread/stack/stretch/Seek");
    }
}
