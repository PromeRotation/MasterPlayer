using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivaleAzulmagia(object managerObject, object runtimeObject, object controllerObject,
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
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)
            && (prefix != "cast/" || !((string)Value(a, "Id")).Contains("carnivale/"))).ToArray();
        int Obstacles() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [center])!).Cast<string>().ToArray();
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed, party: [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }]);
        string Cancel(double t, ulong source, uint action)
            => JsonSerializer.Serialize(new { t, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(t) } });
        object[] Initial(uint oid = 0x2680, bool combat = true) => [Actor(1, oid, center, combat: combat),
            Actor(2, 0x233C, center), Actor(3, 0x233C, center),
            Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 10)];
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            Check(!((IEnumerable)hints.GetType().GetField("forbiddenTargets", flags)!.GetValue(hints)!).Cast<object>().Any()
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld")
                && Values("PredictedDamage").Length == 0, "Azulmagia invented restrictions/hold/damage.");
        }
        foreach (var (oid, name) in new[] { (0x2678u, "邪恶青魔法师——启示者·第1幕"), (0x267Fu, "邪恶青魔法师——启示者·第2幕"), (0x2680u, "邪恶青魔法师——启示者·第3幕") })
        {
            load(guid, [Header(796), At(0, Initial(oid, false), true),
                At(.1, [Actor(1, oid, center, combat: true), Actor(4, 0x2682, center + Vector3.UnitX * 3)]),
                At(1, [Actor(1, oid, center, dead: true)]), At(2, [])]);
            controller.Seek(.05);
            Check(Value(Encounter(), "Active") == null && Hints().Length >= 1 && dangers() == 0, "Azulmagia preparation.");
            controller.Seek(.5);
            var source = Assembly.Load("MasterPlayer.MechanicRuntime");
            var bounds = source.GetType("BossMod.Global.MaskedCarnivale.Layouts")!.GetField(oid == 0x2680 ? "CircleSmall" : "CircleBig")!.GetValue(null)!;
            var wdir = source.GetType("BossMod.WDir")!;
            dynamic arena = Value(Encounter(), "Arena");
            var count = 0;
            for (var x = 74.13f; x < 126; x += .67f)
                for (var z = 74.17f; z < 126; z += .71f)
                {
                    var expected = (bool)bounds.GetType().GetMethod("Contains")!.Invoke(bounds,
                        [Activator.CreateInstance(wdir, [x - 100, z - 100])])!;
                    Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, "Azulmagia source arena.");
                    ++count;
                }
            Check(Shown("carnivale/enemy/").Length == (oid == 0x267F ? 2 : 1), "Azulmagia markers.");
            controller.Seek(1.5);
            Check(Value(Encounter(), "Active") == null && dangers() == 0, "Angon kept Primary alive.");
            controller.Seek(.5);
            manager.SetMechanismEnabled(guid, name, false);
            controller.Seek(.6);
            Check(previews().Length == 0 && dangers() == 0 && Obstacles() == 0, "Azulmagia disabled owner.");
            manager.SetMechanismEnabled(guid, name, true);
            stop();
            Console.WriteLine($"PASS native Carnivale Stage25 {oid:X} source arena oracle {count} points default lifecycle markers live Angon no keepalive reverse Seek owner");
        }

        foreach (var oid in new uint[] { 0x2678, 0x267F, 0x2680 })
        {
            var actions = oid == 0x267F ? new uint[] { 14766, 14767, 14763, 14764 }
                : oid == 0x2678 ? [14766, 14767, 14763, 14764, 14765, 14774, 14775]
                : new uint[] { 14766, 14767, 14763, 14764, 14765, 14774, 14775, 14773, 14771 };
            foreach (var action in actions)
            {
                load(guid, [Header(796), At(0, Initial(oid), true),
                    At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center, rotation: .4f))]),
                    EffectEvent(1, 2, action, 1), At(8, []), Cancel(9, 2, action), At(10, [])]);
                controller.Seek(.5);
                Check(dangers() >= 1 && Shown("cast/").Length == 1,
                    $"Azulmagia ordinary output oid={oid:X} action={action} dangers={dangers()} casts={Shown("cast/").Length}.");
                controller.Seek(8.5);
                Check(Shown("cast/").Length == 1, "Azulmagia ordinary effect/timeout.");
                controller.Seek(9.5);
                Check(Shown("cast/").Length == 0, "Azulmagia ordinary Finish.");
                controller.Seek(.5);
                stop();
            }
        }
        Console.WriteLine("PASS native Carnivale Stage25 twenty ordinary instances anomalous first width90 other width8 rings R8 R7.2 cone36.2 R15 real Finish dual outputs navigation reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(14771, 4, center, target: 101))]),
            At(1, [Actor(101, 0, center + Vector3.UnitX * 14)]),
            At(2, [], removed: [101]), Cancel(3, 2, 14771), At(4, [])]);
        controller.Seek(.5);
        var fixedPosition = (Vector3)Value(Value(Shown("cast/").Single(), "Geometry"), "Position");
        var finish = (DateTime)Value(Value(Values("CarnivaleRayCasts").Single(), "Cast"), "FinishAt");
        controller.Seek(1.5);
        Check((Vector3)Value(Value(Shown("carnivale/ray/cast/").Single(), "Geometry"), "Position") == center + Vector3.UnitX * 14
            && (Vector3)Value(Value(Shown("cast/").Single(), "Geometry"), "Position") == fixedPosition
            && (DateTime)Value(Value(Values("CarnivaleRayCasts").Single(), "Cast"), "FinishAt") == finish
            && dangers() == 2, "Independent Meteor lock/dynamic target.");
        controller.Seek(2.5);
        Check((Vector3)Value(Value(Shown("carnivale/ray/cast/").Single(), "Geometry"), "Position") == center, "Meteor raw fallback.");
        controller.Seek(3.5);
        Check(dangers() == 0 && Values("CarnivaleRayCasts").Length == 0, "Meteor cast Finish.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage25 Meteor R15 fixed and R10 dynamic target independent clocks missing target fallback navigation actual Finish reverse Seek");

        var pos = center + Vector3.UnitX * 10;
        load(guid, [Header(796), At(0, Initial(), true),
            EffectEvent(.1, 2, 14771, 1, center, target: 100),
            TreasureNativeEffect(.1, 2, 14771, 1, 101, [100, 101], center + Vector3.UnitZ * 20),
            TreasureNativeEffect(.2, 2, 14771, 2, 101, []), Cancel(.3, 2, 14771),
            At(10, []), At(11, [Actor(4, 0x1EA8BB, pos + Vector3.UnitX * .12345f, dead: true)]),
            At(12, [Actor(4, 0x1EA8BB, pos, dead: true, eventState: 7)]), At(13, [])]);
        controller.Seek(.15);
        var prediction = Values("CarnivaleRayPredictions").Single();
        Check((Vector3)Value(prediction, "Position") == pos
            && (DateTime)Value(prediction, "Activation") == ((DateTime)Value(prediction, "ObservedAt")).AddSeconds(1.2d)
            && dangers() == 1 && Shown("carnivale/ray/event/").Length == 1, "Native Meteor main target/delay/legacy duplicate.");
        controller.Seek(10.5);
        Check(Values("CarnivaleRayPredictions").Length == 2 && dangers() == 2, "Meteor effect Finish/timeout.");
        controller.Seek(11.5);
        Check(Shown("carnivale/ray/live/").Length == 1 && Obstacles() == 0
            && (Vector3)Value(Value(Shown("carnivale/ray/live/").Single(), "Geometry"), "Position") != pos + Vector3.UnitX * .12345f,
            "Dead lava quantized navigation.");
        controller.Seek(12.5);
        Check(Shown("carnivale/ray/live/").Length == 0, "State7 lava remained.");
        controller.Seek(.15);
        stop();
        Console.WriteLine("PASS native Carnivale Stage25 native Meteor main target 1.2d processing clock no Start legacy dedup no timeout event pool quantized nonobstacle live dead state7 reverse Seek");

        var raw = center + new Vector3(1.12345f, 0, 2.37654f);
        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(4, 0x2681, raw, rotation: .7f, dead: true, targetable: false, eventState: 7)]),
            Cancel(1, 4, 14780), At(2, [Actor(4, 0x2681, raw + Vector3.UnitX * 2, dead: true, eventState: 7)]),
            At(3, [], removed: [4]), At(4, [])]);
        controller.Seek(.5);
        Check(Obstacles() == 1 && dangers() == 0 && Shown("carnivale/maelstrom/").Length == 1
            && (Vector3)Value(Value(Shown("carnivale/maelstrom/").Single(), "Geometry"), "Position") == raw, "Raw Maelstrom.");
        controller.Seek(2.5);
        Check(Obstacles() == 1, "Maelstrom Finish removed entity.");
        controller.Seek(3.5);
        Check(Obstacles() == 0, "Maelstrom destroy retained.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage25 raw moving R8 Maelstrom hard obstacle includes dead untargetable state7 no cast dependency real destroy dual outputs reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(14770, 8, center, target: 100))]),
            At(.2, [Actor(3, 0x233C, center, cast: Cast(14770, 8, center, target: 101, startedAt: .2))]),
            Cancel(1, 2, 14770),
            JsonSerializer.Serialize(new { t = 1.1, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent", packetFacing = 0f,
                data = new { SourceId = 3ul, TargetId = 101ul, ActionId = 14770, CastTime = 8f,
                    EffectPosition = Pose(center + Vector3.UnitX * 3), Timestamp = Start.AddSeconds(.2) } }), At(2, [])]);
        controller.Seek(.5);
        Check(Hints().Any(h => h.Contains("Bait the Meteor")) && dangers() == 0, "Web missing.");
        Isolated();
        controller.Seek(1.5);
        Check(!Hints().Any(h => h.Contains("Bait the Meteor")), "Web changed to count.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage25 Web global bool any matching source Finish cancels overlapping hints no teleport movement hold actions or damage reverse Seek");

        load(guid, [Header(796), At(0, [Actor(1, 0x2680, center, combat: true, stackedStatus: 556, statusExpiresAt: Start),
            Actor(100, 0, center, stackedStatus: 910, statusExpiresAt: Start)], true),
            At(10, []), At(11, [Actor(1, 0x2680, center, combat: true, stackedStatus: 1307, statusExpiresAt: Start)]),
            At(12, [Actor(1, 0x2680, center, combat: true), Actor(100, 0, center)]), At(13, [])]);
        controller.Seek(10.5);
        Check(Hints().Any(h => h.Contains("reflect all magic")) && Hints().Any(h => h.StartsWith("You were doomed!")),
            "Expired occupied reflection/Doom slots filtered by replay.");
        Isolated();
        controller.Seek(11.5);
        Check(Hints().Any(h => h.Contains("reflect all physical")) && !Hints().Any(h => h.Contains("reflect all magic")), "Reflection transition.");
        controller.Seek(12.5);
        Check(!Hints().Any(h => h.Contains("reflect all") || h.StartsWith("You were doomed!")), "Cleared statuses retained.");
        controller.Seek(10.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage25 occupied raw status slots retain expired reflection and Doom until real removal transition reverse Seek no attack restriction");

        load(guid, [Header(796), At(0, Initial(0x2680, false), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(14771, 1, center, target: 101))]),
            Cancel(.2, 2, 14771), At(10, [Actor(1, 0x2680, center, combat: true)]), At(11, [])]);
        controller.Seek(10.5);
        Check(dangers() == 0 && Values("CarnivaleRayCasts").Length == 0, "Canceled pre-pull Meteor revived.");
        Isolated();
        controller.Seek(.1);
        controller.Seek(10.5);
        Check(dangers() == 0, "Canceled pre-pull Meteor Seek.");
        stop();
        Console.WriteLine("PASS native Carnivale Stage25 preactivation Meteor cancel watermark blocks both components after pull and reverse Seek without execution controls");
    }
}
