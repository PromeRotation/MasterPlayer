using System.Numerics;
using System.Reflection;
using BossMod;

internal static partial class BozjaRegression
{
    private static void RunBozjaBlockers(Action<string, Action> run)
    {
        run("Bozja pending CE22 source cold Rest missing two initial positions real activation exception", ChoctoberColdBlocker);
    }

    private static void ChoctoberColdBlocker()
    {
        Initialize();
        foreach (var order in new[] { new uint[] { 20587 }, [20586u, 20587], [20587u, 20586, 20586], [20586u, 20586, 20587] })
        {
            var w = new WorldState(1, "CE22-cold-history-contract");
            w.Frame.Timestamp = START;
            var center = new Vector3(393, 0, 268);
            w.Execute(new ActorState.OpCreate(1, 0x2E68, 1, 0, "", 0, ActorType.Enemy, Class.PLD, 100,
                new(center, 0), 2, default, true, false, 0, 0, 0));
            for (var i = 0; i < order.Length; ++i)
            {
                var id = (ulong)(20 + i);
                var position = center + Vector3.UnitX * (i * 3 + 1);
                w.Execute(new ActorState.OpCreate(id, 0x233C, (int)id, 0, "", 0, ActorType.Helper, Class.PLD, 100,
                    new(position, 0), .5f, default, true, false, 0, 0, 0));
                w.Actors.Find(id)!.CastInfo = new() { Action = new(ActionType.Spell, order[i]),
                    TotalTime = 1, Location = position };
            }
            using var module = new BossMod.Shadowbringers.Foray.CriticalEngagement.CE22TheHuntForRedChoctober.CE22TheHuntForRedChoctober(w, w.Actors.Find(1)!);
            var component = module.GetType().Assembly.GetType(module.GetType().Namespace + ".ChocoMeteorStream")!;
            var activation = typeof(BossModule).GetMethod("ActivateComponent")!.MakeGenericMethod(component);
            Exception? failure = null;
            try { activation.Invoke(module, null); }
            catch (TargetInvocationException ex) { failure = ex.InnerException; }
            var sufficient = order.Length == 3 && order[0] == 20586 && order[1] == 20586;
            Check(sufficient ? failure == null : failure is ArgumentOutOfRangeException,
                "CE22 actual source cold activation order " + string.Join(",", order) + ": " + failure);
            if (failure != null)
                Check(failure.StackTrace?.Contains("ChocoMeteorStream.OnCastStarted") == true,
                    "CE22 exception must be the actual source missing-history access");
            Console.WriteLine("INFO CE22 pending source cold order=" + string.Join(",", order)
                + " result=" + (failure?.GetType().Name ?? "success") + "; no native ownership added");
        }
    }
}
