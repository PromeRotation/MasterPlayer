using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivaleDraugar(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.carnivale.stage01";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(100, 0, 100);
        object? Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj);
        object Encounter()
        {
            object script = Value(manager.Running[guid], "Script")!;
            return script.GetType().BaseType!.GetField("_encounter", flags)!.GetValue(script)!;
        }
        object State() => Value(Encounter(), "CarnivaleDraugar")!;
        object[] Values(string property) => ((IEnumerable)Value(Encounter(), property)!).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")!).Contains(prefix)).ToArray();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [center])!).Cast<string>().ToArray();
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed, party: [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }]);
        string Cancel(double t, ulong source, uint action) => JsonSerializer.Serialize(new { t, kind = "CancelCast",
            type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
            data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(t) } });
        object[] Initial(bool combat = true) => [Actor(1, 0x2CD2, center, combat: combat), Actor(2, 0x233C, center),
            Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 3)];
        object Player(object[] slots, bool dead = false, float rotation = 0)
        {
            var value = JsonSerializer.SerializeToElement(Actor(100, 0, center, dead: dead, rotation: rotation))
                .EnumerateObject().ToDictionary(p => p.Name, p => (object)p.Value.Clone());
            value["Statuses"] = slots;
            return value;
        }
        object Status(uint id, int slot, double expires, ulong source = 1, ushort param = 0)
            => new { Id = id, SlotIndex = slot, ExpiresAt = Start.AddSeconds(expires), SourceId = source, Param = param };

        load(guid, [Header(796), At(0, Initial(false), true), At(.1, [Actor(1, 0x2CD2, center, combat: true)]),
            At(1, [Actor(1, 0x2CD2, center, dead: true)]), At(2, [])]);
        controller.Seek(.01);
        Check(Value(Encounter(), "Active") == null && dangers() == 0 && Hints().Any(h => h.Contains("Mustard Bomb")), "Draugar preparation.");
        controller.Seek(.5);
        var source = Assembly.Load("MasterPlayer.MechanicRuntime");
        var bounds = source.GetType("BossMod.Global.MaskedCarnivale.Layouts")!.GetField("CircleSmall")!.GetValue(null)!;
        var wdir = source.GetType("BossMod.WDir")!;
        dynamic arena = Value(Encounter(), "Arena")!;
        var count = 0;
        for (var x = 74.13f; x < 126; x += .67f)
            for (var z = 74.17f; z < 126; z += .71f)
            {
                var expected = (bool)bounds.GetType().GetMethod("Contains")!.Invoke(bounds,
                    [Activator.CreateInstance(wdir, [x - 100, z - 100])])!;
                Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, "Draugar source arena.");
                ++count;
            }
        controller.Seek(1.5);
        Check(Value(Encounter(), "Active") == null, "Draugar primary completion.");
        controller.Seek(.5);
        manager.SetMechanismEnabled(guid, "绝妙傀儡师——都灵", false);
        controller.Seek(.6);
        Check(previews().Length == 0 && dangers() == 0, "Draugar disabled owner.");
        manager.SetMechanismEnabled(guid, "绝妙傀儡师——都灵", true);
        stop();
        Console.WriteLine($"PASS native Carnivale Stage28 source arena oracle {count} points default primary lifecycle preparation disabled owner reverse Seek");

        var raw = center + new Vector3(1.12345f, 0, 2.37654f);
        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(3, 0x1EA9FA, raw, dead: true, targetable: false), Actor(4, 0x1EA9FA, raw + Vector3.UnitX * 8)]),
            At(1, [Actor(2, 0x233C, center, cast: Cast(19055, 6, center, startedAt: 1))]),
            EffectEvent(2, 2, 19055, 1), At(10, []), Cancel(11, 2, 19055),
            At(12, [Actor(3, 0x1EA9FA, raw, eventState: 7), Actor(4, 0x1EA9FA, raw, eventState: 7)]), At(13, [])]);
        controller.Seek(.5);
        var poolPreviews = Shown("carnivale/necrobane/display/");
        Vector3 PoolPosition(object preview) => (Vector3)Value(Value(preview, "Geometry")!, "Position")!;
        var firstPool = poolPreviews.SingleOrDefault(p => ((string)Value(p, "Id")!).EndsWith("/display/3"));
        var secondPool = poolPreviews.SingleOrDefault(p => ((string)Value(p, "Id")!).EndsWith("/display/4"));
        Check(dangers() == 2 && poolPreviews.Length == 2 && firstPool != null && secondPool != null
            && PoolPosition(firstPool) == raw && PoolPosition(secondPool) == raw + Vector3.UnitX * 8,
            $"Necrobane raw dead/untargetable view: dangers={dangers()}, time={runtime.CurrentTime}, "
                + string.Join("; ", poolPreviews.Select(p => $"{Value(p, "Id")}={PoolPosition(p)}")));
        controller.Seek(1.5);
        dynamic danger = ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().Single();
        Check(dangers() == 1 && !(bool)danger.Geometry.Contains(raw, 0f)
            && !(bool)danger.Geometry.Contains(raw + Vector3.UnitX * 4, 0f)
            && (bool)danger.Geometry.Contains(center - Vector3.UnitX * 12, 0f)
            && ((IEnumerable)danger.DangerWindows).Cast<dynamic>().Single().DangerFrom == (DateTime)Value(Value(State(), "Inversion")!, "FinishAt")!,
            "Inversion must constrain complement of union at cast clock.");
        Check(Shown("carnivale/necrobane/display/").All(p => (bool)Value(p, "Safe")!), "Inverted green pools.");
        controller.Seek(10.5);
        Check(Value(State(), "Inversion") != null && dangers() == 1, "Effect/timeout ended inversion.");
        controller.Seek(11.5);
        Check(Value(State(), "Inversion") == null && dangers() == 2, "Real Finish normal pools.");
        controller.Seek(12.5);
        Check(dangers() == 0 && Shown("carnivale/necrobane/").Length == 0, "State7 pools.");
        controller.Seek(1.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage28 raw dead pools green inverted view quantized union complement hard danger window real Finish state7 no effect timeout reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Player([Status(1958, 0, 10), Status(1960, 1, 20)])]),
            At(1, [Player([Status(1958, 0, 11), Status(1960, 1, 20)])]),
            At(2, [Player([Status(1958, 0, 12.01), Status(1960, 1, 20), Status(1958, 2, 30, 2)])]),
            At(3, [Player([Status(1960, 1, 20), Status(1958, 2, 30, 2)], dead: true, rotation: MathF.PI / 2)]),
            At(4, [], removed: [100]), At(5, [Actor(100, 0, center)]), At(6, [])]);
        controller.Seek(.5);
        Check(Values("CarnivaleDraugarMovements").Length == 2 && dangers() == 0 && Shown("carnivale/brainstorm/").Length >= 2,
            "March model/native previews.");
        controller.Seek(1.5);
        Check(Values("CarnivaleDraugarMovements").Length == 2, "One second raw clock drift.");
        controller.Seek(2.5);
        Check(Values("CarnivaleDraugarMovements").Length == 4, "Extension/sameID second source slot.");
        controller.Seek(3.5);
        Check(Values("CarnivaleDraugarMovements").Length == 3 && dangers() == 0, "First direction loss/dead slots.");
        controller.Seek(5.5);
        Check(Values("CarnivaleDraugarMovements").Length == 1, "Destroy must emit losses only, preserving unmatched historical Gain.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage28 raw per slot identity source Param expiry threshold chained march dead actor slot losses destroy unmatched historical Gain native previews no nav reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Player([Status(1257, 0, 5), Status(3629, 1, 8), Status(1960, 2, 20)])]),
            At(1, [Player([Status(3629, 1, 8), Status(1960, 2, 20)])]), At(2, [])]);
        controller.Seek(.5);
        Check(Values("CarnivaleDraugarMovements").Length == 2 && dangers() == 0, "Two forced categories/current residual plus pending.");
        controller.Seek(1.5);
        Check(Values("CarnivaleDraugarMovements").Length == 1, "Any forced Lose clears.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage28 forced1257 and3629 last gain residual 6point6 speed any loss clears no wall immunity hold or facing requests reverse Seek");

        foreach (var (action, countExpected, hint) in new[] { (19082u, 0, "GTFO from aoe!"),
            (19059u, 0, "GTFO from aoe!"), (19064u, 0, "GTFO from aoe!"), (19067u, 0, "GTFO from aoe!"),
            (19053u, 0, "Interrupt!"), (19057u, 0, "Summons adds! (Kill with fire!)"),
            (19051u, 0, "Heal to full before cast ends!"), (19084u, 2, "Raidwide"), (19056u, 2, "Raidwide") })
        {
            var hazard = action is 19082 or 19059 or 19064 or 19067;
            load(guid, [Header(796), At(0, Initial(), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center))]),
                EffectEvent(1, 2, action, 1), At(8, []), Cancel(9, 2, action), At(10, [])]);
            controller.Seek(.5);
            Check(dangers() == (hazard ? 1 : 0) && Values("PredictedDamage").Length == countExpected && Hints().Contains(hint),
                $"Stage28 actual action {action} scope.");
            controller.Seek(8.5);
            Check(dangers() == (hazard ? 1 : 0) && Hints().Contains(hint), "Stage28 action effect/timeout.");
            controller.Seek(9.5);
            Check(dangers() == 0 && Values("PredictedDamage").Length == 0 && !Hints().Contains(hint), "Stage28 action real Finish.");
            controller.Seek(.5);
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage28 all nine cast component actions R10 shared R6 native danger pure interrupt heal summon hints raidwide64 raw clocks real Finish reverse Seek");
    }
}
