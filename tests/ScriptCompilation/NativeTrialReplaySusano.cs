using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunSusano(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object Value(object a, string name) => a.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(a)!;
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        string IconEvent(double t, ulong actor, uint icon) => JsonSerializer.Serialize(new { t, kind = "TargetIcon",
            type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = actor, TargetId = actor, MarkerId = icon, Timestamp = Start.AddSeconds(t) } });
        var party = new object[] { new { Slot = 0, Role = "Tank", EntityId = 100UL }, new { Slot = 1, Role = "Tank", EntityId = 101UL } };
        const string id = "masterplayer.trial.susano";
        load(id,
        [
            Header(674), Frame(674, 0, [Actor(1, 0x1AF7, Vector3.Zero, combat: true),
                Actor(100, 0, Vector3.UnitZ * 5, classJob: 19), Actor(101, 0, Vector3.UnitZ * 10, classJob: 21),
                Actor(10, 0x1BA1, Vector3.Zero), Actor(11, 0x1BA1, Vector3.Zero),
                Actor(12, 0x1B9B, Vector3.UnitX * 10)], full: true, party: party),
            IconEvent(.1, 100, 62), IconEvent(.2, 101, 230),
            Frame(674, 1, [Actor(10, 0x1BA1, Vector3.Zero, cast: Cast(8232, 3, Vector3.Zero, startedAt: 1)),
                Actor(11, 0x1BA1, Vector3.Zero, cast: Cast(8233, 5, Vector3.Zero, startedAt: 1)),
                Actor(1, 0x1AF7, Vector3.Zero, combat: true, cast: Cast(8223, 4, Vector3.Zero, startedAt: 1))], party: party),
            Frame(674, 2, [Actor(1, 0x1AF7, Vector3.Zero, targetable: false,
                cast: Cast(8223, 4, Vector3.Zero, startedAt: 1))], party: party),
            EffectEvent(3, 1, 8224, 1, target: 100), EffectEvent(3.5, 1, 8227, 2),
            Frame(674, 4.4, [Actor(10, 0x1BA1, Vector3.Zero)], party: party),
            Frame(674, 5.4, [Actor(1, 0x1AF7, Vector3.Zero, targetable: false)], party: party),
            Frame(674, 6.4, [Actor(11, 0x1BA1, Vector3.Zero), Actor(12, 0x1B9B, Vector3.UnitX * 10, dead: true)], party: party),
            Frame(674, 7, [Actor(1, 0x1AF7, Vector3.Zero, dead: true, targetable: false)], party: party),
            Frame(674, 8, [], party: party),
        ]);
        controller.Seek(.5);
        Check(Group("susano/stack/1/buddy").Length == 1 && Group("susano/tank/position").Length == 1,
            "Susano icon positions missing.");
        Check((DateTime)Value(Group("susano/tank/position").Single(), "Activation") == Start.AddSeconds(.2).AddSeconds(5.1),
            "Susano icon TB clock changed.");
        var hints = Script().GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        var facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
        Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!,
            "Susano replay executed PR requests.");
        controller.Seek(2.5);
        Check(Value(Encounter(), "Active") != null && dangers() >= 2 && Group("susano/knockback").Length == 2,
            "Susano hidden primary or independent casts lost.");
        controller.Seek(3.2);
        Check(!Group("susano/stack").Any() && Group("susano/tank").Any(), "Susano stack effect cleared TB.");
        controller.Seek(3.7);
        Check(!Group("susano/tank").Any(), "Susano TB effect leaked.");
        controller.Seek(4.7); Check(dangers() == 1 && Group("susano/knockback").Any(), "Susano first sea splitter cleared all casts.");
        controller.Seek(5.7); Check(!Group("susano/knockback").Any() && dangers() == 1, "Susano knockback real finish lost.");
        controller.Seek(6.7); Check(dangers() == 0 && !Group("susano/orb").Any(), "Susano final cast/orb cleanup failed.");
        controller.Seek(7.5); Check(previews().Length == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "Susano death leaked output.");
        controller.Seek(.5); Check(Group("susano/stack").Any() && Group("susano/tank").Any(), "Susano reverse Seek lost icons.");
        manager.SetScriptEnabled(id, false);
        Check(dangers() == 0 && previews().Length == 0, "Susano disable leaked."); stop();
        Console.WriteLine("PASS Susano actual DLL replay icon clocks, independent sea splits and knockback, hidden sword phase, real ends, death, Seek and PR isolation");
    }
}
