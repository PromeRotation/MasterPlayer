using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivaleRed(object managerObject, object runtimeObject, object controllerObject,
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
        object State() => Value(Encounter(), "CarnivaleRed")!;
        object[] Values(string property) => ((IEnumerable)Value(Encounter(), property)!).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")!).Contains(prefix)).ToArray();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [center])!).Cast<string>().ToArray();
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed, party: [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }]);
        string Cancel(double t, ulong source, uint action) => JsonSerializer.Serialize(new { t, kind = "CancelCast",
            type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
            data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(t) } });
        object[] Initial(uint oid = 0x2C5D, bool combat = true) => [Actor(1, oid, center, combat: combat),
            Actor(2, 0x233C, center), Actor(3, 0x233C, center),
            Actor(100, 0, center + Vector3.UnitZ * 5), Actor(101, 0, center + Vector3.UnitX * 3)];

        foreach (var (oid, name) in new[] { (0x2C5Bu, "水与火之歌·第一幕"), (0x2C5Du, "水与火之歌·第二幕") })
        {
            load(guid, [Header(796), At(0, Initial(oid, false), true),
                At(.1, [Actor(1, oid, center, combat: true)]), At(1, [Actor(1, oid, center, dead: true)]), At(2, [])]);
            controller.Seek(.01);
            Check(Value(Encounter(), "Active") == null && dangers() == 0 && Hints().Length > 0, "Red preparation.");
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
                    Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, "Red source arena.");
                    ++count;
                }
            controller.Seek(1.5);
            Check(Value(Encounter(), "Active") == null, "Red primary completion.");
            controller.Seek(.5);
            manager.SetMechanismEnabled(guid, name, false);
            controller.Seek(.6);
            Check(previews().Length == 0 && dangers() == 0, "Red disabled owner.");
            manager.SetMechanismEnabled(guid, name, true);
            stop();
            Console.WriteLine($"PASS native Carnivale Stage29 {oid:X} source arena oracle {count} points primary lifecycle preparation disabled owner reverse Seek");
        }

        foreach (var (oid, action, hazard) in new[] { (0x2C5Bu, 18694u, true), (0x2C5Bu, 18692u, true),
            (0x2C5Bu, 18696u, true), (0x2C5Bu, 18895u, true), (0x2C5Bu, 18698u, true), (0x2C5Bu, 18699u, true),
            (0x2C5Du, 18971u, true), (0x2C5Du, 18984u, true), (0x2C5Du, 18969u, true), (0x2C5Du, 18985u, true),
            (0x2C5Bu, 18690u, false), (0x2C5Du, 19022u, false), (0x2C5Du, 19028u, false), (0x2C5Du, 18964u, false) })
        {
            load(guid, [Header(796), At(0, Initial(oid), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center, rotation: MathF.PI / 2))]),
                EffectEvent(1, 2, action, 1), At(8, []), Cancel(9, 2, action), At(10, [])]);
            controller.Seek(.5);
            Check(dangers() == (hazard ? 1 : 0) && Values("PredictedDamage").Length == (action == 19022 ? 2 : 0), "Red action scope.");
            controller.Seek(8.5);
            Check(dangers() == (hazard ? 1 : 0), "Red effect/timeout.");
            controller.Seek(9.5);
            Check(dangers() == 0 && Values("PredictedDamage").Length == 0, "Red real Finish.");
            controller.Seek(.5);
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage29 ordinary circles 74 ray R39 total30 grouped cones true Finish pure Rush hand Throttle hints Cascade alive64 prediction no fabricated charge");

        foreach (var (oid, action, forward) in new[] { (0x2C5Bu, 18689u, true), (0x2C5Du, 18961u, true), (0x2C5Du, 18965u, false) })
        {
            load(guid, [Header(796), At(0, Initial(oid), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center, rotation: MathF.PI / 2, target: 999))]),
                Cancel(1, 2, action), At(2, [])]);
            controller.Seek(.5);
            var kb = Values("CarnivaleCastKnockbacks").Single();
            Check((bool)Value(kb, "Forward")! == forward && (float)Value(kb, "Distance")! == (forward ? 50 : 25)
                && (float)Value(Value(kb, "Cast")!, "Rotation")! == (forward ? MathF.PI / 2 : 0)
                && Shown("carnivale/red/kb/").Length > 0 && dangers() == 0, "Red KB lock/type no missing-target gate.");
            controller.Seek(1.5);
            Check(Shown("carnivale/red/kb/").Length == 0, "Red KB Finish.");
            controller.Seek(.5);
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage29 separate forward50 away25 no cone no wall missing target source facing locked native projections no KB navigation reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(18963, 4, center))]),
            At(.2, [Actor(3, 0x233C, center, cast: Cast(18962, 4, center, startedAt: .2))]),
            Cancel(1, 2, 18963), TreasureNativeEffect(2, 2, 18974, 1, 100, [100]), At(3, [])]);
        controller.Seek(.5);
        Check((bool)Value(Value(State(), "FerrofluidKnockback")!, "Pull")! && dangers() == 1, "Global pull replaces away.");
        controller.Seek(1.5);
        Check(Value(State(), "FerrofluidKnockback") == null && Value(State(), "FerrofluidAoe") != null && dangers() == 1,
            "Any matching Finish clears global KB only.");
        controller.Seek(2.5);
        Check(Value(State(), "FerrofluidAoe") == null && dangers() == 0, "Opposite native effect clears current global circle.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage29 global six pull clamp current hazard locked clock overwrite any source Finish KB only native opposite effect AOE clear reverse Seek");

        load(guid, [Header(796), At(0, Initial(0x2C5B), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(18691, 4, center))]), Cancel(1, 2, 18691),
            At(2, [Actor(101, 0, center, stackedStatus: 960, statusExpiresAt: Start)]),
            At(3, [Actor(101, 0, center)]), At(4, [])]);
        controller.Seek(.5);
        var requirements = (Array)Value(Value(State(), "Pyretic")!, "Requirements")!;
        Check(requirements.Cast<object?>().All(r => r != null) && Hints().Contains("Stop everything!") && dangers() == 0, "Pyretic64 model.");
        controller.Seek(1.5);
        Check(Hints().Contains("Stop everything!") && Hints().Any(h => h.StartsWith("SpecialMode: Pyretic")), "Cancel cleared semantic requirement.");
        controller.Seek(3.5);
        Check(!Hints().Contains("Stop everything!") && ((Array)Value(Value(State(), "Pyretic")!, "Requirements")!).Cast<object?>().All(r => r == null),
            "Other actor raw960 loss must clear all slots.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage29 Pyretic64 cast requirements survive cancel raw960 Gain no reset other actor real loss clears all native hints no expanded automatic control reverse Seek");

        foreach (var lossFirst in new[] { false, true })
        {
            var initial = Initial(0x2C5B).ToList();
            if (lossFirst)
            {
                var actor = initial[^1];
                initial.RemoveAt(initial.Count - 1);
                initial.Insert(1, actor);
            }
            load(guid, [Header(796), At(0, initial.ToArray(), true),
                At(.1, [Actor(101, 0, center, stackedStatus: 960, statusExpiresAt: Start)]),
                At(1, [Actor(2, 0x233C, center, cast: Cast(18691, 4, center, startedAt: 1)),
                    Actor(101, 0, center)]), At(2, [])]);
            controller.Seek(1.5);
            Check(((Array)Value(Value(State(), "Pyretic")!, "Requirements")!).Cast<object?>().All(r => (r != null) == lossFirst),
                "Raw actor Start/Lose interleaving.");
            controller.Seek(.5);
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage29 same raw frame Pyretic Start and other actor960 loss preserve both actor orders reverse Seek");
    }
}
