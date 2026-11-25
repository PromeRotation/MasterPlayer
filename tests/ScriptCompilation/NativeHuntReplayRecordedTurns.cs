using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntRecordedTurns(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic controller = controllerObject;
        dynamic runtime = runtimeObject;
        dynamic manager = managerObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object[] Areas(string group) => previews().Where(p => ((string)Value(p, "Id")).Contains("/" + group + "/"))
            .OrderBy(p => int.Parse(((string)Value(p, "Id")).Split('/')[^1])).ToArray();
        DateTime[] Activations() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>()
            .Select(a => (DateTime)Value(((IEnumerable)Value(a, "DangerWindows")).Cast<object>().Single(), "DangerFrom")).Order().ToArray();
        const uint territory = 1187;
        var origin = new Vector3(10, 0, 20);
        var moved = new Vector3(80, 0, 90);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];

        foreach (var omen in Enumerable.Range(38626, 6))
        foreach (var combo in Enumerable.Range(38632, 4))
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x43DB, origin, combat: true), Actor(100, 0, origin), Actor(101, 0, moved)], full: true, party: party),
                Frame(territory, .1, [Actor(1, 0x43DB, origin, combat: true, cast: Cast((uint)omen, 5, origin))], party: party),
                CastEvent(.1, 1, (uint)omen, 5, origin),
                Frame(territory, 6, [Actor(1, 0x43DB, origin, combat: true, cast: Cast((uint)combo, 5, origin, startedAt: 6))], party: party),
                CastEvent(6, 1, (uint)combo, 5, origin), CastEvent(6, 1, (uint)combo, 5, origin),
                Frame(territory, 12, [Actor(1, 0x43DB, moved, combat: true, rotation: 1)], party: party),
                EffectEvent(12.1, 1, 38636, 10),
                Frame(territory, 20, [Actor(1, 0x43DB, moved, combat: true, cast: Cast(38636, .8, moved, startedAt: 20))], party: party),
                Frame(territory, 21, [Actor(1, 0x43DB, moved, combat: true)], party: party),
                Frame(territory, 23, [Actor(1, 0x43DB, moved, combat: true, cast: Cast(38641, .8, moved, startedAt: 23))], party: party),
                Frame(territory, 24, [Actor(1, 0x43DB, moved, combat: true)], party: party),
                Frame(territory, 25, [Actor(1, 0x43DB, moved, combat: true, cast: Cast(39878, 5, moved, startedAt: 25))], party: party),
                Frame(territory, 31, [Actor(1, 0x43DB, moved, combat: true)], party: party),
                Frame(territory, 32, [], full: true, party: party)
            ]);
            controller.Seek(1d);
            Check(dangers() == 0 && Areas("whirling-omen").Length == 0, "Omen created a guessed cone during recording.");
            controller.Seek(7d);
            var areas = Areas("whirling-omen");
            Check(dangers() == 2 && areas.Length == 2, "Omen duplicate packets consumed history or lost two-stage output.");
            var activations = Activations();
            Check(Math.Abs((activations[0] - Start.AddSeconds(12.7)).TotalSeconds) < .00001
                && Math.Abs((activations[1] - Start.AddSeconds(15.1)).TotalSeconds) < .00001, "Production Omen delay differs.");
            Check((Vector4)Value(areas[0], "Color") == new Vector4(1, 1, 0, 1), "Omen first preview lost Danger color.");
            float offset = omen is 38627 or 38630 or 38631 ? -MathF.PI / 2 : MathF.PI / 2;
            var angles = combo switch
            {
                38632 => new[] { 0f, offset + MathF.PI },
                38633 => new[] { MathF.PI, offset },
                38634 => new[] { offset, offset + MathF.PI },
                _ => new[] { offset + MathF.PI, offset }
            };
            for (var i = 0; i < 2; ++i)
            {
                dynamic shape = Value(areas[i], "Geometry");
                var inside = origin + new Vector3(MathF.Sin(angles[i]), 0, MathF.Cos(angles[i])) * 39.9f;
                var outsideAngle = angles[i] + 75.1f * MathF.PI / 180;
                var outside = origin + new Vector3(MathF.Sin(outsideAngle), 0, MathF.Cos(outsideAngle)) * 20;
                Check((bool)shape.Contains(inside, 0f) && !(bool)shape.Contains(outside, 0f), "Omen angle/shape matrix differs in DLL.");
            }
            controller.Seek(19d);
            Check(dangers() == 2 && Areas("whirling-omen").Length == 2, "Effect, boss movement or predicted time consumed Omen.");
            controller.Seek(21.1);
            Check(dangers() == 1 && Areas("whirling-omen").Length == 1, "Omen real cast ending failed to pop one item.");
            controller.Seek(24.1);
            Check(dangers() == 0 && Areas("whirling-omen").Length == 0, "Omen last finish leaked.");
            controller.Seek(26d);
            Check(dangers() == 0 && ((string)Value(Script(), "MechanicHintsText")).Contains("Raidwide, no turn buffs this time!")
                && ((string)Value(Script(), "PredictedDamageText")).Contains("Raidwide"), "Omen pure Raidwide lost hint or damage prediction.");
            controller.Seek(7d);
            Check(dangers() == 2 && Areas("whirling-omen").Length == 2, "Omen reverse Seek lost recorded turns.");
            stop();
        }

        foreach (var count in Enumerable.Range(0, 5))
        {
            var lines = new List<string>
            {
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x4232, origin, combat: true), Actor(100, 0, origin)], full: true)
            };
            for (var i = 0; i < count; ++i)
            {
                var action = i == 0 ? 37164u : i % 2 == 0 ? 37166u : 37167u;
                lines.Add(Frame(territory, .1 + i * 4, [Actor(1, 0x4232, origin, combat: true,
                    cast: Cast(action, 3, origin, startedAt: .1 + i * 4))]));
                lines.Add(EffectEvent(3.5 + i * 4, 1, action, (uint)(i + 1)));
                lines.Add(EffectEvent(3.5 + i * 4, 1, action, (uint)(i + 1)));
            }
            lines.Add(Frame(territory, 20, [Actor(1, 0x4232, origin, combat: true, cast: Cast(37170, 5, origin, startedAt: 20))]));
            lines.Add(CastEvent(20, 1, 37170, 5, origin));
            lines.Add(Frame(territory, 26, [Actor(1, 0x4232, moved, combat: true, rotation: 2)]));
            for (var i = 0; i < count; ++i)
            {
                lines.Add(EffectEvent(30 + i * 2, 1, i % 2 == 0 ? 37171u : 37172u, (uint)(100 + i)));
                lines.Add(EffectEvent(30 + i * 2, 1, i % 2 == 0 ? 37171u : 37172u, (uint)(100 + i)));
            }
            lines.Add(Frame(territory, 40, [Actor(1, 0x4232, origin, combat: true, cast: Cast(37372, 4, origin, startedAt: 40))]));
            lines.Add(Frame(territory, 45, [Actor(1, 0x4232, origin, combat: true)]));
            lines.Add(Frame(territory, 46, [Actor(1, 0x4232, origin, combat: true, dead: true)]));
            load("masterplayer.hunts.simple", lines);
            controller.Seek(21d);
            Check(Areas("wingblade").Length == Math.Min(2, count) && dangers() == (count == 0 ? 0 : 1),
                "Wingblade partial history or opposite future navigation differs.");
            if (count != 0)
            {
                dynamic shape = Value(Areas("wingblade")[0], "Geometry");
                Check((bool)shape.Contains(origin + new Vector3(-1, 0, 20), 0f)
                    && !(bool)shape.Contains(origin + new Vector3(1, 0, 20), 0f), "Wingblade used a 90-degree total angle.");
                Check(Math.Abs((Activations().Single() - Start.AddSeconds(25.7)).TotalSeconds) < .00001,
                    "Wingblade first repeat activation differs.");
            }
            controller.Seek(29d);
            Check(Areas("wingblade").Length == Math.Min(2, count), "Reflex finish or timeout consumed repeat history.");
            for (var i = 0; i < count; ++i)
            {
                controller.Seek(30.1 + i * 2);
                Check(Areas("wingblade").Length == Math.Min(2, count - i - 1)
                    && dangers() == (i + 1 == count ? 0 : 1), "Wingblade duplicate effect or promoted Risky differs.");
            }
            controller.Seek(41d);
            Check(dangers() == 1, "LaughingLeap missing native danger.");
            dynamic rectangle = Value(previews().Single(p => ((string)Value(p, "Id")).EndsWith("hunt/1")), "Geometry");
            Check((bool)rectangle.Contains(origin + new Vector3(2.49f, 0, 14.9f), 0f)
                && !(bool)rectangle.Contains(origin + new Vector3(2.51f, 0, 14.9f), 0f), "LaughingLeap full width changed.");
            controller.Seek(45.1);
            Check(dangers() == 0 && Areas("wingblade").Length == 0, "Leap real finish leaked.");
            controller.Seek(46.1);
            Check(previews().Length == 0, "Wingblade boss death retained output.");
            controller.Seek(21d);
            Check(Areas("wingblade").Length == Math.Min(2, count), "Wingblade Seek failed to rebuild history.");
            stop();
        }
        Console.WriteLine("PASS HuntRecordedTurns actual DLL: all 24 Omen matrices, float delays, finish-only FIFO, pure Raidwide; Wingblade 0–4 recorded sides, two previews, persistent risk, effect dedup, exact cones/Leap, Seek and cleanup");
    }
}
