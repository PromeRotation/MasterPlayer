using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivaleSnorters(object managerObject, object runtimeObject, object controllerObject,
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
        object State() => Value(Encounter(), "CarnivaleSnorter")!;
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")!).Contains(prefix)).ToArray();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [center])!).Cast<string>().ToArray();
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed, party: [new { Slot = 0, EntityId = 100ul }]);
        string Cancel(double t, ulong source, uint action) => JsonSerializer.Serialize(new { t, kind = "CancelCast",
            type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
            data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(t) } });
        object[] Initial(bool combat = true) => [Actor(1, 0x2CF8, center, combat: combat), Actor(2, 0x233C, center),
            Actor(100, 0, center + Vector3.UnitZ * 5)];

        load(guid, [Header(796), At(0, Initial(false), true), At(.1, [Actor(1, 0x2CF8, center, combat: true)]),
            At(1, [Actor(1, 0x2CF8, center, dead: true)]), At(2, [])]);
        controller.Seek(.01);
        Check(Value(Encounter(), "Active") == null && dangers() == 0 && Hints().Any(h => h.Contains("35s")), "Snorters preparation.");
        controller.Seek(.5);
        var source = Assembly.Load("MasterPlayer.MechanicRuntime");
        var bounds = source.GetType("BossMod.Global.MaskedCarnivale.Layouts")!.GetField("CircleBig")!.GetValue(null)!;
        var wdir = source.GetType("BossMod.WDir")!;
        dynamic arena = Value(Encounter(), "Arena")!;
        var count = 0;
        for (var x = 74.13f; x < 126; x += .67f)
            for (var z = 74.17f; z < 126; z += .71f)
            {
                var expected = (bool)bounds.GetType().GetMethod("Contains")!.Invoke(bounds,
                    [Activator.CreateInstance(wdir, [x - 100, z - 100])])!;
                Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, "Snorters source arena.");
                ++count;
            }
        controller.Seek(1.5);
        Check(Value(Encounter(), "Active") == null, "Snorters primary completion.");
        controller.Seek(.5);
        manager.SetMechanismEnabled(guid, "鼻息爆破死斗", false);
        controller.Seek(.6);
        Check(previews().Length == 0 && dangers() == 0, "Snorters disabled owner.");
        manager.SetMechanismEnabled(guid, "鼻息爆破死斗", true);
        stop();
        Console.WriteLine($"PASS native Carnivale Stage27 source arena oracle {count} points default primary lifecycle preparation reverse Seek disabled owner");

        var bomb = center + Vector3.UnitX * 15;
        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(3, 0x2CF9, bomb, animationState1: 1)]),
            At(1, [Actor(3, 0x2CF9, bomb, animationState1: 1, modelState: 1)]),
            At(2, [Actor(3, 0x2CF9, bomb, animationState1: 1, modelState: 1, animationState2: 1)]),
            EffectEvent(3, 3, 19259, 1, target: 100), EffectEvent(3, 3, 19259, 1, target: 1),
            TreasureNativeEffect(3, 3, 19259, 1, 100, [100, 1]), At(4, [], removed: [3]),
            At(5, [Actor(3, 0x2CF9, center)]), TreasureNativeEffect(6, 3, 19259, 2, 100, [100]), At(7, [])]);
        controller.Seek(.5);
        Check(dangers() == 1, "Live Create must be followed by real nondefault ModelState.");
        controller.Seek(1.5);
        Check(dangers() == 2 && Shown("carnivale/snorter/bomb/").Length == 2, "ModelState native output.");
        controller.Seek(2.5);
        Check(dangers() == 3 && Shown("carnivale/snorter/bomb/").Length == 3
            && (DateTime)Value(State(), "ExplosionActivation")! > Start.AddSeconds(7.9), "Duplicate references/shared clock.");
        controller.Seek(3.5);
        Check(dangers() == 2, "Native packet must remove one reference despite flattened multi-target effects.");
        controller.Seek(6.5);
        Check(dangers() == 2 && Shown("carnivale/snorter/bomb/").All(p => (Vector3)Value(Value(p, "Geometry")!, "Position")! == bomb),
            "Destroyed old identity frozen/new effect must not clear.");
        controller.Seek(2.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage27 real model changes duplicate bomb references shared deadline first Explosion removal old identity frozen native hazards dual outputs reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true), At(.1, [Actor(3, 0x2CF9, bomb)]),
            At(.2, [Actor(3, 0x2CF9, bomb, animationState1: 1)]),
            At(.3, [Actor(2, 0x233C, center, cast: Cast(19266, 4, center, startedAt: .3))]),
            Cancel(1, 2, 19266), At(8, []), At(9, [])]);
        controller.Seek(.5);
        Check(dangers() == 1 && Shown("carnivale/snorter/snort/").Length >= 1
            && MathF.Abs(((Vector3)Value(Value(Shown("carnivale/snorter/bomb/").Single(), "Geometry")!, "Position")!).X - 124.1f) < .001f,
            "Snort player and bomb independent wall projection.");
        controller.Seek(1.5);
        Check(Shown("carnivale/snorter/snort/").Length == 0 && dangers() == 1
            && (DateTime)Value(State(), "SnortEffectEnds")! != default, "Cancel lost bomb projection window.");
        controller.Seek(8.5);
        Check((Vector3)Value(Value(Shown("carnivale/snorter/bomb/").Single(), "Geometry")!, "Position")! == bomb,
            "After Snort window did not restore bomb.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage27 Snort locked 15 wall knockback separate bomb dynamic projection fixed point4 margin cancel retained window no KB navigation reverse Seek");

        var eight = Enumerable.Range(10, 8).Select(i => Actor((ulong)i, 0x2CF9, center + Vector3.UnitZ * 20)).ToArray();
        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(4, 0x2CEC, new(96, 0, 94)), .. eight]),
            At(.2, [Actor(10, 0x2CF9, center + Vector3.UnitZ * 20, animationState1: 1)]),
            TreasureNativeEffect(1, 2, 19259, 1, 100, [100]), TreasureNativeEffect(2, 2, 19256, 2, 100, [100]), At(3, [])]);
        controller.Seek(.5);
        Check((bool)Value(State(), "OtherPatterns")! && (DateTime)Value(State(), "FungahActivation")! > Start.AddSeconds(5.3)
            && Shown("carnivale/snorter/fungah/").Length >= 1 && dangers() == 1, "Fungah eight/special timing.");
        controller.Seek(1.5);
        Check((DateTime)Value(State(), "FungahActivation")! == default && (bool)Value(State(), "OtherPatterns")!
            && Shown("carnivale/snorter/fungah/").Length >= 1, "Explosion cleared pattern too.");
        controller.Seek(2.5);
        Check(!(bool)Value(State(), "OtherPatterns")! && Shown("carnivale/snorter/fungah/").Length == 0, "Fungah effect.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage27 exact eight bombs first explosive special Fungah cone 15 wall separate flags event endings no timeout native preview no automatic action reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(19258, 4, center))]),
            EffectEvent(1, 2, 19258, 1), At(8, []), Cancel(9, 2, 19258), At(10, [])]);
        controller.Seek(8.5);
        Check(dangers() == 1 && Shown("cast/").Length == 1, "Fireball effect/timeout cleared ordinary.");
        controller.Seek(9.5);
        Check(dangers() == 0, "Fireball real Finish.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage27 Fireball source R8 locked position observed Finish ordinary native navigation reverse Seek");
    }
}
