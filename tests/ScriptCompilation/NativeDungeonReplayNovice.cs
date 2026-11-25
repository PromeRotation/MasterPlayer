using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunNovice(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        object Value(object obj, string name) => obj.GetType().GetProperty(name, hidden)!.GetValue(obj)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Hints() => Value(Script(), "Hints");
        ulong Planned()
        {
            var script = Script();
            var encounter = script.GetType().BaseType!.GetField("_encounter", hidden)!.GetValue(script)!;
            var player = Value(script, "CurrentPlayer");
            return (ulong)encounter.GetType().GetMethod("PriorityTarget", hidden)!
                .Invoke(encounter, [(ulong)Value(player, "TargetId"), (Vector3)Value(player, "Position")])!;
        }
        bool Active() => (bool)Value(Script(), "EncounterActive");
        object Npc(ulong id, uint oid, Vector3 position, bool combat, ulong target = 100, bool targetable = true, uint hp = 1000)
        {
            var node = JsonSerializer.SerializeToNode(Actor(id, oid, position, combat: combat, targetId: target,
                targetable: targetable, objectIndex: (ushort)(id * 2), isEnemy: true))!;
            node["AggroPlayer"] = combat;
            node["Hp"] = hp;
            return node;
        }
        object Gladiator(ulong target)
            => Actor(20, 0x1574, default, targetId: target, isEnemy: false, objectIndex: 40, subKind: 5);
        void Isolated()
        {
            var hints = Hints();
            Check(((IEnumerable)hints.GetType().GetField("forbiddenTargets", hidden)!.GetValue(hints)!).Cast<ulong>().Count() == 0,
                "Novice warning expanded into forbidden targets.");
            var facing = hints.GetType().GetField("_facing", hidden)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", hidden)!.GetValue(facing)!
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld"),
                "Novice replay leaked PR or movement control.");
        }
        foreach (var (territory, oid, guid, mechanism, center) in new[]
        {
            (545u, 0x1571u, "masterplayer.novice.dps03", "NoviceDPS03", Vector3.Zero),
            (547u, 0x157Au, "masterplayer.novice.dps05", "NoviceDPS05", Vector3.Zero),
            (552u, 0x1591u, "masterplayer.novice.basic-final", "NoviceBasicFinal", new Vector3(457.383f, 0, 271.721f)),
        })
        {
            var direction = new Vector3(MathF.Sin(MathF.PI / 64), 0, MathF.Cos(MathF.PI / 64));
            var alternate = territory == 552 ? oid : 0xABCDEFu;
            load(guid,
            [
                Header(territory), Frame(territory, 0, [Npc(1, oid, center + new Vector3(30, 0, 0), false),
                    Actor(100, 0, center, targetId: 1), Gladiator(2)], full: true),
                Frame(territory, .1, [Npc(1, oid, center + direction * 30, true), Npc(2, alternate, center + Vector3.UnitX * 5, false)]),
                Frame(territory, 1, [Npc(1, oid, center, false, targetable: false)]),
                Frame(territory, 2, [Npc(1, oid, center, false, hp: 0)], removed: [2]),
                Frame(territory, 3, [Npc(1, oid, center, true)]),
                Frame(territory, 4, [], removed: [1]),
                Frame(territory, 5, [Npc(1, oid, center, true)]), Frame(territory, 6, []),
            ]);
            controller.Seek(0);
            Check(!Active() && Planned() == 0 && dangers() == 0 && previews().Length == 2
                && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
                "Novice precombat lost display or created navigation/target before pull.");
            controller.Seek(.2);
            Check(Active() && Planned() == (territory == 552 ? 1ul : 2ul)
                && dangers() == 0 && previews().Length == 1,
                $"Novice {territory} actual output failed: active={Active()}, target={Planned()}, dangers={dangers()}, previews={previews().Length}.");
            object? boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
            Check(boundary != null && (Vector3)Value(boundary, "Position") == center, "Novice native arena center differs.");
            Check(Vector3.Distance((Vector3)Value(Value(previews().Single(), "Geometry"), "Position"), center + direction * 20) < .0001f,
                "Active primary preview used polygonal rather than exact radial projection.");
            Isolated();
            manager.SetMechanismEnabled(guid, mechanism, false);
            controller.Seek(.4);
            Check(!Active() && Planned() == 0 && previews().Length == 0
                && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "Novice mechanism off retained outputs.");
            manager.SetMechanismEnabled(guid, mechanism, true);
            controller.Seek(.6);
            Check(Active(), "Novice re-enable did not rebuild.");
            controller.Seek(1.2);
            Check(Active() && previews().Length == 0 && Planned() == 2,
                "Novice lost combat latch or painted untargetable primary.");
            controller.Seek(3.2);
            Check(!Active() && Planned() == 0 && previews().Length == 0,
                "Novice HP0-ended generation restarted without Remove/Create.");
            controller.Seek(5.2);
            Check(Active() && previews().Length == 1, "Novice same-ID new generation stayed blocked.");
            controller.Seek(0);
            Check(!Active() && previews().Length == 2 && Planned() == 0, "Novice reverse Seek failed.");
            Isolated();
            stop();
            Check(previews().Length == 0 && dangers() == 0
                && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "Novice stop retained owner outputs.");
            Console.WriteLine($"PASS native novice production {territory} original precombat dual views without navigation exact arena priority setter combat latch untargetable HP0 completion new generation mechanism off replay isolation reverse Seek owner cleanup");
        }
        object PredictedAdd(bool heal)
        {
            var node = JsonSerializer.SerializeToNode(Npc(2, 0xAAAA, default, true, hp: heal ? 1u : 1000u))!;
            node["IncomingEffects"] = JsonSerializer.SerializeToNode(new[]
            {
                new { Slot = 0, GlobalSequence = 1u, TargetIndex = (byte)0, TargetConfirmed = false,
                    Knockback = false, HpDelta = heal ? 10 : -999 }
            });
            return node;
        }
        load("masterplayer.novice.dps05",
        [
            Header(547), Frame(547, 0, [Npc(1, 0x157A, default, true, hp: 0), Actor(100, 0, default, targetId: 2)], full: true),
            Frame(547, 1, [Npc(1, 0x157A, default, true)]),
            Frame(547, 2, [], removed: [1]),
            Frame(547, 3, [Npc(1, 0x157A, default, true), PredictedAdd(false)]),
            Frame(547, 4, [PredictedAdd(true)]), Frame(547, 5, []),
        ]);
        controller.Seek(1.2);
        Check(!Active() && Planned() == 0 && previews().Length == 0, "Initially HP0 generation revived after healing.");
        controller.Seek(3.2);
        Check(Active() && Planned() == 1, "Pending lethal damage was lost by production DTO conversion.");
        controller.Seek(4.2);
        Check(Planned() == 2, "Pending healing did not restore the preferred training target.");
        controller.Seek(.2);
        Check(!Active() && Planned() == 0, "Initial HP0 completion failed reverse rebuild.");
        Isolated();
        stop();
        Console.WriteLine("PASS native novice production initially HP0 completion pending damage healing actual new generation reverse Seek isolation");
        object AlliedBoss(bool status)
        {
            var node = JsonSerializer.SerializeToNode(Npc(1, 0x157A, default, true, target: 20))!;
            node["AggroPlayer"] = false;
            node["Statuses"] = JsonSerializer.SerializeToNode(status
                ? new[] { new { Id = 325, Param = 0, SourceId = 1ul, ExpiresAt = Start.AddSeconds(.5) } } : []);
            return node;
        }
        load("masterplayer.novice.dps05",
        [
            Header(547), Frame(547, 0, [AlliedBoss(false), Actor(100, 0, default, targetId: 1), Gladiator(1)],
                full: true, party: [new { Slot = 0, EntityId = 100ul }]),
            Frame(547, 1, [AlliedBoss(true)]), Frame(547, 2, [AlliedBoss(false)]), Frame(547, 3, []),
        ]);
        controller.Seek(.2);
        Check(Active() && Planned() == 1, "NPC target did not preserve source baseline priority.");
        controller.Seek(1.2);
        Check(Planned() == 1, "Active training module incorrectly inherited automatic invincibility priority.");
        controller.Seek(2.2);
        Check(Planned() == 1, "Actual status removal failed baseline recovery.");
        Isolated();
        stop();
        Console.WriteLine("PASS native novice production friendly NPC party baseline active module ignores auto invincible priority replay control isolation");
    }
}
