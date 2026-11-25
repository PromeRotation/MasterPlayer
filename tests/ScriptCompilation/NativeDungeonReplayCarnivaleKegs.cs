using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivaleKegs(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.carnivale.stage01";
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
        string Cancel(double t, ulong source, uint action)
            => JsonSerializer.Serialize(new { t, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(t) } });
        object[] Initial(uint oid, bool combat = true) =>
        [
            Actor(1, oid, center, combat: combat, objectIndex: 2, isEnemy: true),
            Actor(2, 0x233C, center),
            Actor(3, 0x2726, center + Vector3.UnitX * 5, objectIndex: 4, isEnemy: true),
            Actor(100, 0, center + Vector3.UnitZ * 10), Actor(101, 0, center + Vector3.UnitZ * 5),
        ];
        foreach (var (oid, name) in new[] { (0x2724u, "爆破死斗·第一幕"), (0x2725u, "爆破死斗·第二幕") })
        {
            load(guid, [Header(796), At(0, Initial(oid, false), true),
                At(.5, [Actor(3, 0x2726, center + Vector3.UnitX * 5, combat: true, objectIndex: 4, isEnemy: true),
                    Actor(4, oid, center + Vector3.UnitX * 2, combat: true)]),
                At(1, [Actor(1, oid, center, combat: true, dead: true)]),
                At(2, [Actor(4, oid, center, dead: true)]),
                At(3, [Actor(3, 0x2726, center + Vector3.UnitX * 5, dead: true)]), At(4, [])]);
            controller.Seek(.1);
            Check(Value(Encounter(), "Active") == null && Value(Encounter(), "CarnivalePrecombatHint") != null
                && Shown("carnivale/explosion/").Length == 1 && dangers() == 0
                && (bool)Value(Shown("carnivale/explosion/").Single(), "OutlineOnly")
                && Hints().Contains("In keg explosion radius!"), "Keg constructor outline became navigation.");
            controller.Seek(.6);
            Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == oid
                && Value(Encounter(), "CarnivalePrecombatHint") == null
                && Shown("carnivale/enemy/").Length == (oid == 0x2725 ? 3 : 2)
                && (Vector4)Value(Shown("carnivale/enemy/3").Single(), "Color") == new Vector4(1, 128f / 255, 0, 1),
                "Keg group pull or per-act marker scope.");
            var assembly = Assembly.Load("MasterPlayer.MechanicRuntime");
            var bounds = assembly.GetType("BossMod.Global.MaskedCarnivale.Layouts")!
                .GetField("CircleBig", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
            var direction = assembly.GetType("BossMod.WDir")!;
            dynamic arena = Value(Encounter(), "Arena");
            var count = 0;
            for (var x = 73.13f; x < 127; x += .7f)
                for (var z = 73.17f; z < 127; z += .7f)
                {
                    var expected = (bool)bounds.GetType().GetMethod("Contains")!.Invoke(bounds,
                        [Activator.CreateInstance(direction, [x - 100, z - 100])])!;
                    Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, "Keg source arena.");
                    ++count;
                }
            controller.Seek(2.5);
            Check(Value(Encounter(), "Active") != null, "Keg stage ended before barrel died.");
            controller.Seek(3.5);
            Check(Value(Encounter(), "Active") == null && previews().Length == 0 && dangers() == 0, "Keg all dead cleanup.");
            controller.Seek(.6);
            manager.SetMechanismEnabled(guid, name, false);
            controller.Seek(.7);
            Check(previews().Length == 0 && dangers() == 0, "Keg owner switch.");
            manager.SetMechanismEnabled(guid, name, true);
            stop();
            Console.WriteLine($"PASS native Carnivale Stage18 {oid:X} source big arena oracle {count} points constructor R10 outline group pull complete enemy markers colours reverse Seek owner");
        }

        foreach (var oid in new uint[] { 0x2724, 0x2725 })
            foreach (var (action, shape, radius) in new[]
            {
                (15054u, "CircleTemplate", 10f), (15051u, "CircleTemplate", 6f),
                (15050u, "FanTemplate", 8f), (15052u, "FanTemplate", 15f),
            })
            {
                load(guid, [Header(796), At(0, Initial(oid), true),
                    At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center, .7f))]),
                    EffectEvent(7, 2, action, 1), At(8, []), Cancel(9, 2, action), At(10, [])]);
                controller.Seek(.5);
                var cast = Value(Values("Casts").Single(), "Value");
                var template = Value(Value(cast, "Rule"), "Shape");
                Check(template.GetType().Name == shape && (float)Value(template, "Radius") == radius
                    && (shape != "FanTemplate" || (float)Value(template, "AngleRadians") == MathF.PI / 2)
                    && dangers() == 1 && Shown("cast/").Length == 1 && Shown("carnivale/explosion/").Length == 1,
                    "Keg ordinary geometry or constructor separation.");
                Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = (ushort)action })
                    && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = 15053 }),
                    "Bone Shaker expanded component scope.");
                controller.Seek(8.5);
                Check(dangers() == 1, "Ordinary AOE Effect/deadline cleanup.");
                controller.Seek(9.5);
                Check(dangers() == 0 && Shown("carnivale/explosion/").Length == 1, "Ordinary Finish cleared constructor cache.");
                controller.Seek(.5);
                stop();
            }
        Console.WriteLine("PASS native Carnivale Stage18 eight ordinary component instances exact R10 R6 R8 R15 quarter cones constructor display separate navigation real end inactive BoneShaker");

        foreach (var oid in new uint[] { 0x2724, 0x2725 })
        {
            load(guid, [Header(796), At(0, Initial(oid), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(15055, 3.5, center, target: 100))]),
                At(1, [Actor(2, 0x233C, center + Vector3.UnitX * 3, cast: Cast(15055, 3.5, center, target: 100)),
                    Actor(100, 0, center + Vector3.UnitZ * 13)]),
                At(2, [Actor(2, 0x233C, center + Vector3.UnitX * 3, dead: true, cast: Cast(15055, 3.5, center, target: 100))]),
                Cancel(3, 2, 15055), At(4, [])]);
            controller.Seek(1.5);
            var charge = Values("CarnivaleCharges").Single();
            Check((Vector3)Value(Value(Value(charge, "Source"), "Actor"), "Position") == center + Vector3.UnitX * 3
                && (Vector3)Value(Value(Value(charge, "Target"), "Actor"), "Position") == center + Vector3.UnitZ * 13
                && Value(Encounter(), "CarnivaleChargeKnockback") == null
                && dangers() == 1 && Shown("/charge/").Single(a => ((string)Value(a, "Id")).EndsWith("/display")) is { } display
                && (bool)Value(display, "OutlineOnly") && Values("PredictedDamage").Length == 1, "WildCharge inherited native model.");
            Check(Hints().Contains("Aim charge at a keg!") && !Hints().Contains("Bait away from raid!")
                && !Hints().Contains("GTFO from baited aoe!"), "WildCharge text override.");
            controller.Seek(2.5);
            Check(Values("CarnivaleCharges").Length == 1 && Shown("/charge/").Length == 0 && dangers() == 0
                && Hints().Contains("Aim charge at a keg!"), "WildCharge hint used live baits.");
            controller.Seek(3.5);
            Check(Values("CarnivaleCharges").Length == 0 && !Hints().Contains("Aim charge at a keg!"), "WildCharge Finish.");
            controller.Seek(1.5);
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage18 both dynamic WildCharge actor references inherited personal navigation damage outline no knockback overridden hints dead source Finish reverse Seek");

        load(guid, [Header(796), At(0, Initial(0x2724, false), true),
            At(.1, [], removed: [3]),
            At(1, [Actor(1, 0x2724, center, combat: true),
                Actor(2, 0x233C, center, cast: Cast(15055, 3.5, center, target: 100, startedAt: 1))]),
            At(2, [Actor(4, 0x2726, center + Vector3.UnitX * 8)]),
            At(3, [], removed: [4]),
            At(4, [Actor(4, 0x2726, center + Vector3.UnitX * 18)]),
            At(5, [Actor(4, 0x2726, center + Vector3.UnitX * 18, dead: true)]),
            At(6, [Actor(4, 0x2726, center + Vector3.UnitX * 18)]), At(7, [])]);
        controller.Seek(1.5);
        Check(Shown("carnivale/explosion/").Length == 1 && !Hints().Contains("Aim charge at a keg!"),
            "WildCharge reused constructor pre-pull destroyed barrel.");
        controller.Seek(2.5);
        Check(Shown("carnivale/explosion/").Length == 2 && Hints().Contains("Aim charge at a keg!"), "New barrel not delivered.");
        controller.Seek(3.5);
        Check(Shown("carnivale/explosion/").Length == 2 && Hints().Contains("Aim charge at a keg!"), "Destroyed barrel unfroze or removed.");
        controller.Seek(4.5);
        Check(Shown("carnivale/explosion/").Length == 3, "Same-ID fresh barrel did not create new cache identity.");
        controller.Seek(6.5);
        Check(Shown("carnivale/explosion/").Length == 2 && Hints().Contains("Aim charge at a keg!"),
            "Death removed old destroyed generations or revival reinserted.");
        controller.Seek(1.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage18 separate constructor combat caches pre-pull destroyed barrel excluded from new charge cache destroyed references freeze same ID adds death removes only new revival does not reinsert");

        load(guid, [Header(796), At(0, Initial(0x2724), true),
            At(.05, [Actor(4, 0x2725, center, combat: true)]),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(15055, 3.5, center, target: 100))]),
            At(.2, [Actor(1, 0x2724, center, dead: true), Actor(3, 0x2726, center, dead: true)]),
            Cancel(.3, 2, 15055), At(1, [])]);
        controller.Seek(.25);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x2725
            && Values("CarnivaleCharges").Length == 1 && dangers() == 1 && !Hints().Contains("Aim charge at a keg!"),
            "Shared action handoff discarded background bait or missed keg death.");
        controller.Seek(.5);
        Check(Values("CarnivaleCharges").Length == 0 && dangers() == 0, "Shared action Finish.");
        stop();
        Console.WriteLine("PASS native Carnivale Stage18 independent modules shared charge action retained through unload cached barrel death background propagation Finish owner");
        load(guid, [Header(796), At(0, [Actor(1, 0x2724, center), Actor(2, 0x233C, center),
                Actor(4, 0x233C, center), Actor(100, 0, center + Vector3.UnitZ * 10)], true),
            At(.1, [Actor(4, 0x233C, center, cast: Cast(15055, 5, center, target: 100)),
                Actor(2, 0x233C, center, cast: Cast(15055, 3, center, target: 100)),
                Actor(1, 0x2724, center, combat: true)]), At(1, [])]);
        controller.Seek(.5);
        Check(Values("CarnivaleCharges").Select(c => (ulong)Value(Value(c, "Cast"), "Source")).SequenceEqual(new ulong[] { 2, 4 })
            && (DateTime)Value(Values("PredictedDamage").Single(), "Activation") == Start.AddSeconds(3.4),
            "Charge bootstrap snapshot order replaced persistent source order.");
        controller.Seek(.05);
        controller.Seek(.5);
        Check((DateTime)Value(Values("PredictedDamage").Single(), "Activation") == Start.AddSeconds(3.4),
            "Source bootstrap order changed after reverse Seek.");
        stop();
        Console.WriteLine("PASS native Carnivale Stage18 persistent actor creation order for simultaneous charge bootstrap first damage clock despite reversed delta source order reverse Seek");
    }
}
