using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.Loader;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;

internal static class Pt99TargetControlRegression
{
    public static void Run(string path)
    {
        using var stream = File.OpenRead(path);
        var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
        var actorType = assembly.GetType("MasterPlayer.Replay.ReplayActorState")!;
        var statusType = assembly.GetType("MasterPlayer.Replay.ReplayStatus")!;
        var hostType = assembly.GetType("MasterPlayer.Scripting.IScriptHostContext")!;
        var script = Activator.CreateInstance(assembly.GetType("MasterPlayer.Scripts.PilgrimsTraverse.PilgrimsTraverse99")!)!;
        var attribute = script.GetType().GetCustomAttributes().Single(a => a.GetType().Name == "MechanicScriptAttribute");
        var territories = (uint[])attribute.GetType().GetProperty("Territorys")!.GetValue(attribute)!;
        Check(territories.SequenceEqual(new uint[] { 1290, 1333 }), "The same entry must serve PT99 and Final Verse normal only");
        var metadata = Activator.CreateInstance(assembly.GetType("MasterPlayer.Scripting.ScriptMetadata")!,
            ["promeradar.pt99", "test", territories, "1.0.0", "", "", "", ""]);
        var match = assembly.GetType("MasterPlayer.Scripting.ScriptManager")!.GetMethod("IsTerritoryMatched")!;
        foreach (var territory in new uint[] { 1290, 1333 })
            Check((bool)match.Invoke(null, [metadata, territory])!, "Real script discovery must accept both supported territories");
        foreach (var territory in new uint[] { 1041, 1063, 1065, 1311, 1332, 1334 })
            Check(!(bool)match.Invoke(null, [metadata, territory])!, "CFC IDs, Quantum and unrelated territories must not start this script");
        var catalog = assembly.GetType("MasterPlayer.Mechanics.PilgrimsTraverse.PtBossCatalog")!;
        var owns = catalog.GetMethod("Owns", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check((bool)owns.Invoke(null, [0x48EAu, 1333u])! && !(bool)owns.Invoke(null, [0x48EEu, 1311u])!,
            "Normal trial must deduplicate BMR without suppressing Quantum");
        Check((bool)catalog.GetMethod("IsScript", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, ["promeradar.pt99"])!,
            "Keep the existing script identity and default-enable policy");
        Console.WriteLine("PASS production Final Verse registration: shared entry, actual territory matching, preserved identity and Quantum isolation");
        var scriptType = script.GetType().BaseType!;
        var model = scriptType.GetField("encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        var now = DateTime.UtcNow;
        var center = new Vector3(-600, 0, -300);
        object Actor(ulong id, uint oid, bool player = false)
        {
            var a = Activator.CreateInstance(actorType)!;
            foreach (var (key, value) in new (string, object)[] { ("Id", id), ("Oid", oid), ("Hp", 1000u),
                ("MaxHp", 1000u), ("Position", center), ("Targetable", true), ("InCombat", true), ("Kind", (byte)(player ? 1 : 2)) })
                actorType.GetProperty(key)!.SetValue(a, value);
            return a;
        }
        var pc = Actor(100, 0, true);
        var statuses = Array.CreateInstance(statusType, 1);
        statuses.SetValue(Activator.CreateInstance(statusType, [4560u, (ushort)0, 0ul, now.AddSeconds(30)]), 0);
        actorType.GetProperty("Statuses")!.SetValue(pc, statuses);
        var observations = Array.CreateInstance(actorType, 3);
        observations.SetValue(Actor(1, 0x48EA), 0);
        observations.SetValue(Actor(2, 0x48EB), 1);
        observations.SetValue(pc, 2);
        model.GetType().GetMethod("Synchronize")!.Invoke(model, [observations, now]);
        var replay = false;
        Func<string, object?[], object?> hostInvoke = (name, _) => name switch
        {
            "get_CurrentPlayer" => pc,
            "get_IsReplay" => replay,
            "get_ReplayWorld" => null,
            _ => throw new NotSupportedException(name)
        };
        var host = typeof(CatalogServiceProxy).GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(hostType).Invoke(null, [hostInvoke]);
        scriptType.BaseType!.GetProperty("Host")!.SetValue(script, host);
        var update = scriptType.GetMethod("UpdateTarget", BindingFlags.Instance | BindingFlags.NonPublic)!;
        void Tick() => update.Invoke(script, [now, 100ul]);
        var originalTargets = Svc.Targets;
        var originalObjects = Svc.Objects;
        var writes = 0;
        var reads = 0;
        IBattleNpc Npc(uint id, uint oid) => CatalogServiceProxy.Create<IBattleNpc>((name, _) => name switch
        {
            "get_EntityId" => id, "get_GameObjectId" => (ulong)id, "get_BaseId" => oid,
            "get_Address" => (nint)1, "get_IsDead" => false, "get_IsTargetable" => true, "get_CurrentHp" => 1000u,
            _ => throw new NotSupportedException(name)
        });
        var grief = Npc(1, 0x48EA);
        var eater = Npc(2, 0x48EB);
        IGameObject? current = eater;
        try
        {
            SetService(nameof(Svc.Targets), CatalogServiceProxy.Create<ITargetManager>((name, values) =>
            {
                ++reads;
                if (name == "get_Target") return current;
                if (name == "set_Target") { ++writes; current = (IGameObject?)values[0]; return null; }
                throw new NotSupportedException(name);
            }));
            SetService(nameof(Svc.Objects), CatalogServiceProxy.Create<IObjectTable>((name, _) =>
            {
                ++reads;
                return name == "GetEnumerator" ? ((IEnumerable<IGameObject>)new IGameObject[] { grief, eater }).GetEnumerator()
                    : throw new NotSupportedException(name);
            }));
            Tick();
            Check(current == grief && writes == 1, "Production adapter must switch the wrong-colour boss");
            Tick();
            Check(writes == 1, "Production adapter must not rewrite an unchanged target");
            Check((bool)scriptType.GetProperty("TargetControlActive")!.GetValue(script)!
                && (ulong)scriptType.GetProperty("PreferredTargetId")!.GetValue(script)! == 1, "Publish active target ownership and entity ID");
            replay = true;
            var beforeReplay = reads;
            Tick();
            Check(reads == beforeReplay && writes == 1 && !(bool)scriptType.GetProperty("TargetControlActive")!.GetValue(script)!,
                "ARR/offline replay must not even read live target services");
            replay = false;
            actorType.GetProperty("Statuses")!.SetValue(pc, Array.CreateInstance(statusType, 0));
            model.GetType().GetMethod("Synchronize")!.Invoke(model, [observations, now.AddSeconds(1)]);
            Tick();
            Check(current == null && writes == 2 && (ulong)scriptType.GetProperty("PreferredTargetId")!.GetValue(script)! == 0,
                "Lost colour explicitly clears an owned boss and publishes hold fire");
            current = Npc(10, 0x1234);
            Tick();
            Check(current != null && writes == 2, "No-colour hold does not clear unrelated manual targets");
            model.GetType().GetMethod("Reset")!.Invoke(model, null);
            var beforeStop = reads;
            Tick();
            Check(reads == beforeStop && !(bool)scriptType.GetProperty("TargetControlActive")!.GetValue(script)!, "Inactive encounter releases ownership");
            Console.WriteLine("PASS production PT99 target adapter: switch, stable target, IPC directive, no-colour hold, unrelated targets, replay and reset");
        }
        finally
        {
            SetService(nameof(Svc.Targets), originalTargets);
            SetService(nameof(Svc.Objects), originalObjects);
        }
        Pt99BlazeRefugeHostRegression.Run(assembly);
    }

    private static void SetService(string name, object? value)
        => typeof(Svc).GetProperty(name, BindingFlags.Public | BindingFlags.Static)!.SetValue(null, value);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
