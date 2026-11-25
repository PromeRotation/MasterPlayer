using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivaleRebekkah(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.carnivale.stage01";
        const string ink = "Cast Ink Jet on boss to get blinded!";
        const string act2 = "魅惑芳香——反射魔花瑞贝卡·第二幕";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Values(string name) => ((IEnumerable)Value(Encounter(), name)).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [center])!).Cast<string>().ToArray();
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed, party: party);
        string Cancel(double t, ulong source, uint action, double? eventTime = null)
            => JsonSerializer.Serialize(new { t, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(eventTime ?? t) } });
        object[] Initial(uint oid = 0x2728, bool combat = true) =>
        [
            Actor(1, oid, center, combat: combat, objectIndex: 2, isEnemy: true),
            Actor(2, 0x233C, center), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 3),
        ];
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", flags)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", flags)!.GetValue(facing)!
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld")
                && Values("Gazes").Length == 0,
                "Rebekkah source scope/replay execution isolation.");
        }

        foreach (var oid in new uint[] { 0x2727, 0x2728 })
        {
            load(guid, [Header(796), At(0, Initial(oid, false), true),
                At(.1, [Actor(3, 0x2779, center, combat: true)]),
                At(1, [Actor(1, oid, center, combat: true)]),
                At(2, [Actor(1, oid, center, dead: true)]), At(3, [])]);
            controller.Seek(.5);
            Check(Value(Encounter(), "Active") == null && Value(Encounter(), "CarnivalePrecombatHint") != null
                && dangers() == 0 && Shown("carnivale/enemy/").Length == 1, "Rebekkah precombat default scope.");
            controller.Seek(1.5);
            Check(Value(Encounter(), "Active") != null && Shown("carnivale/enemy/").Length == 1
                && ((Array)Value(Encounter(), "ForbiddenTargets")).Length == 0, "Rebekkah primary pull/default enemies.");
            var assembly = Assembly.Load("MasterPlayer.MechanicRuntime");
            var bounds = assembly.GetType("BossMod.Global.MaskedCarnivale.Layouts")!
                .GetField("CircleSmall", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
            var direction = assembly.GetType("BossMod.WDir")!;
            dynamic arena = Value(Encounter(), "Arena");
            var count = 0;
            for (var x = 82.13f; x < 118; x += .47f)
                for (var z = 82.17f; z < 118; z += .53f)
                {
                    var expected = (bool)bounds.GetType().GetMethod("Contains")!.Invoke(bounds,
                        [Activator.CreateInstance(direction, [x - 100, z - 100])])!;
                    Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, "Rebekkah source small arena.");
                    ++count;
                }
            Isolated();
            controller.Seek(2.5);
            Check(Value(Encounter(), "Active") == null && previews().Length == 0, "Rebekkah waited for HotHip death.");
            controller.Seek(1.5);
            stop();
            Console.WriteLine($"PASS native Carnivale Stage19 {oid:X} source small arena oracle {count} points primary lifecycle preparation default markers reverse Seek owner");
        }

        foreach (var oid in new uint[] { 0x2727, 0x2728 })
            foreach (var action in new uint[] { 15074, 15075 })
            {
                load(guid, [Header(796), At(0, Initial(oid), true),
                    At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center, .7f))]),
                    EffectEvent(7, 2, action, 1), At(8, []), Cancel(9, 2, action), At(10, [])]);
                controller.Seek(.5);
                var cast = Value(Values("Casts").Single(), "Value");
                var shape = Value(Value(cast, "Rule"), "Shape");
                Check(dangers() == 1 && Shown("cast/").Length == 1
                    && (action == 15074 ? (float)Value(shape, "Radius") == 17.775f
                        && Math.Abs((float)Value(shape, "AngleRadians") - MathF.PI * 2 / 3) < .00001f
                        : (float)Value(shape, "Length") == 11.775f && (float)Value(shape, "MaxLocalX") == 4)
                    && (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = (ushort)action })
                    && !Hints().Contains("Interrupt!"), "Rebekkah dimensions, native takeover or comment-expanded interrupt.");
                controller.Seek(8.5);
                Check(dangers() == 1, "Rebekkah ordinary timeout.");
                controller.Seek(9.5);
                Check(dangers() == 0 && Shown("cast/").Length == 0, "Rebekkah ordinary Finish.");
                controller.Seek(.5);
                stop();
            }
        Console.WriteLine("PASS native Carnivale Stage19 four ordinary component instances exact source dimensions observed lifetime double view navigation no extra BadBreath interrupt reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(15073, 3, center))]),
            EffectEvent(.2, 2, 15073, 1), At(1, [Actor(2, 0x233C, center)]),
            At(10, []), At(11, [Actor(2, 0x233C, center, cast: Cast(15073, 3, center, startedAt: 11))]),
            Cancel(11.2, 2, 15073, eventTime: .5), Cancel(12, 2, 15073), At(13, [])]);
        controller.Seek(.5);
        Check(Hints().Contains("Boss will reflect all magic damage!") && Value(Encounter(), "CarnivaleReflectCast") != null
            && dangers() == 0, "Reflect Start/effect latch.");
        controller.Seek(10.5);
        Check(Hints().Contains("Boss reflects all magic damage!"), "Reflect raw Finish/persistent latch.");
        controller.Seek(11.5);
        Check(Hints().Contains("Boss will reflect all magic damage!"), "Reflect repeated start or stale old Finish.");
        controller.Seek(12.5);
        Check(Hints().Contains("Boss reflects all magic damage!") && ((Array)Value(Encounter(), "ForbiddenTargets")).Length == 0,
            "Reflect cancellation latch or automatic stop.");
        Isolated();
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage19 Reflect complete cast persistent any Finish latch repeated start stale Finish no reflection status or automatic restriction reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(15077, 5, center))]),
            At(1, [Actor(2, 0x233C, center)]), At(10, []),
            At(11, [Actor(100, 0, center, stackedStatus: 15, statusExpiresAt: Start)]),
            At(12, [Actor(100, 0, center)]),
            At(13, [Actor(2, 0x233C, center, cast: Cast(15078, 6, center, startedAt: 13))]),
            EffectEvent(14, 2, 15078, 1), Cancel(15, 2, 15078), At(16, [])]);
        controller.Seek(.5);
        Check(Hints().Contains(ink) && Value(Encounter(), "CarnivaleBlindTrigger") != null, "Schizocarps trigger.");
        controller.Seek(10.5);
        Check(Hints().Contains(ink), "Schizocarps raw cancel/deadline ended prompt.");
        controller.Seek(11.5);
        Check(!Hints().Contains(ink), "Blind raw gain checked expiry.");
        controller.Seek(12.5);
        Check(Hints().Contains(ink), "Blind raw loss.");
        controller.Seek(14.5);
        Check(Hints().Contains(ink) && Shown("gaze/").Length == 0 && dangers() == 0
            && Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [center, 0f]) == null,
            "ExplosiveDehiscence started a base gaze or effect cleared prompt.");
        Isolated();
        controller.Seek(15.5);
        Check(!Hints().Contains(ink), "ExplosiveDehiscence Finish did not clear prompt.");
        controller.Seek(12.5);
        manager.SetMechanismEnabled(guid, act2, false);
        controller.Seek(12.6);
        Check(dangers() == 0 && previews().Length == 0, "Rebekkah disabled owner.");
        manager.SetMechanismEnabled(guid, act2, true);
        stop();
        Console.WriteLine("PASS native Carnivale Stage19 Schizocarps cancel persists raw Blind ignores expiry loss resumes explosive effect persists Finish clears no eyes facing execution reverse Seek disabled owner");

        foreach (var oid in new uint[] { 0x2727, 0x2728 })
        {
            var position = center + Vector3.UnitX * 3;
            load(guid, [Header(796), At(0, Initial(oid), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(15076, 3, center + Vector3.UnitZ * 12, target: 101))]),
                At(1, [Actor(101, 0, center + Vector3.UnitX * 7)]), At(2, [], removed: [101]),
                Cancel(3, 2, 15076), At(4, [])]);
            controller.Seek(.5);
            Check(Hints().Contains("Interrupt!") && dangers() == 1 && Shown("carnivale/ray/cast/").Length == 1
                && (Vector3)Value(Value(Shown("carnivale/ray/cast/").Single(), "Geometry"), "Position") == position, "Offal cast target/interrupt.");
            controller.Seek(1.5);
            Check((Vector3)Value(Value(Shown("carnivale/ray/cast/").Single(), "Geometry"), "Position") == center + Vector3.UnitX * 7,
                "Offal cast moving target.");
            controller.Seek(2.5);
            Check((Vector3)Value(Value(Shown("carnivale/ray/cast/").Single(), "Geometry"), "Position") == center + Vector3.UnitZ * 12,
                "Offal missing target Loc fallback.");
            controller.Seek(3.5);
            Check(dangers() == 0 && Values("CarnivaleInterrupts").Length == 0, "Offal Finish.");
            stop();
            load(guid, [Header(796), At(0, Initial(oid), true), EffectEvent(.1, 2, 15076, 0, center, target: 100),
                TreasureNativeEffect(.1, 2, 15076, 0, 101, [100, 101]),
                TreasureNativeEffect(.2, 2, 15076, 0, 101, []),
                At(1, [Actor(101, 0, center + Vector3.UnitX * 15)]), At(10, []),
                At(11, [Actor(5, 0x1EA9F9, position + Vector3.UnitX * .123456f, dead: true)]),
                At(11.1, [Actor(5, 0x1EA9F9, position, dead: true, eventState: 7)]), At(12, [])]);
            controller.Seek(.15);
            var p = Values("CarnivaleRayPredictions").Single();
            Check((ulong)Value(p, "MainTarget") == 101 && (uint)Value(p, "Action") == 15076
                && (DateTime)Value(p, "Activation") == ((DateTime)Value(p, "ObservedAt")).AddSeconds(oid == 0x2727 ? (double)1.6f : 1.6d)
                && (Vector3)Value(p, "Position") == position && dangers() == 1, "Offal native packet/delay/legacy skip.");
            controller.Seek(10.5);
            Check(Values("CarnivaleRayPredictions").Length == 2 && dangers() == 2
                && Values("CarnivaleRayPredictions").All(v => (Vector3)Value(v, "Position") == position), "Offal event lock/lifetime/zero sequence.");
            controller.Seek(11.01);
            Check(Shown("carnivale/ray/live/").Length == 1 && dangers() >= 1, "Offal dead live zone.");
            controller.Seek(11.15);
            Check(Shown("carnivale/ray/live/").Length == 0, "Offal state7.");
            controller.Seek(.15);
            stop();
            Console.WriteLine($"PASS native Carnivale Stage19 {oid:X} OffalBreath interrupt dynamic cast target actual MainTarget native empty targets distinct zero events exact float double delay locked prediction dead live state7 navigation dual view reverse Seek");
        }

        load(guid, [Header(796), At(0, [Actor(9, 0x26F2, center, combat: true), Actor(100, 0, center)], true),
            At(.05, Initial()),
            At(.1, [Actor(1, 0x2728, center, combat: true, cast: Cast(15077, 5, center)),
                Actor(2, 0x233C, center, cast: Cast(15073, 3, center))]),
            At(.2, [Actor(2, 0x233C, center)]), TreasureNativeEffect(.2, 2, 15076, 1, 100, []),
            At(.3, [Actor(9, 0x26F2, center, combat: true, dead: true)]), At(1, [])]);
        controller.Seek(.25);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x26F2 && Shown("carnivale/ray/").Length == 0, "Background output leak.");
        controller.Seek(.5);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x2728 && Hints().Contains(ink)
            && Hints().Contains("Boss reflects all magic damage!") && Values("CarnivaleRayPredictions").Length == 1 && dangers() == 1,
            "Background reflect blind trigger event lost.");
        controller.Seek(.15);
        stop();
        Console.WriteLine("PASS native Carnivale Stage19 background reflection latch blind trigger native voidzone retained immediate module handoff owner and reverse Seek");
        load(guid, [Header(796), At(0, Initial(0x2727), true),
            At(.05, [Actor(4, 0x2728, center)]),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(15073, 3, center))]),
            At(1, [Actor(2, 0x233C, center), Actor(4, 0x2728, center, combat: true)]),
            At(2, [Actor(1, 0x2727, center, dead: true)]), At(3, [])]);
        controller.Seek(2.5);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x2728
            && !Hints().Contains("Boss reflects all magic damage!"), "New component consumed pre-activation Finish.");
        controller.Seek(.5);
        Check(Hints().Contains("Boss will reflect all magic damage!"), "Original reflection start missing after Seek.");
        stop();
        Console.WriteLine("PASS native Carnivale Stage19 newly activated background component excludes same-frame preactivation Reflect Finish and retains no historical latch reverse Seek");

        load(guid, [Header(796), At(0, Initial(0x2727), true),
            At(.05, [Actor(4, 0x2728, center)]),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(15076, 3, center, target: 101))]),
            At(20, [Actor(4, 0x2728, center, combat: true)]),
            At(21, [Actor(1, 0x2727, center, dead: true)]),
            At(22, [Actor(2, 0x233C, center)]), At(23, [])]);
        controller.Seek(20.5);
        Check((DateTime)Value(Value(Values("CarnivaleRayCasts").Single(), "Cast"), "FinishAt") == Start.AddSeconds(3.4),
            "Existing component activation refreshed.");
        controller.Seek(21.5);
        var late = (DateTime)Value(Value(Values("CarnivaleRayCasts").Single(), "Cast"), "FinishAt");
        Check(late >= Start.AddSeconds(20.3) && late < Start.AddSeconds(20.41)
            && dangers() == 1 && Shown("carnivale/ray/cast/").Length == 1, "Late component retained obsolete clock.");
        controller.Seek(20.5);
        Check((DateTime)Value(Value(Values("CarnivaleRayCasts").Single(), "Cast"), "FinishAt") == Start.AddSeconds(3.4),
            "Late activation contaminated old component after Seek.");
        controller.Seek(22.5);
        Check(Values("CarnivaleRayCasts").Length == 0 && Values("CarnivaleInterrupts").Length == 0
            && !Hints().Contains("Interrupt!") && dangers() == 0, "Late activation interrupt receipt survived Finish.");
        stop();
        Console.WriteLine("PASS native Carnivale Stage19 existing cast deadline retained late module bootstrap locks current capped NPC remaining delay independent clocks double view navigation reverse Seek");

        string Viewed(double t, object[] actors, ulong[]? removed = null) =>
            At(t, actors, removed: removed).Replace("\"LocalPlayerId\":100,", "\"LocalPlayerId\":101,", StringComparison.Ordinal);
        load(guid, [Header(796), At(0, [Actor(1, 0x2728, center, combat: true),
                Actor(2, 0x233C, center), Actor(100, 0, center)], true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(15077, 5, center))]),
            At(1, [Actor(101, 0, center, stackedStatus: 15)]),
            Viewed(2, []), Viewed(3, [], [101]),
            Viewed(4, [Actor(101, 0, center, stackedStatus: 15)]),
            Viewed(5, [Actor(101, 0, center)]), Viewed(6, [])]);
        controller.Seek(2.5);
        Check((ulong)Value(Encounter(), "PlayerId") == 101 && !Hints().Contains(ink),
            "Enrolled actor first Gain missing after view switch.");
        controller.Seek(4.5);
        Check(!Hints().Contains(ink), "Same ID reappearance lost retained roster identity.");
        controller.Seek(5.5);
        Check(Hints().Contains(ink), "Reappeared actor Blind Lose missing.");
        controller.Seek(2.5);
        Check(!Hints().Contains(ink), "Blind roster replay rebuild.");
        Isolated();
        stop();
        Console.WriteLine("PASS native Carnivale Stage19 absent world actor retained party roster first Blind Gain destruction same ID rebuild view switch raw loss reverse Seek isolation");
    }
}
