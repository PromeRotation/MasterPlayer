using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunPalacePlants(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Read(object obj, string name)
        {
            var property = obj.GetType().GetProperty(name, flags);
            return property != null ? property.GetValue(obj)! : obj.GetType().GetField(name, flags)!.GetValue(obj)!;
        }
        var center = new Vector3(-300, 0, -236);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        var bmr = Assembly.Load("MasterPlayer.MechanicRuntime");
        foreach (var (name, territory, oid, hornet, actions, boundsName) in new[]
        {
            ("spurge", 562u, 0x169Fu, 0x1763u, new uint[] { 6422, 6423, 6424, 6425 }, "ArenaBounds2090110"),
            ("kirtimukha", 599u, 0x1819u, 0x1905u, new uint[] { 7134, 7135, 7136, 7137, 919 }, "ArenaBounds120130"),
        })
        {
            var id = $"masterplayer.palace.{name}";
            object Script() => Read(manager.Running[id], "Script");
            object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
            object[] Values(string property) => ((IEnumerable)Read(Encounter(), property)).Cast<object>().ToArray();
            object[] Shown(string prefix) => previews().Where(p => ((string)Read(p, "Id")).Contains(prefix)).ToArray();
            object[] Damage() => Values("PredictedDamage");
            var records = new List<string> { Header(territory), Frame(territory, 0,
                [Actor(1, oid, center, targetId: 100), Actor(2, hornet, center + Vector3.UnitZ * 4),
                    Actor(100, 0, center + Vector3.UnitX * 10), Actor(101, 0, center + Vector3.UnitZ * 15)], full: true, party: party),
                Frame(territory, .1, [Actor(1, oid, center, combat: true, targetId: 100)], party: party),
                Frame(territory, 1, [Actor(1, oid, center, combat: true, rotation: 2, targetId: 101)], party: party),
                Frame(territory, 2, [Actor(1, oid, center, combat: true, targetId: 100,
                    cast: Cast(99999, 1, center, startedAt: 2))], party: party),
                Frame(territory, 3, [Actor(1, oid, center, combat: true, targetId: 100)], party: party) };
            records.Add(Frame(territory, 4, actions.Select((action, i) => Actor((ulong)(10 + i),
                0x233C, center, cast: Cast(action, 2, center, .7f, target: 101, startedAt: 4))).ToArray(), party: party));
            records.Add(EffectEvent(4.2, 10, actions[0], 1));
            records.Add(Frame(territory, 8, [], party: party));
            records.Add(Frame(territory, 9, actions.Select((_, i) => Actor((ulong)(10 + i), 0x233C, center)).ToArray(), party: party));
            records.Add(Frame(territory, 9.7, [Actor(10, 0x233C, center, dead: true,
                cast: Cast(actions[0], 3, center, startedAt: 9.7))], party: party));
            records.Add(Frame(territory, 10, [Actor(1, oid, center, dead: true)], party: party));
            records.Add(Frame(territory, 11, [], party: party));
            load(id, records);
            controller.Seek(.01);
            Check(Read(Encounter(), "Active") == null && dangers() == 0 && Shown("palace-plant/precombat/").Length == 2,
                "Palace precombat navigation/preview scope.");
            controller.Seek(.5);
            var own = Shown("palace-plant/cleave/").Single();
            Check(dangers() == 1 && (bool)Read(own, "OutlineOnly") && (float)Read(Read(own, "Geometry"), "Radius") == 11.6f,
                "Dynamic cleave exact own outline/navigation.");
            Check((ulong)Encounter().GetType().GetMethod("PriorityTarget")!.Invoke(Encounter(), [1ul, center])! == 2
                && Values("ForbiddenDotTargets").Cast<ulong>().SequenceEqual([2ul])
                && !Values("ForbiddenTargets").Cast<ulong>().Contains(2ul), "Hornet priority2 / DoT intention expanded to attack ban.");
            Check(Shown("hw-intro/enemy/").Length == 2, "Hornet marker missing.");
            dynamic arena = Read(Encounter(), "Arena");
            var bounds = bmr.GetType("BossMod.Heavensward.DeepDungeon.PalaceOfTheDead.SharedBounds")!
                .GetField(boundsName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            var sourceCenter = Read(bounds, "Center");
            var sx = (float)Read(sourceCenter, "X");
            var sz = (float)Read(sourceCenter, "Z");
            Check((Vector3)Read((object)arena, "Position") == new Vector3(sx, 0, sz), "Palace source custom center.");
            var directionType = bmr.GetType("BossMod.WDir")!;
            var contains = bounds.GetType().GetMethod("Contains")!;
            var points = 0;
            for (var x = -326.137f; x < -274; x += .67f)
                for (var z = -262.173f; z < -210; z += .71f)
                {
                    var offset = Activator.CreateInstance(directionType, [x - sx, z - sz])!;
                    Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == (bool)contains.Invoke(bounds, [offset])!,
                        "Palace arena differs from source oracle.");
                    ++points;
                }
            var hostType = Script().GetType().Assembly.GetType("MasterPlayer.MechanicHost.MechanicHostController")!;
            Check((bool)hostType.GetMethod("IsReplaySuppressed", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(RuntimeHelpers.GetUninitializedObject(hostType), null)!, "Palace territory still accepts the migrated floor bridge.");
            controller.Seek(1.5);
            Check(Shown("palace-plant/cleave/").Length == 1
                && MathF.Abs((float)Read(Read(Shown("palace-plant/cleave/").Single(), "Geometry"), "Rotation")) < .001f,
                "Cleave followed boss rotation instead of current target.");
            controller.Seek(2.5);
            Check(dangers() == 0 && Shown("palace-plant/cleave/").Length == 0, "Arbitrary cast did not hide cleave.");
            controller.Seek(4.5);
            Check(dangers() == 4 && Shown("palace-plant/cleave/").Length == 1 && Damage().Length == (name == "spurge" ? 2 : 3),
                "Ordinary shapes/Raidwide/FinalSting split.");
            controller.Seek(8.5);
            Check(dangers() == 4 && Damage().Length == (name == "spurge" ? 2 : 3)
                && Damage().All(d => (DateTime)Read(d, "Activation") > runtime.CurrentTime),
                "Deadline/effect ended observed cast or saturated NPC prediction.");
            controller.Seek(9.5);
            Check(dangers() == 1 && Damage().Length == 0, "Actual Finish did not clean ordinary casts.");
            controller.Seek(9.75);
            var handles = Script().GetType().BaseType!.GetMethod("HandlesCast", flags)!;
            var castEvent = Activator.CreateInstance(handles.GetParameters()[0].ParameterType)!;
            castEvent.GetType().GetProperty("SourceId")!.SetValue(castEvent, 10ul);
            castEvent.GetType().GetProperty("ActionId")!.SetValue(castEvent, (ushort)actions[0]);
            Check((bool)handles.Invoke(Script(), [castEvent])! && dangers() == 2,
                "Native dead helper escaped generic-dodge claim.");
            controller.Seek(10.5);
            Check(Read(Encounter(), "Active") == null && dangers() == 0, "Living hornet held dead primary phase.");
            controller.Seek(4.5);
            Check(dangers() == 4, "Reverse Seek did not reconstruct.");
            var hints = Read(Script(), "Hints");
            Check(!(bool)Read(Read(hints, "_facing"), "_hasRequest")
                && !(bool)Read(Read(runtimeObject, "Setting"), "RuntimeMovementHeld")
                && !((IEnumerable)Read(hints, "forbiddenTargets")).Cast<object>().Any(), "Palace replay executed native control.");
            stop();
            Console.WriteLine($"PASS native Palace {name} source arena {points} points exact ordinary casts observed end dynamic cleave target party navigation unknown cast hornet priority DoT model markers damage NPC clock reverse Seek owner replay floor isolation");
        }
    }
}
