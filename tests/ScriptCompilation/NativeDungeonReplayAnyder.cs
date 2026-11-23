using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunAnyder(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.anyder";
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object a) => (string)a.GetType().GetProperty("Id")!.GetValue(a)!;
        object[] Damage() => ((IEnumerable)Encounter().GetType().GetProperty("PredictedDamage")!.GetValue(Encounter())!).Cast<object>().ToArray();
        bool Contains(Vector3 p)
        {
            var arena = Encounter().GetType().GetProperty("Arena")!.GetValue(Encounter())!;
            if (!(bool)arena.GetType().GetMethod("Contains")!.Invoke(arena, [p, .001f])!) return false;
            var flower = Aoes().SingleOrDefault(a => AoeId(a) == "anyder/blossom");
            if (flower == null) return true;
            var template = flower.GetType().GetProperty("Shape")!.GetValue(flower)!;
            var shape = template.GetType().GetMethod("Resolve")!.Invoke(template,
                [flower.GetType().GetProperty("Position")!.GetValue(flower), 0f, null])!;
            return !(bool)shape.GetType().GetMethod("Contains")!.Invoke(shape, [p, .001f])!;
        }
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var c = new Vector3(-305, 0, 211.5f);
        load(id,
        [
            Header(841), Frame(841, 0, [Actor(1, 0x27AB, c, combat: true), Actor(2, 0x27AC, c + Vector3.UnitX * 8, combat: true),
                Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitZ * 8)], full: true, party: party),
            Frame(841, .1, [Actor(2, 0x27AC, c + Vector3.UnitX * 8, combat: true, cast: Cast(15876, 4, c, target: 100))], party: party),
            Frame(841, 4.2, [Actor(1, 0x27AB, c, dead: true), Actor(2, 0x27AC, c + Vector3.UnitX * 8, combat: true)], party: party),
            EffectEvent(7, 2, 15876, 1, target: 100),
            Frame(841, 8, [Actor(2, 0x27AC, c, combat: true, cast: Cast(15877, 4, c, target: 100, startedAt: 8))], party: party),
            Frame(841, 12.1, [Actor(2, 0x27AC, c, combat: true), Actor(20, 0x1E909F, c)], party: party),
            Frame(841, 14, [Actor(20, 0x1E909F, c, eventState: 7)], party: party),
            Frame(841, 15, [Actor(2, 0x27AC, c, dead: true)], party: party),
        ]);
        controller.Seek(4.3); Check(Damage().Length == 1 && (ulong)Encounter().GetType().GetProperty("PrimaryId")!.GetValue(Encounter())! == 2,
            "Production Anyder partner death erased delayed buster.");
        controller.Seek(7.1); Check(Damage().Length == 0, "Production Anyder actual buster effect leaked.");
        controller.Seek(8.1); Check(Aoes().Any(a => AoeId(a) == "party/separate/2/101"), "Production Anyder shark spread missing.");
        controller.Seek(12.2); Check(previews().Length == 1 && dangers() == 1, "Production Anyder water pool missing.");
        controller.Seek(14.1); Check(previews().Length == 0 && dangers() == 0, "Production Anyder pool state seven leaked.");
        controller.Seek(15.1); Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) == null, "Production Anyder both deaths retained session.");
        controller.Seek(4.3); Check(Damage().Length == 1, "Production Anyder shark seek failed."); stop();

        c = new(-224, 0, -38);
        List<string> frames =
        [
            Header(841), Frame(841, 0, [Actor(1, 0x249E, c, combat: true), Actor(20, 0x1EA1A1, c),
                Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitZ * 8)], full: true, party: party),
            Frame(841, .1, [Actor(1, 0x249E, c, combat: true, cast: Cast(15888, 5, c, .7f))], party: party),
            Frame(841, 5.2, [Actor(1, 0x249E, c, combat: true)], party: party),
        ];
        for (var hit = 0; hit < 5; ++hit) frames.Add(EffectEvent(5.4 + hit * 6.1, 1, hit == 0 ? 15888u : 15889u, (uint)hit));
        frames.Add(EffectEvent(32.5, 1, 15890, 10, target: 100));
        frames.Add(EffectEvent(36, 1, 15894, 11, target: 100));
        frames.Add(Frame(841, 37, [Actor(20, 0x1EA1A1, c, eventAnimation: 0x00100020)], party: party));
        frames.Add(Frame(841, 38, [Actor(20, 0x1EA1A1, c, eventAnimation: 0x00010002)], party: party));
        frames.Add(Frame(841, 39, [Actor(20, 0x1EA1A1, c, eventAnimation: 0x00040008)], party: party));
        load(id, frames);
        controller.Seek(.2); Check(previews().Length == 1 && dangers() == 1, "Production Anyder first cross missing/duplicate.");
        controller.Seek(5.3); Check(previews().Length == 1, "Production Anyder natural finish erased first cross.");
        for (var hit = 1; hit < 5; ++hit)
        {
            controller.Seek(5.4 + hit * 6.1 - .1);
            Check(Aoes().Any(a => AoeId(a) == "anyder/cross"), "Production Anyder next cross not predicted.");
            controller.Seek(5.5 + hit * 6.1);
            Check(!Aoes().Any(a => AoeId(a) == "anyder/cross"), "Production Anyder next cross did not advance.");
        }
        controller.Seek(32.4); Check(Aoes().Any(a => AoeId(a) == "anyder/breath"), "Production Anyder five-hit breath missing.");
        controller.Seek(32.6); Check(previews().Length == 0 && Damage().Length == 1, "Production Anyder breath/lash handoff failed.");
        controller.Seek(36.1); Check(Damage().Length == 0, "Production Anyder lash actual effect leaked.");
        controller.Seek(37.1); Check(!Contains(c + new Vector3(5, 0, -5)) && Contains(c + new Vector3(-5, 0, -5)), "Production Anyder yellow blossom differs.");
        controller.Seek(38.1); Check(Contains(c + new Vector3(5, 0, -5)) && !Contains(c + new Vector3(-5, 0, -5)), "Production Anyder blue blossom differs.");
        controller.Seek(39.1); Check(Contains(c + new Vector3(5, 0, -5)) && Contains(c + new Vector3(-5, 0, -5)), "Production Anyder default blossom differs.");
        controller.Seek(.2); Check(previews().Length == 1, "Production Anyder Morbol seek failed."); stop();

        c = new(0, 0, -379);
        frames =
        [
            Header(841), Frame(841, 0, [Actor(1, 0x28DA, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitZ * 8),
                Actor(10, 0x28DB, c + Vector3.UnitX * 8, rotation: MathF.PI / 2), Actor(11, 0x28DB, c - Vector3.UnitX * 8),
                Actor(20, 0x1EAB51, c + Vector3.UnitZ * 10, eventAnimation: 0x00100020)], full: true, party: party),
            Frame(841, .1, [Actor(30, 0x233C, c, cast: Cast(15906, 5, c, .7f))], party: party),
            EffectEvent(2, 10, 15901, 1, target: 100), EffectEvent(3, 11, 17170, 2, target: 1),
            Frame(841, 5.2, [Actor(30, 0x233C, c)], party: party),
        ];
        for (var hit = 0; hit < 8; ++hit) frames.Add(EffectEvent(6 + hit * .25, (ulong)(50 + hit % 4), 15904, (uint)hit));
        frames.Add(Frame(841, 9, [Actor(1, 0x28DA, c, combat: true, cast: Cast(15903, 15, c, startedAt: 9))], party: party));
        frames.Add(Frame(841, 24.1, [Actor(1, 0x28DA, c, combat: true)], party: party));
        frames.Add(Frame(841, 25, [Actor(20, 0x1EAB51, c + Vector3.UnitZ * 10, eventAnimation: 0x00010002)], party: party));
        load(id, frames);
        controller.Seek(.2); Check(previews().Length == 4 && dangers() == 3 && Aoes().Any(a => AoeId(a) == "anyder/collect/enter"),
            "Production Anyder collect union, growth or plumage missing.");
        controller.Seek(3.1); Check(!Aoes().Any(a => AoeId(a).StartsWith("anyder/collect/")), "Production Anyder resolved orbs revived.");
        controller.Seek(8); Check(previews().Length == 1 && dangers() == 1, "Production Anyder expanding orb count differs.");
        var growth = Aoes().Single();
        var shape = growth.GetType().GetProperty("Shape")!.GetValue(growth)!;
        Check((float)shape.GetType().GetProperty("Radius")!.GetValue(shape)! == 3.5f, "Production Anyder growth did not count all eight effects.");
        controller.Seek(9.1); Check(previews().Length == 2, "Production Anyder winding donut missing.");
        controller.Seek(24.2);
        growth = Aoes().Single(); shape = growth.GetType().GetProperty("Shape")!.GetValue(growth)!;
        Check((float)shape.GetType().GetProperty("Radius")!.GetValue(shape)! == 1.5f, "Production Anyder winding finish did not reset growth.");
        controller.Seek(25.1); Check(previews().Length == 0 && dangers() == 0, "Production Anyder growing orb off animation leaked.");
        controller.Seek(.2); Check(previews().Length == 4, "Production Anyder Quetzalcoatl seek failed."); stop();
        Console.WriteLine("PASS native Anyder three bosses, shark survivor and delayed buster, pool, five crosses and breath, lash, both blossom arenas, forced orb collection, growth and winding reset, seek and stop");
    }
}
