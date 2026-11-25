using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntExecutionRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);
    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Sally recorded forward and reverse incomplete histories exact delays risk cross geometry", Histories);
        run("SimpleHunts Sally effects only global two-item queue locked execution pose corrections and cleanup", Queue);
    }

    private static void Histories()
    {
        foreach (var count in new[] { 0, 1, 2, 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var state = new HuntExecutionState();
            var actions = new uint[] { 38454, 38455, 38456, 38454 };
            for (var i = 0; i < count; ++i) state.Start(Pending(actions[i], i * 3));
            var cast = Pending(reverse ? 38459u : 38457u, 20);
            state.Start(cast);
            for (var stage = 0; stage < count; ++stage)
            {
                var visible = state.Areas.ToArray();
                var index = reverse ? count - stage - 1 : stage;
                var delay = reverse ? 5.2f - 2.6f * index : index * 2.6f;
                Check(visible.Length == Math.Min(2, count - stage)
                    && visible[0].Activation == cast.Activation.AddSeconds(delay) && visible[0].Risky,
                    "Sally changed the literal reverse formula, history capacity or global two-item preview limit.");
                if (visible.Length == 2)
                {
                    var nextIndex = reverse ? index - 1 : index + 1;
                    Check(visible[1].Risky == (actions[nextIndex] == 38456), "Sally treated the future cross as a harmless preview.");
                }
                var area = visible[0];
                if (actions[index] == 38456)
                {
                    var geometry = area.Shape!.Resolve(area.Location, area.Rotation, null);
                    Check(geometry.Contains(new(20, 0, 20), 0) && !geometry.Contains(new(20, 0, 0), 0),
                        "Sally cross lost its forty-by-five arms or its intrinsic 45-degree rotation.");
                }
                else Check(actions[index] == 38454 ? area.Shape is CircleTemplate { Radius: 10 } : area.Shape is DonutTemplate,
                    "Execution inferred shape from its first-action name instead of recorded history.");
                state.Effect(38461, Start.AddSeconds(30 + stage));
            }
            Check(!state.Areas.Any(), "Zero history invented an AOE or resolved history retained a stage.");
        }
    }

    private static void Queue()
    {
        var encounter = new SimpleHuntEncounter();
        var player = new ReplayActorState { Id = 100 };
        void Observe(uint action, double at)
            => encounter.Synchronize([Boss(Pending(action, at).Input), player], _ => true, Start.AddSeconds(at));
        Observe(38454, 0);
        Observe(38456, 3);
        Observe(38455, 6);
        Observe(38457, 9);
        var original = encounter.Areas.Where(c => c.Shape != null).ToArray();
        Check(original.Length == 2 && original.All(c => c.Risky), "Sally did not show a risky future cross.");
        var correction = Pending(38457, 9).Input with { Location = new(10, 0, 20), Rotation = .2f };
        encounter.Start(1, correction, true);
        encounter.Synchronize([Boss(correction) with { Position = new(80, 0, 90), Rotation = 2 }, player], _ => true, Start.AddSeconds(10));
        var corrected = encounter.Areas.Where(c => c.Shape != null).ToArray();
        Check(corrected.Length == 2 && corrected.All(c => c.Location == new Vector3(10, 0, 20)),
            "Packet correction duplicated execution or the boss dragged locked shapes.");
        encounter.Synchronize([Boss(null), player], _ => true, Start.AddSeconds(20));
        Check(encounter.Areas.Count(c => c.Shape != null) == 2, "Execution cast finish consumed an effect-driven stage.");
        encounter.Effect(1, 38457, Start.AddSeconds(21), 1);
        encounter.Effect(1, 38457, Start.AddSeconds(21), 1);
        Check(encounter.Areas.Count(c => c.Shape != null) == 2
            && encounter.Areas.First(c => c.Shape != null).Shape is not CircleTemplate,
            "Duplicate effect consumed two queue heads.");
        Observe(38454, 22);
        Observe(38459, 25);
        Check(encounter.Areas.Count(c => c.Shape != null) == 2, "Appended execution exposed more than two global heads.");
        foreach (var n in Enumerable.Range(0, 3)) encounter.Effect(1, 38463, Start.AddSeconds(30 + n), (uint)(10 + n));
        Check(!encounter.Areas.Any(c => c.Shape != null), "Remaining appended sequence did not resolve.");
        encounter.Reset();
        Check(!encounter.Active && !encounter.Areas.Any(), "Sally reset retained history.");
    }

    private static ReplayActorState Boss(ReplayCast? cast)
        => new() { Hp = 1000, Id = 1, Oid = 0x4395, Targetable = true, InCombat = true, Cast = cast };
    private static HuntPendingCast Pending(uint action, double at)
    {
        var definition = SimpleHuntCatalog.Find(0x4395)!;
        var rule = definition.Casts.Single(c => c.Action == action);
        var cast = new ReplayCast(action, 1, Vector3.Zero, 0, Start.AddSeconds(at), Start.AddSeconds(at + 5.3), false, false);
        return new(1, definition, rule, cast.Location, 0, cast.StartedAt, cast.FinishAt, 1, false, true, null, cast, cast);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
