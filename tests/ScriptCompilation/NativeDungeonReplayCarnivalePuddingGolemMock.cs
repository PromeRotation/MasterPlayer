using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivalePuddingGolemMock(object managerObject, object runtimeObject, object controllerObject,
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
            Check(manager.Running.ContainsKey(guid), $"Pudding/Golem/Mock stopped: {manager.ErrorMessage}");
            object script = Value(manager.Running[guid], "Script")!;
            return script.GetType().BaseType!.GetField("_encounter", flags)!.GetValue(script)!;
        }
        object[] Values(string property) => ((IEnumerable)Value(Encounter(), property)!).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")!).Contains(prefix)).ToArray();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [center])!).Cast<string>().ToArray();
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed, party: [new { Slot = 0, EntityId = 100ul }]);
        string Cancel(double t, ulong source, uint action) => JsonSerializer.Serialize(new { t, kind = "CancelCast",
            type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
            data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(t) } });
        object[] Initial(uint oid, bool combat = true) => [Actor(1, oid, center, combat: combat, objectIndex: 2, isEnemy: true),
            Actor(2, 0x233C, center), Actor(100, 0, center + Vector3.UnitZ * 3)];
        object WithHp(object actor, uint hp)
        {
            var properties = JsonSerializer.SerializeToElement(actor).EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
            properties["Hp"] = JsonSerializer.SerializeToElement(hp);
            return properties;
        }

        foreach (var (oid, name, layout) in new[] { (0x25C0u, "布丁拼盘·第一幕", "CircleBig"),
            (0x25C1u, "布丁拼盘·第二幕", "CircleBig"), (0x25D4u, "最初的岩壁——希帕克纳", "CircleBig"),
            (0x242Du, "七彩甜点——棉花软糖", "CircleSmall") })
        {
            load(guid, [Header(796), At(0, Initial(oid, false), true), At(.1, [Actor(1, oid, center, combat: true)]),
                At(1, [Actor(1, oid, center, dead: true)]), At(2, [])]);
            controller.Seek(.01);
            Check(Value(Encounter(), "Active") == null && Shown("carnivale/precombat/arena").Length == 1
                && (Value(Encounter(), "CarnivalePrecombatHint") != null) == (oid != 0x25C1), "Constructor hints.");
            controller.Seek(.5);
            var source = Assembly.Load("MasterPlayer.MechanicRuntime");
            var bounds = source.GetType("BossMod.Global.MaskedCarnivale.Layouts")!.GetField(layout)!.GetValue(null)!;
            var wdir = source.GetType("BossMod.WDir")!;
            dynamic arena = Value(Encounter(), "Arena")!;
            var count = 0;
            for (var x = 74.13f; x < 126; x += .67f)
                for (var z = 74.17f; z < 126; z += .71f)
                {
                    var expected = (bool)bounds.GetType().GetMethod("Contains")!.Invoke(bounds,
                        [Activator.CreateInstance(wdir, [x - 100, z - 100])])!;
                    Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, "Pudding/Golem/Mock source arena.");
                    ++count;
                }
            controller.Seek(1.5);
            Check(Value(Encounter(), "Active") == null, "No living group members remains.");
            controller.Seek(.5);
            manager.SetMechanismEnabled(guid, name, false);
            controller.Seek(.6);
            Check(previews().Length == 0 && dangers() == 0, "Disabled owner.");
            manager.SetMechanismEnabled(guid, name, true);
            stop();
            Console.WriteLine($"PASS native Carnivale Pudding Golem Mock {oid:X} exact source {layout} {count} points preparation scope lifecycle reverse Seek disabled owner");
        }

        foreach (var (oid, adds) in new[] { (0x25C0u, new uint[] { 0x25C2, 0x25C4 }), (0x25C1u, new uint[] { 0x25C5, 0x25C3 }) })
        {
            load(guid, [Header(796), At(0, Initial(oid, false), true),
                At(.1, adds.Select((add, i) => Actor((ulong)(10 + i), add, center, combat: i == 0)).ToArray()),
                At(1, [Actor(1, oid, center, dead: true)]),
                At(2, adds.Select((add, i) => Actor((ulong)(10 + i), add, center, dead: true)).ToArray()), At(3, [])]);
            controller.Seek(.5);
            Check(Value(Encounter(), "Active") != null && Shown("carnivale/enemy/").Length == 3, "Any add pulls group.");
            controller.Seek(1.5);
            Check(Value(Encounter(), "Active") != null, "Primary death does not complete group.");
            controller.Seek(2.5);
            Check(Value(Encounter(), "Active") == null, "All group Dead.");
            stop();
        }
        Console.WriteLine("PASS native Carnivale Pudding both acts any trash pull primary death retains phase all three Dead complete all enemy markers");

        foreach (var (oid, action) in new[] { (0x25C0u, 14265u), (0x25C1u, 14265u), (0x25D4u, 14365u), (0x242Du, 14265u) })
        {
            load(guid, [Header(796), At(0, Initial(oid), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center))]), EffectEvent(1, 2, action, 1),
                At(8, []), Cancel(9, 2, action), At(10, [])]);
            controller.Seek(8.5);
            Check(Values("CarnivaleInterrupts").Length == 1 && Hints().Contains("Interrupt!") && dangers() == 0
                && Values("PredictedDamage").Length == 0, "Interrupt intent only persists.");
            controller.Seek(9.5);
            Check(Values("CarnivaleInterrupts").Length == 0, "Interrupt true Finish.");
            controller.Seek(.5);
            stop();
        }
        Console.WriteLine("PASS native Carnivale Pudding GoldenTongue and Golem Obliterate pure interrupt intents known source effect timeout retained real cancel no fabricated raidwide");

        load(guid, [Header(796), At(0, Initial(0x25C0).Append(Actor(3, 0x25C1, center)).ToArray(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(14265, 4, center))]),
            At(1, [Actor(3, 0x25C1, center, combat: true)]),
            At(2, [Actor(1, 0x25C0, center, dead: true)]), Cancel(3, 2, 14265), At(4, [])]);
        controller.Seek(2.5);
        Check((uint)Value(Value(Encounter(), "Active")!, "BossOid")! == 0x25C1
            && Values("CarnivaleInterrupts").Length == 1 && Hints().Contains("Interrupt!"),
            "Later Stage02 owner missed unchanged raw during activation.");
        controller.Seek(3.5);
        Check(Values("CarnivaleInterrupts").Length == 0, "Later owner interrupt Finish.");
        stop();
        Console.WriteLine("PASS native Carnivale Pudding later owner activation unchanged raw current interrupt real Finish");

        foreach (var (oid, action) in new[] { (0x25C0u, 14265u), (0x25D4u, 14363u),
            (0x25D4u, 14364u), (0x25D4u, 14365u), (0x242Du, 15057u) })
        {
            object Raw(double started) => new { ActionId = action, TargetId = 100ul, Location = Pose(center),
                Rotation = .7f, TotalTime = 3f, StartedAt = Start.AddSeconds(started), FinishAt = Start.AddSeconds(started + 3.3) };
            load(guid, [Header(796), At(0, Initial(oid), true),
                At(3, [Actor(2, 0x233C, center, cast: Raw(0))]),
                At(3.6, [Actor(2, 0x233C, center, cast: Raw(.6))]), At(5, [])]);
            controller.Seek(3.2);
            var accepted = Value(Values("OutputCasts").Single(), "Value")!;
            var pool = Values("CarnivaleRayCasts").SingleOrDefault();
            controller.Seek(3.8);
            var current = Value(Values("OutputCasts").Single(), "Value")!;
            Check((DateTime)Value(accepted, "StartedAt")! == (DateTime)Value(current, "StartedAt")!
                && (DateTime)Value(accepted, "FinishAt")! == (DateTime)Value(current, "FinishAt")!,
                "Raw saturation recreated accepted ordinary cast.");
            if (pool != null)
                Check((long)Value(pool, "Generation")! == (long)Value(Values("CarnivaleRayCasts").Single(), "Generation")!
                    && (DateTime)Value(Value(pool, "Cast")!, "FinishAt")!
                        == (DateTime)Value(Value(Values("CarnivaleRayCasts").Single(), "Cast")!, "FinishAt")!,
                    "Raw saturation recreated pool or changed its original deadline.");
            stop();
        }
        Console.WriteLine("PASS native Carnivale Pudding Golem Mock five raw saturated clocks preserve cast generations pool deadlines and ordinary R5 clock");

        foreach (var (oid, action) in new[] { (0x25C0u, 14265u), (0x25D4u, 14363u),
            (0x25D4u, 14364u), (0x25D4u, 14365u), (0x242Du, 15057u) })
        {
            load(guid, [Header(796), At(0, Initial(oid, false), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center, target: 100))]),
                Cancel(.2, 2, action), At(.3, [Actor(1, oid, center, combat: true)]), At(1, [])]);
            controller.Seek(.5);
            Check(Values("OutputCasts").Length == 0 && Values("CarnivaleRayCasts").Length == 0
                && Values("CarnivaleInterrupts").Length == 0, "Unclaimed canceled cast revived on pull.");
            stop();
        }
        Console.WriteLine("PASS native Carnivale Pudding Golem Mock five unclaimed preactivation cancellations block unchanged raw bootstrap");

        foreach (var (oid, action, poolOid, radius, delay) in new[] {
            (0x25D4u, 14364u, 0x1E8FEAu, 6f, (double)1.2f), (0x242Du, 15057u, 0x1E9C9Du, 4f, 1d) })
        {
            load(guid, [Header(796), At(0, Initial(oid), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center, target: 100))]),
                TreasureNativeEffect(.2, 999, action, 1, 100, [100]), TreasureNativeEffect(.3, 2, action, 2, 100, [100]),
                Cancel(.6, 2, action), At(1, [Actor(10, poolOid, center + Vector3.UnitZ * 3, dead: true, targetable: false)]),
                At(2, []), At(3, [Actor(10, poolOid, center, eventState: 7)]), At(4, [])]);
            controller.Seek(.5);
            var prediction = Values("CarnivaleRayPredictions").Single();
            Check(Values("CarnivaleRayCasts").Length == 1 && (DateTime)Value(prediction, "Activation")! == Start.AddSeconds(.3).AddSeconds(delay)
                && (ulong)Value(prediction, "MainTarget")! == 100 && dangers() == (oid == 0x242D ? 3 : 2), "Independent cast/effect shape.");
            Check(Shown("carnivale/ray/").All(a => (float)Value(Value(a, "Geometry")!, "Radius")! == radius), "Pool source radius.");
            controller.Seek(2.5);
            Check(Values("CarnivaleRayCasts").Length == 0 && Values("CarnivaleRayPredictions").Length == 0
                && dangers() == 1, "Live replaces event no timeout.");
            controller.Seek(3.5);
            Check(dangers() == 0, "Live state7.");
            controller.Seek(.5);
            stop();
            Console.WriteLine($"PASS native Carnivale {(oid == 0x25D4 ? "Golem" : "Mock")} three native voidzone paths radius{radius} exact delay{delay} MainTarget source gate independent ordinary shape Finish live death state7 reverse Seek");
        }

        var six = new uint[] { 0x2714, 0x2716, 0x2711, 0x2713, 0x2715, 0x2712 };
        load(guid, [Header(796), At(0, Initial(0x242D).Concat(six.Select((oid, i) =>
            WithHp(Actor((ulong)(10 + i), oid, center, isEnemy: true, objectIndex: (ushort)(4 + i * 2)), i == 0 ? 1u : 1000u))).ToArray(), true),
            At(.1, []), At(1, [Actor(1, 0x242D, center, dead: true)]), At(2, [])]);
        controller.Seek(.5);
        var target = Encounter().GetType().GetMethod("PriorityTarget")!.Invoke(Encounter(), [20ul, center]);
        Check(Shown("carnivale/enemy/").Length == 7 && Shown("carnivale/enemy/").Count(a =>
            (Vector4?)Value(a, "Color") == new Vector4(1, 128f / 255, 0, 1)) == 6
            && target is ulong id && id == 10, "All adds markers unconditional priority including pending-dead.");
        controller.Seek(1.5);
        Check(Value(Encounter(), "Active") == null, "Mock primary completion.");
        stop();
        Console.WriteLine("PASS native Carnivale Mock six orange adds all legal priority1 over boss0 default Primary completion and owner cleanup");
    }
}
