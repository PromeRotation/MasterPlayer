using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunMtGulg(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.mt-gulg";
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object a) => (string)a.GetType().GetProperty("Id")!.GetValue(a)!;
        bool Sprint() => (bool)Encounter().GetType().GetProperty("WantsGulgSprint", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Encounter())!;
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        foreach (var (boss, caster, action, center) in new (uint, uint, uint, Vector3)[]
        {
            (0x28F2, 0x28F1, 16813, Vector3.Zero), (0x27CA, 0x233C, 17153, new(188, 0, -170)),
            (0x28F0, 0x233C, 16245, new(0, 0, -200)), (0x28F0, 0x233C, 16245, new(-190, 0, -100)),
            (0x28F3, 0x2947, 16251, new(-240, 0, 176.3f)),
        })
        {
            load(id,
            [
                Header(822), Frame(822, 0, [Actor(1, boss, center, combat: true), Actor(10, caster, center),
                    Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitZ * 8)], full: true, party: party),
                Frame(822, .1, [Actor(10, caster, center, cast: Cast(action, 5, center, .7f))], party: party),
                Frame(822, 5.2, [Actor(10, caster, center)], party: party),
            ]);
            controller.Seek(.2); Check(previews().Length == 1 && dangers() == 1, $"Production MtGulg {boss:X} cast missing.");
            controller.Seek(5.3); Check(previews().Length == 0 && dangers() == 0, "Production MtGulg ordinary cast leaked.");
            controller.Seek(.2); Check(previews().Length == 1, "Production MtGulg ordinary seek failed."); stop();
        }
        var c = new Vector3(-240, 0, -50);
        load(id,
        [
            Header(822), Frame(822, 0, [Actor(1, 0x27CC, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitZ * 8)], full: true, party: party),
            Frame(822, .1, [Actor(1, 0x27CC, c, combat: true, cast: Cast(16989, 5, c))], party: party),
            Frame(822, 5.2, [Actor(1, 0x27CC, c, combat: true)], party: party),
            EffectEvent(5.8, 50, 15623, 1),
            Frame(822, 6, [Actor(10, 0x27CD, c), Actor(11, 0x27CD, c + Vector3.UnitZ * 10),
                Actor(20, 0x1EAACF, c - Vector3.UnitX * 9), Actor(21, 0x1EAACF, c + Vector3.UnitX * 9)], party: party),
            Frame(822, 10, [Actor(10, 0x27CD, c, cast: Cast(15630, 6, c, startedAt: 10))], party: party),
            EffectEvent(13.6, 50, 15633, 2), EffectEvent(13.7, 50, 15632, 3),
            Frame(822, 16.1, [Actor(10, 0x27CD, c)], party: party),
        ]);
        controller.Seek(.2); Check(previews().Length == 5 && dangers() == 5, "Production MtGulg diagonal cells missing.");
        controller.Seek(5.3); Check(previews().Length == 5, "Production MtGulg natural cell cast finish erased forecast.");
        controller.Seek(5.9); Check(previews().Length == 0, "Production MtGulg real cell effect not consumed.");
        controller.Seek(6.1); Check(previews().Length == 4 && Aoes().Any(a => AoeId(a) == "gulg/tower/enter"), "Production MtGulg birth donuts/towers missing.");
        controller.Seek(13.8); Check(previews().Length == 2, "Production MtGulg tower FIFO effects failed.");
        controller.Seek(16.2); Check(previews().Length == 0 && dangers() == 0, "Production MtGulg Contrition all-clear failed.");
        controller.Seek(6.1); Check(previews().Length == 4, "Production MtGulg Whimsy seek failed."); stop();

        c = new(-240, 0, 237);
        foreach (var staggered in new[] { false, true })
        {
            Vector3[] firstPair = staggered ? [new(-227.5f, 0, 253), new(-232.5f, 0, 251.5f)]
                : [new(-250, 0, 253), new(-230, 0, 253)];
            List<string> frames =
            [
                Header(822), Frame(822, 0, [Actor(1, 0x27CE, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitZ * 8)], full: true, party: party),
                Frame(822, .1, [Actor(1, 0x27CE, c, combat: true, cast: Cast(15644, 3, c))], party: party),
                Frame(822, 3.2, [Actor(1, 0x27CE, c, combat: true)], party: party),
                Frame(822, 4, [Actor(50, 0x1EA000, c, eventAnimation: 0x00010002)], party: party),
                Frame(822, 5, [Actor(51, 0x1EA001, c, eventAnimation: 0x00040008)], party: party),
                EffectEvent(6, 1, 15652, 1),
                Frame(822, 6.1, Enumerable.Range(0, 6).Select(i => Actor((ulong)(10 + i), 0x1EAB62,
                    i < 2 ? firstPair[i] : new(-250 + i * 3, 0, 252 - i))), party: party),
            ];
            for (var i = 0; i < 6; ++i) frames.Add(EffectEvent(13.1 + i * .5, 60, 15655, (uint)i));
            frames.Add(Frame(822, 17, [Actor(30, 0x27D0, c), Actor(31, 0x1EAB62, c + Vector3.UnitZ * 12)], party: party));
            frames.Add(Frame(822, 18, [Actor(30, 0x27D0, c, eventAnimation: 0x00040008)], party: party));
            load(id, frames);
            controller.Seek(.2); Check(previews().Length == 1, "Production MtGulg Penance preview missing.");
            controller.Seek(3.3); Check(previews().Length == 1, "Production MtGulg natural Penance finish erased danger.");
            controller.Seek(4.1); Check(previews().Length == 0 && Sprint(), "Production MtGulg shrink/sprint failed.");
            controller.Seek(5.1); Check(!Sprint(), "Production MtGulg restore failed.");
            controller.Seek(6.2); Check(previews().Length == 4 && dangers() == (staggered ? 1 : 2), "Production MtGulg future gold previews incorrectly dangerous.");
            for (var i = 0; i < 6; ++i)
            {
                controller.Seek(13.2 + i * .5);
                Check(previews().Length == Math.Min(4, 5 - i), "Production MtGulg six-line FIFO advancement failed.");
            }
            controller.Seek(17.1); Check(Aoes().Count(a => AoeId(a).StartsWith("gulg/orb/")) == 1, "Production MtGulg orb capsule missing.");
            controller.Seek(18.1); Check(previews().Length == 0 && dangers() == 0, "Production MtGulg orb animation/stale state failed.");
            controller.Seek(6.2); Check(previews().Length == 4, "Production MtGulg final boss seek failed."); stop();
        }
        Console.WriteLine("PASS native MtGulg six modules, both Apathy rooms, cell latch, birth donuts/towers, six gold FIFO patterns, orb capsule, shrink/sprint replay isolation, seek and stop");
    }
}
