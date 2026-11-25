using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivalePapa(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.carnivale.stage01";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(100, 0, 100);
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Values(string name) => ((IEnumerable)Value(Encounter(), name)).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        int Obstacles() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [center])!).Cast<string>().ToArray();
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed, party: [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }]);
        string Cancel(double t, ulong source, uint action)
            => JsonSerializer.Serialize(new { t, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(t) } });
        string Icon(double t, ulong actor, ulong target)
            => JsonSerializer.Serialize(new { t, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                data = new { SourceId = actor, TargetId = target, MarkerId = 23u, Timestamp = Start.AddSeconds(t) } });
        object[] Initial(uint oid = 0x2C58, bool combat = true) => [Actor(1, oid, center, combat: combat),
            Actor(2, 0x233C, center), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 5)];
        object? KB() => Value(Encounter(), "CarnivaleDadJoke");
        object? Landing()
        {
            var actors = Value(Encounter(), "Actors");
            var player = actors.GetType().GetProperty("Item")!.GetValue(actors, [100ul])!;
            return Encounter().GetType().GetMethod("CarnivalePapaLanding", flags)!.Invoke(Encounter(), [player]);
        }

        foreach (var (oid, name) in new[] { (0x2C84u, "强大的老爹——洪巴巴之父·第一幕"), (0x2C58u, "强大的老爹——洪巴巴之父·第二幕") })
        {
            load(guid, [Header(796), At(0, Initial(oid, false), true), Icon(.05, 100, 101),
                At(.1, [Actor(1, oid, center, combat: true)]), At(1, [Actor(1, oid, center, dead: true)]), At(2, [])]);
            controller.Seek(.01);
            Check(Value(Encounter(), "Active") == null && dangers() == 0 && Hints().Length >= 1, "Papa preparation.");
            controller.Seek(.5);
            var source = Assembly.Load("MasterPlayer.MechanicRuntime");
            var bounds = source.GetType("BossMod.Global.MaskedCarnivale.Layouts")!.GetField(oid == 0x2C58 ? "CircleSmall" : "CircleBig")!.GetValue(null)!;
            var wdir = source.GetType("BossMod.WDir")!;
            dynamic arena = Value(Encounter(), "Arena");
            var count = 0;
            for (var x = 74.13f; x < 126; x += .67f)
                for (var z = 74.17f; z < 126; z += .71f)
                {
                    var expected = (bool)bounds.GetType().GetMethod("Contains")!.Invoke(bounds,
                        [Activator.CreateInstance(wdir, [x - 100, z - 100])])!;
                    Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, "Papa source arena.");
                    ++count;
                }
            Check(KB() == null && Shown("carnivale/enemy/").Length == 1, "Papa icon history/markers.");
            controller.Seek(1.5);
            Check(Value(Encounter(), "Active") == null, "Papa completion.");
            controller.Seek(.5);
            manager.SetMechanismEnabled(guid, name, false);
            controller.Seek(.6);
            Check(previews().Length == 0 && dangers() == 0 && Obstacles() == 0, "Papa disabled owner.");
            manager.SetMechanismEnabled(guid, name, true);
            stop();
            Console.WriteLine($"PASS native Carnivale Stage26 {oid:X} source arena oracle {count} points default primary lifecycle no historical icon bootstrap reverse Seek disabled owner");
        }

        foreach (var (oid, action) in new[] { (0x2C84u, 18687u), (0x2C58u, 18602u) })
        {
            load(guid, [Header(796), At(0, Initial(oid), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center))]),
                EffectEvent(1, 2, action, 1), At(8, []), Cancel(9, 2, action), At(10, [])]);
            controller.Seek(.5);
            Check(dangers() == 1 && Shown("cast/").Length == 1, "Papa ordinary.");
            controller.Seek(8.5);
            Check(dangers() == 1, "Papa effect/timeout cleared ordinary.");
            controller.Seek(9.5);
            Check(dangers() == 0 && Shown("cast/").Length == 0, "Papa ordinary Finish.");
            controller.Seek(.5);
            stop();
        }
        foreach (var (oid, action, hint) in new[] { (0x2C84u, 18688u, "Interrupt!"),
            (0x2C84u, 18686u, "Prepare to dispel buff"), (0x2C58u, 18604u, "Prepare to dispel buff") })
        {
            load(guid, [Header(796), At(0, Initial(oid), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center))]), Cancel(9, 2, action), At(10, [])]);
            controller.Seek(.5);
            Check(Hints().Contains(hint) && dangers() == 0 && Values("PredictedDamage").Length == 0, "Papa pure hint/interrupt.");
            controller.Seek(9.5);
            Check(!Hints().Contains(hint), "Papa hint Finish.");
            controller.Seek(.5);
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage26 R3 R4 ordinary native navigation dual outputs observed Finish and three pure cast hint interrupt components no auto dispel or damage");

        var raw = center + new Vector3(1.12345f, 0, 2.37654f);
        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(4, 0x2C59, raw, dead: true, targetable: false)]), EffectEvent(1, 4, 18606, 1),
            At(2, [Actor(4, 0x2C59, raw, dead: true, eventState: 7)]),
            At(3, [Actor(4, 0x2C59, raw, dead: true)]), At(4, [], removed: [4]), At(5, [])]);
        controller.Seek(.5);
        Check(Obstacles() == 1 && dangers() == 0 && Shown("carnivale/thunderhead/").Length == 1
            && (Vector3)Value(Value(Shown("carnivale/thunderhead/").Single(), "Geometry"), "Position") == raw
            && Shown("carnivale/enemy/").Length == 1, "Thunderhead raw dead untargetable.");
        controller.Seek(2.5);
        Check(Obstacles() == 0, "Thunderhead state7.");
        controller.Seek(3.5);
        Check(Obstacles() == 1, "Thunderhead restore.");
        controller.Seek(4.5);
        Check(Obstacles() == 0, "Thunderhead destroy.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage26 Thunderhead raw R8 hard obstacle dead untargetable state7 restore destroy no effect dependency double view no enemy marker reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true), Icon(.1, 100, 101), Icon(1, 101, 100),
            At(2, [Actor(1, 0x2C58, center + Vector3.UnitX * 3, combat: true)]),
            Cancel(3, 1, 18605), At(20, []), EffectEvent(21, 2, 18605, 1), At(22, [])]);
        controller.Seek(.5);
        var initial = KB()!;
        Check((DateTime)Value(initial, "Activation") == ((DateTime)Value(initial, "ReceivedAt")).AddSeconds(5d)
            && Shown("carnivale/papa/kb/").Length >= 1 && dangers() == 0, "Papa icon clock/projection.");
        controller.Seek(1.5);
        Check((long)Value(KB()!, "Generation") != (long)Value(initial, "Generation"), "Papa icon overwrite.");
        controller.Seek(2.5);
        Check(Hints().All(h => !h.Contains("About to be knocked")) && Shown("carnivale/papa/kb/").Length >= 1, "Papa dynamic Primary.");
        controller.Seek(20.5);
        Check(KB() != null, "Papa icon KB timeout/cancel.");
        controller.Seek(21.5);
        Check(KB() == null && Shown("carnivale/papa/kb/").Length == 0, "Papa nonPrimary effect clear.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage26 icon23 overwrites processing five second clock dynamic primary viewer coincidence plusZ no timeout cancel only effect18605 clear reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true), Icon(.1, 100, 100),
            At(.2, [Actor(4, 0x2C59, center + Vector3.UnitZ * 15, dead: true, targetable: false)]),
            At(1, [Actor(4, 0x2C59, center + Vector3.UnitZ * 15, dead: true, eventState: 7)]), At(2, [])]);
        controller.Seek(.5);
        Check(Hints().Contains("About to be knocked into danger!") && dangers() == 0 && Obstacles() == 1, "Papa current landing danger.");
        var activation = (DateTime)Value(KB()!, "Activation");
        void Status(uint id, DateTime expiry) => Encounter().GetType().GetMethod("StatusChanged")!.Invoke(Encounter(),
            [100ul, id, true, Start.AddSeconds(.5), (ushort)0, expiry, 0ul]);
        Status(160, activation);
        Check(Landing() is Vector3 equal && Vector3.Distance(equal, center + Vector3.UnitZ * 15) < .001f, "Papa equal expiry immunity.");
        Status(160, activation.AddTicks(1));
        Check(Landing() == null, "Papa strict later immunity.");
        Status(1209, Start);
        Check(Landing() != null, "Papa category overwrite.");
        controller.Seek(1.5);
        Check(!Hints().Contains("About to be knocked into danger!") && Obstacles() == 0, "Papa state7 danger persists.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage26 strict expiry equality immunity role overwrite live Thunderhead landing no KB navigation movement hold or automatic immunity");

        load(guid, [Header(796), At(0, Initial(), true),
            Icon(.05, 999, 100), Icon(.1, 100, 101),
            At(1, [], removed: [1, 100]), At(2, [Actor(1, 0x2C58, center + Vector3.UnitX * 3, combat: true), Actor(100, 0, center)]),
            EffectEvent(3, 999, 18605, 1), EffectEvent(4, 2, 18605, 1), At(5, [])]);
        controller.Seek(.06);
        Check(KB() == null, "Unknown icon source admitted.");
        controller.Seek(.5);
        var oldActivation = (DateTime)Value(KB()!, "Activation");
        controller.Seek(2.5);
        Check(KB() != null && (DateTime)Value(KB()!, "Activation") == oldActivation
            && Landing() is Vector3 rebound && Vector3.Distance(rebound, center - Vector3.UnitX * 15) < .001f,
            "Primary same-ID rebound did not update origin.");
        controller.Seek(3.5);
        Check(KB() != null, "Unknown effect source cleared KB.");
        controller.Seek(4.5);
        Check(KB() == null, "Known helper effect did not clear KB.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage26 party0 absent primary same-ID rebind updates dynamic origin preserves icon clock unknown sources rejected known helper effect clears reverse Seek");

        foreach (var (action, hint, count) in new[] { (18603u, "Raidwide + Electrocution", 2), (18601u, "Soft Tankbuster", 1) })
        {
            load(guid, [Header(796), At(0, Initial(), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center, target: 101))]),
                At(1, [Actor(2, 0x233C, center, cast: Cast(action, 30, center, target: 101))]),
                Cancel(8, 2, action), At(9, [])]);
            controller.Seek(.5);
            Check(Values("PredictedDamage").Length == count && Hints().Contains(hint) && dangers() == 0, "Papa damage scope.");
            controller.Seek(1.5);
            var finish = JsonSerializer.SerializeToElement(Cast(action, 30, center)).GetProperty("FinishAt").GetDateTime();
            Check(Values("PredictedDamage").All(p => (DateTime)Value(p, "Activation") == finish), "Papa dynamic damage clock.");
            controller.Seek(8.5);
            Check(Values("PredictedDamage").Length == 0, "Papa damage Finish.");
            controller.Seek(.5);
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage26 actual raidwide and single target dynamic raw predictions source hints no R20 navigation true Finish reverse Seek");

        foreach (var (oid, bossStatus, playerStatus, text) in new[] { (0x2C84u, 63u, 269u, "Windburn"), (0x2C58u, 1797u, 271u, "Electrocution") })
        {
            load(guid, [Header(796), At(0, [Actor(1, oid, center, combat: true, stackedStatus: bossStatus, statusExpiresAt: Start),
                Actor(100, 0, center, stackedStatus: playerStatus, statusExpiresAt: Start)], true),
                At(10, []), At(11, [Actor(1, oid, center, combat: true), Actor(100, 0, center)]), At(12, [])]);
            controller.Seek(10.5);
            Check(Hints().Any(h => h.StartsWith("Dispel ")) && Hints().Any(h => h.StartsWith(text + " on you!")), "Papa occupied expired slots.");
            controller.Seek(11.5);
            Check(!Hints().Any(h => h.StartsWith("Dispel ") || h.StartsWith(text + " on you!")), "Papa removed status retained.");
            controller.Seek(10.5);
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage26 occupied expired buff and personal debuff slots until actual removal no auto dispel restrictions reverse Seek");
    }
}
