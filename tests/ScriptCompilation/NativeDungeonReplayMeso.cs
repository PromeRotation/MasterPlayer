using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunMeso(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.meso"; const uint territory = 1292;
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object Value(object a, string name) => a.GetType().GetProperty(name)!.GetValue(a)!;
        object[] Group(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("meso/" + key)).ToArray();
        ulong[] Forbidden() => (ulong[])Value(Encounter(), "ForbiddenTargets");
        int Contours()
        {
            var arena = Value(Encounter(), "Arena");
            return ((IEnumerable)arena.GetType().GetMethod("GetContours")!.Invoke(arena, [64])!).Cast<object>().Count();
        }
        string Map(double t, uint index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect",
            type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = index, Param1 = state & 0xFFFF, Param2 = state >> 16, Timestamp = Start.AddSeconds(t) } });
        string Timeline(double t, ulong source) => JsonSerializer.Serialize(new { t, kind = "ActionTimeline",
            type = "PromeRotation.LogSystem.LogSystemPlayActionTimelineEvent", data = new { SourceId = source, TimelineId = 0x11D3, Timestamp = Start.AddSeconds(t) } });
        string Yell(double t, ulong source) => JsonSerializer.Serialize(new { t, kind = "NpcYell",
            type = "PromeRotation.LogSystem.LogSystemNpcYellEvent", data = new { SourceId = source, NpcYellId = 18705, Timestamp = Start.AddSeconds(t) } });
        var c = new Vector3(270, 0, 12);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x488F, c, combat: true), Actor(100, 0, c)], full: true),
            CastEvent(.1, 1, 43798, 5, c), Frame(territory, .2, [Actor(1, 0x488F, c, combat: true, cast: Cast(43798, 5, c))]),
            Map(1, 4, 0x00020001), Map(1.1, 0x0B, 0x00020001), Map(1.2, 0x0C, 0x00020001),
            Map(1.3, 0x0D, 0x00020001), Map(1.4, 0x0E, 0x00020001),
            CastEvent(2, 10, 43806, 5.5, c), Frame(territory, 2.1, [Actor(10, 0x233C, c, cast: Cast(43806, 5.5, c, startedAt: 2))]),
            Frame(territory, 8, [Actor(1, 0x488F, c, combat: true), Actor(10, 0x233C, c)]),
        ]);
        controller.Seek(.5); Check(Group("shrink").Length == 1 && previews().Length == 1, "Meso shrink preview.");
        controller.Seek(1.5); Check(Group("sphere").Length == 4 && Group("sphere").All(a => !(bool)Value(a, "Navigate"))
            && previews().Length == 4 && dangers() == 0, "Meso early display only.");
        controller.Seek(3); Check(dangers() == 4 && previews().Length == 4, "Meso sphere activation.");
        controller.Seek(8.5); Check(dangers() == 0 && previews().Length == 0, "Meso small finish.");
        controller.Seek(3); Check(dangers() == 4, "Meso sphere Seek."); stop();

        c = new(60, 0, -258);
        var party = new object[] { new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL } };
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4891, c, combat: true),
                Actor(2, 0x4890, c + Vector3.UnitX * 10, combat: true), Actor(3, 0x4892, c - Vector3.UnitX * 10, combat: true),
                Actor(4, 0x4893, c + Vector3.UnitZ * 10, combat: true), Actor(100, 0, c), Actor(101, 0, c)], full: true, party: party),
            StatusEvent(.1, 1, 2552, 0x394, true),
            Frame(territory, .2, [Actor(1, 0x4891, c, combat: true, stackedStatus: 2552, statusParam: 0x394)], party: party),
            StatusEvent(.3, 100, 4543, 0, true), Map(.4, 0x19, 0x00020001),
            Frame(territory, .5, [Actor(20, 0x4894, c, rotation: MathF.PI / 2)], party: party),
            JsonSerializer.Serialize(new { t = 2, kind = "ActionEffect", type = "PromeRotation.LogSystem.LogSystemActionEffectEvent",
                data = new { SourceId = 20UL, ActionId = 43594, GlobalSequence = 1, TargetId = 0UL, TargetIndex = 0, SourcePosition = Pose(c), Timestamp = Start.AddSeconds(2) } }),
            StatusEvent(3, 100, 4550, 0, true),
            Frame(territory, 4, [Actor(1, 0x4891, c, dead: true)], party: party),
        ]);
        controller.Seek(1); Check(Forbidden().Order().SequenceEqual(new ulong[] { 2, 3, 4, 20 }) && Contours() == 3,
            $"Meso assigned prison and ACR forbidden: targets={string.Join(",", Forbidden())} contours={Contours()}.");
        Check(Group("hellmaker").Length == 0 && Group("sword").Length == 12 && previews().Length == 12, "Meso swords and personal display.");
        controller.Seek(2.5); Check(Group("sword").Length == 0, "Meso actual targeted packet sword stop.");
        controller.Seek(3.5); Check(Forbidden().Length == 0 && Contours() == 1 && Group("hellmaker").Length == 1, "Meso assignment release.");
        controller.Seek(4.5); Check(Value(Encounter(), "Active") != null, "Meso original primary death ended group.");
        controller.Seek(1); Check(Group("sword").Length == 12 && Contours() == 3, "Meso group Seek."); stop();

        c = Vector3.Zero;
        var lines = new List<string>
        {
            Header(territory), Frame(territory, 0, new[] { Actor(1, 0x48BE, c, combat: true), Actor(100, 0, new(12, 0, 0)) }
                .Concat(Enumerable.Range(20, 7).Select(i => Actor((ulong)i, 0x48C0, new((i - 20) * 4, 0, 0)))).ToArray(), full: true),
        };
        foreach (var n in Enumerable.Range(20, 7)) lines.Add(Timeline(.1, (ulong)n));
        for (var n = 0; n < 8; ++n) lines.Add(Yell(.2 + n * .01, 20));
        lines.Add(CastEvent(1, 10, 43818, 5, c));
        lines.Add(Frame(territory, 1.1, [Actor(10, 0x233C, c, cast: Cast(43818, 5, c, startedAt: 1))]));
        lines.Add(Frame(territory, 6.5, [Actor(10, 0x233C, c)]));
        lines.Add(CastEvent(10, 11, 43811, 1.5, c));
        lines.Add(Frame(territory, 10.1, [Actor(11, 0x233C, c, cast: Cast(43811, 1.5, c, startedAt: 10))]));
        lines.Add(Frame(territory, 12, [Actor(11, 0x233C, c)]));
        load(id, lines);
        controller.Seek(.5); Check(Group("bomb").Length == 8 && dangers() == 8, "Meso yell predictions.");
        controller.Seek(2); Check(Group("bomb").All(a => !(bool)Value(a, "Navigate")) && previews().Length >= 9
            && Group("knockback/center").Length == 1, "Meso Impression risk and display.");
        controller.Seek(7); Check(Group("bomb").All(a => (bool)Value(a, "Navigate")), "Meso post-knockback risk.");
        controller.Seek(12.5); Check(previews().Length == 0 && dangers() == 0, "Meso bombardment finish.");
        controller.Seek(2); Check(Group("bomb").Length == 8 && Group("knockback/avoid").Length == 1, "Meso last boss Seek."); stop();
        Console.WriteLine("PASS: native Meso three encounters synthetic replay shrink/map spheres/personal prison/ACR forbidden/sword TargetIndex/yell bombs/knockback risk/Seek");
    }
}
