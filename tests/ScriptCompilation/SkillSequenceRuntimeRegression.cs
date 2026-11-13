using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using PromeRotation.LogSystem;

internal static class SkillSequenceRuntimeRegression
{
    public static void Run(Assembly assembly)
    {
        var originalObjects = Svc.Objects;
        try
        {
            var player = CatalogServiceProxy.Create<IPlayerCharacter>((name, _) => name switch
            {
                "get_EntityId" => (object)42u,
                "get_GameObjectId" => 42ul,
                _ => throw new NotSupportedException(name),
            });
            var objects = CatalogServiceProxy.Create<IObjectTable>((name, _) => name == "get_LocalPlayer" ? player : throw new NotSupportedException(name));
            typeof(Svc).GetProperty(nameof(Svc.Objects))!.SetValue(null, objects);
            var type = assembly.GetType("MasterPlayer.Combat.Sequences.CombatSkillSequence", true)!;
            var runnerType = assembly.GetType("MasterPlayer.Combat.Sequences.SkillSequenceRunner", true)!;
            var instance = RuntimeHelpers.GetUninitializedObject(type);
            var runner = Activator.CreateInstance(runnerType)!;
            Set("runner", runner);

            void Set(string name, object value) => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(instance, value);
            T Get<T>(string name) => (T)type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;
            void Effect(uint sequence, uint action = 20, ulong source = 42)
            {
                var effect = new LogSystemActionEffectEvent { GlobalSequence = sequence, ActionId = action, SourceId = source };
                type.GetMethod("OnEffect", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(instance, [effect]);
            }
            bool Confirmed() => (bool)runnerType.GetField("confirmed", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(runner)!;

            Set("pendingId", 10u);
            Set("pendingAdjusted", 20u);
            Effect(1, source: 99);
            Effect(1, action: 99);
            Check(Get<uint>("pendingId") == 10 && !Confirmed(), "Ignore other players and unrelated effects.");
            Effect(1);
            Check(Get<uint>("pendingId") == 0 && Confirmed(), "Adjusted action effect confirms the pending step.");
            runnerType.GetField("confirmed", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(runner, false);
            Set("pendingId", 10u);
            Set("pendingAdjusted", 20u);
            Effect(1);
            Check(Get<uint>("pendingId") == 10 && !Confirmed(), "Repeated targets in the same global sequence must not confirm an adjacent equal skill.");
            Effect(2);
            Check(Confirmed(), "A new global sequence may confirm the next equal skill.");
            Console.WriteLine("PASS skill sequence runtime: local adjusted action confirmation and multi-target deduplication");

            runnerType.GetField("confirmed", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(runner, false);
            Set("chain", new uint[] { 2259, 2261, 2267 });
            Set("chainIndex", 0);
            Set("pendingId", 2259u);
            Set("pendingAdjusted", 18805u);
            Effect(3, 18805);
            Check(Get<int>("chainIndex") == 1 && !Confirmed(), "Confirm each mudra without advancing the configured skill.");
            Set("pendingId", 2261u);
            Set("pendingAdjusted", 18806u);
            Effect(4, 18806);
            Check(Get<int>("chainIndex") == 2 && !Confirmed(), "Advance the second mudra.");
            Set("pendingId", 2267u);
            Set("pendingAdjusted", 2267u);
            Effect(5, 2267);
            Check(Confirmed() && Get<uint[]?>("chain") == null, "Only the final ninjutsu confirms the configured skill.");
            Set("pendingCast", true);
            Set("pendingId", 10u);
            type.GetMethod("Stop")!.Invoke(instance, null);
            Check(!Get<bool>("pendingCast") && Get<uint>("pendingId") == 0 && !Confirmed(), "Stop clears pending casts and confirmation state.");
            Console.WriteLine("PASS skill sequence runtime: mudra substeps and pending-cast stop cleanup");
            var settings = PromeRotation.Data.PromeSettings.Instance;
            var originalAcr = settings.EnableAcr;
            try
            {
                settings.EnableAcr = PromeRotation.Data.AcrState.Hold;
                Set("pausedAcr", true);
                type.GetMethod("Stop")!.Invoke(instance, null);
                Check(settings.EnableAcr == PromeRotation.Data.AcrState.On, "Restore an ACR paused by this sequence.");
                settings.EnableAcr = PromeRotation.Data.AcrState.Off;
                Set("pausedAcr", true);
                type.GetMethod("Stop")!.Invoke(instance, null);
                Check(settings.EnableAcr == PromeRotation.Data.AcrState.Off, "Preserve a user override made during the sequence.");
                settings.EnableAcr = PromeRotation.Data.AcrState.Hold;
                type.GetMethod("Stop")!.Invoke(instance, null);
                Check(settings.EnableAcr == PromeRotation.Data.AcrState.Hold, "Do not release another source's pre-existing Hold.");
            }
            finally { settings.EnableAcr = originalAcr; }
            Console.WriteLine("PASS skill sequence runtime: restore only the ACR hold owned by this sequence");
        }
        finally { typeof(Svc).GetProperty(nameof(Svc.Objects))!.SetValue(null, originalObjects); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
