using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHeavenOnHighKenko(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.hoh.70";
        const uint territory = 774;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(-300, 0, -300);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        object Read(object obj, string name)
        {
            var property = obj.GetType().GetProperty(name, flags);
            return property != null ? property.GetValue(obj)! : obj.GetType().GetField(name, flags)!.GetValue(obj)!;
        }
        object Script() => Read(manager.Running[id], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Values(string property) => ((IEnumerable)Read(Encounter(), property)).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(p => ((string)Read(p, "Id")).Contains(prefix)).ToArray();
        object[] Snapshot(string property) => ((IEnumerable)Read((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime), property)).Cast<object>().ToArray();
        string Icon(double at) => System.Text.Json.JsonSerializer.Serialize(new { t = at, kind = "TargetIcon",
            type = "PromeRotation.LogSystem.LogSystemTargetIconEvent", data = new { TargetId = 100ul, SourceId = 101ul, MarkerId = 1u } });
        void Isolated()
        {
            var hints = Read(Script(), "Hints");
            Check(!(bool)Read(Read(hints, "_facing"), "_hasRequest")
                && !(bool)Read(Read(runtimeObject, "Setting"), "RuntimeMovementHeld")
                && !((IEnumerable)Read(hints, "forbiddenTargets")).Cast<object>().Any(), "Kenko replay executed control.");
        }
        load(id, [Header(territory), Frame(territory, 0, [Actor(1, 0x23EB, center, combat: true),
            Actor(100, 0, center + Vector3.UnitZ * 10), Actor(101, 0, center + Vector3.UnitX * 15)], full: true, party: party),
            Icon(.1), TreasureNativeEffect(.2, 1, 12207, 1, 100, [100]),
            Icon(.4), Frame(territory, .6, [Actor(100, 0, center - Vector3.UnitZ * 10)], party: party),
            TreasureNativeEffect(.7, 1, 12207, 2, 100, [100]),
            Frame(territory, 1, [Actor(2, 0x233C, center, cast: Cast(12208, 2, center))], party: party),
            Frame(territory, 4, [Actor(2, 0x233C, center)], party: party),
            TreasureNativeEffect(4.5, 2, 12204, 3, 0, []),
            Frame(territory, 5, [], party: party)]);
        controller.Seek(.3);
        Check(Read(Encounter(), "KenkoTargetPool") == null && Shown("kenko/pool").Length == 0,
            "Early icon captured a pool created later.");
        controller.Seek(.5);
        Check((bool)Read(Shown("kenko/pool").Single(), "Safe") && Snapshot("GoalAreas").Length > 0 && dangers() == 0,
            "Inverse pool must be the existing R3 soft goal.");
        var goalGeometry = Read(Shown("kenko/pool").Single(), "Geometry");
        Check((float)Read(goalGeometry, "Radius") == 3, "Inverse pool radius.");
        var captured = (Vector3)Read(goalGeometry, "Position");
        controller.Seek(.8);
        Check((Vector3)Read(Read(Shown("kenko/pool").Single(), "Geometry"), "Position") == captured, "Captured inverse pool moved.");
        controller.Seek(1.5);
        Check(!(bool)Read(Shown("kenko/pool").Single(), "Safe") && dangers() == 1 && Snapshot("GoalAreas").Length == 0,
            "Ululation did not switch goal to timed danger.");
        var window = ((IEnumerable)Read(Snapshot("DangerAreas").Single(), "DangerWindows")).Cast<object>().Single();
        Check(Math.Abs(((DateTime)Read(window, "DangerFrom") - Start.AddSeconds(7.9)).TotalSeconds) < .001,
            "Pool effect processing clock plus7.2 lost.");
        controller.Seek(4.2);
        Check((bool)Read(Shown("kenko/pool").Single(), "Safe"), "Real Finish did not restore captured goal.");
        controller.Seek(4.8);
        Check(Read(Encounter(), "KenkoTargetPool") == null && Shown("kenko/pool").Length == 1
            && !(bool)Read(Shown("kenko/pool").Single(), "Safe"), "Devour cleared normal pool.");
        controller.Seek(.5);
        Check((bool)Read(Shown("kenko/pool").Single(), "Safe"), "Reverse Seek did not rebuild both pools.");
        Isolated();
        stop();
        Check(previews().Length == 0 && dangers() == 0, "Pool owner stop retained output.");
        Console.WriteLine("PASS native HoH70 independent normal inverse pools captured target icon early icon native effect clock ululation soft goal danger window reverse Seek owner");

        load(id, [Header(territory), Frame(territory, 0, [Actor(1, 0x23EB, center, combat: true),
            Actor(2, 0x233C, center), Actor(100, 0, center + Vector3.UnitZ * 15),
            Actor(101, 0, center + Vector3.UnitX * 20)], full: true, party: party),
            Frame(territory, .1, [Actor(2, 0x233C, center, cast: Cast(12206, 3, center, target: 101))], party: party),
            Frame(territory, .5, [Actor(3, 0x233C, center - Vector3.UnitZ,
                cast: Cast(12206, 4, center, target: 100))], party: party),
            Frame(territory, 1, [Actor(101, 0, center + Vector3.UnitX * 12)], party: party),
            Frame(territory, 2, [Actor(2, 0x233C, center), Actor(3, 0x233C, center - Vector3.UnitZ)], party: party),
            Frame(territory, 3, [], removed: [101], party: party),
            Frame(territory, 3.2, [Actor(101, 0, center - Vector3.UnitZ * 20)], party: party),
            TreasureNativeEffect(4, 2, 12204, 1, 0, []), Frame(territory, 5, [], party: party)]);
        controller.Seek(.3);
        Check(Shown("kenko/charge/").Length == 1 && dangers() == 1 && Values("PredictedDamage").Length == 0,
            "Non bait viewer received Hound navigation/prediction.");
        controller.Seek(.8);
        Check(Shown("kenko/charge/").Length == 2 && Shown("kenko/devour/").Length == 2
            && dangers() == 4 && Values("PredictedDamage").Length == 2, "Target gate did not enable the entire charge component.");
        Check(Shown("kenko/charge/").Count(p => (bool)Read(p, "OutlineOnly")) == 1, "Own charge outline/other filled.");
        var predictions = Values("PredictedDamage");
        Check(predictions.All(p => (DateTime)Read(p, "Activation") == (DateTime)Read(predictions[0], "Activation")),
            "Charge predictions must use first stored bait clock.");
        controller.Seek(1.5);
        var otherCharge = Shown("kenko/charge/").Single(p => !(bool)Read(p, "OutlineOnly"));
        Check(MathF.Abs((float)Read(Read(otherCharge, "Geometry"), "Length") - 12) < .04f,
            "Charge geometry did not follow target.");
        controller.Seek(2.5);
        Check(Values("KenkoCharges").Length == 0 && Values("KenkoDevours").Length == 2
            && Shown("kenko/charge/").Length == 0 && Shown("kenko/devour/").Length == 2, "Finish coupled independent bait lists.");
        controller.Seek(3.5);
        var oldTarget = Read(Values("KenkoDevours")[0], "Target");
        Check(MathF.Abs(((Vector3)Read(Read(oldTarget, "Actor"), "Position")).X + 288) < .04f,
            "Destroyed Devour target rebound to new actor with same ID.");
        controller.Seek(4.5);
        Check(Values("KenkoDevours").Length == 0 && Shown("kenko/devour/").Length == 0, "Devour native effect failed to clear all.");
        controller.Seek(.8);
        Check(dangers() == 4 && Values("KenkoCharges").Length == 2, "Bait reverse Seek did not rebuild.");
        Isolated();
        stop();
        Console.WriteLine("PASS native HoH70 independent charge devour activation target whole component gate source references dynamic rectangles real Finish native effect reverse Seek replay isolation");

        load(id, [Header(territory), Frame(territory, 0, [Actor(1, 0x23EB, center, combat: true),
            Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 10)], full: true, party: party),
            Frame(territory, .1, [Actor(2, 0x233C, center, cast: Cast(12207, 3, center, target: 101))], party: party),
            Frame(territory, 1, [Actor(2, 0x233C, center)], party: party), Frame(territory, 2, [], party: party)]);
        controller.Seek(.5);
        var spreadRadius = (float)Read(Read(Shown("quest/spread/").Single(), "Geometry"), "Radius");
        var spreadDangerRadius = (float)Read(Read(Snapshot("DangerAreas").Single(), "Geometry"), "Radius");
        var settings = Script().GetType().Assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!
            .GetProperty("Instance")!.GetValue(null)!;
        var dangerMargin = (float)Read(settings, "DodgeDangerMargin");
        Check((float)Read(Read(Shown("quest/spread/").Single(), "Geometry"), "Radius") == 3 && dangers() == 1
            && Values("PredictedDamage").Length == 0
            && MathF.Abs(spreadDangerRadius - (4 + dangerMargin)) < .001,
            $"Spread R3 display/R4 navigation mismatch: display={spreadRadius}, danger={spreadDangerRadius}, predictions={Values("PredictedDamage").Length}.");
        controller.Seek(1.5);
        Check(dangers() == 0 && Shown("quest/spread/").Length == 0, "Spread Finish retained.");
        stop();
        Console.WriteLine("PASS native HoH70 spread display R3 navigation R4 no prediction real Finish double view owner");

        foreach (var consumed in new[] { false, true })
        {
            var records = new List<string> { Header(territory), Frame(territory, 0,
                [Actor(1, 0x23EB, center, combat: true), Actor(2, 0x233C, center),
                    Actor(100, 0, center + Vector3.UnitZ * 15), Actor(101, 0, center + Vector3.UnitX * 20)], full: true, party: party),
                CastEvent(.1, 2, 12206, 3, center, target: 100) };
            if (consumed) records.Add(TreasureNativeEffect(.15, 3, 12204, 1, 0, []));
            records.Add(Frame(territory, .2, [Actor(2, 0x233C, center, cast: Cast(12206, 3, center, 1, 101))], party: party));
            records.Add(Frame(territory, .4, [Actor(2, 0x233C, center, cast: Cast(12206, 3, center, 2, 101))], party: party));
            records.Add(Frame(territory, .6, [Actor(2, 0x233C, center)], party: party));
            records.Add(Frame(territory, 1, [], party: party));
            load(id, records);
            controller.Seek(.3);
            Check(Values("KenkoCharges").Length == 1 && Values("KenkoDevours").Length == (consumed ? 0 : 1),
                "First raw confirmation duplicated or resurrected Devour / lost active charge.");
            controller.Seek(.5);
            Check(Values("KenkoCharges").Length == 1 && Values("KenkoDevours").Length == (consumed ? 1 : 2),
                "Real subsequent raw Start did not preserve independent Devour generations.");
            controller.Seek(.8);
            Check(Values("KenkoCharges").Length == 0 && Values("KenkoDevours").Length == (consumed ? 1 : 2),
                "Actual raw Finish cleared persistent Devour.");
            stop();
            Console.WriteLine($"PASS native HoH70 first raw confirmation consumed={consumed} preserves charge prevents duplicate resurrection subsequent real raw replacement adds independent Devour");
        }
    }
}
