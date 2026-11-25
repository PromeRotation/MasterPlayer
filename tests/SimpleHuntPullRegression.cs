using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntPullRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Rusalka fixed Flood finish plus3.6 actual effect overwrite and pull destination hint", Rusalka);
        run("SimpleHunts Sugaar three rotating cones exact initial offsets float clocks and actual finishes", Sugaar);
        run("SimpleHunts pulls range hitbox minima dynamic origins all immunities and exact expiry", Pulls);
    }

    private static void Rusalka()
    {
        var encounter = new SimpleHuntEncounter();
        var cast = Cast(17366);
        var boss = Boss(1, 0x2853, cast);
        var player = new ReplayActorState { Id = 100, Position = new(20, 0, 0), HitboxRadius = .5f };
        encounter.Synchronize([boss, player], _ => true);
        var flood = encounter.Areas.Single(a => a.Shape != null);
        Check(flood.Shape is CircleTemplate { Radius: 8 } && flood.Activation == cast.FinishAt.AddSeconds(3.6d),
            "Flood lost its exact R8/Finish+3.6 clock or the R30 pull became a generic danger.");
        Check(encounter.Knockbacks(player).Single().To == Vector3.Zero
            && encounter.Hints(player).Contains("About to be knocked into danger!"),
            "AetherialPull failed to land at the locked origin inside the predicted Flood.");
        encounter.Effect(1, 17369, Start.AddSeconds(2), 1);
        Check(!encounter.Areas.Any(a => a.Shape != null) && encounter.Knockbacks(player).Any()
            && !encounter.Hints(player).Contains("About to be knocked into danger!"),
            "Flood effect incorrectly cleared the pull or landing hint ignored the cleared Flood.");
        encounter.Finish(1);
        encounter.Start(1, Cast(17366, 10), true);
        encounter.Finish(1);
        encounter.Synchronize([boss with { Cast = null, Position = new(50, 0, 50) }, player], _ => true, Start.AddSeconds(50));
        Check(!encounter.Knockbacks(player).Any() && encounter.Areas.Single(a => a.Shape != null).Location == Vector3.Zero,
            "AetherialPull finish or clock expiry cleared/moved the later Flood.");
        encounter.Start(1, Cast(17366, 60) with { Location = new(10, 0, 20) }, true);
        Check(encounter.Areas.Count(a => a.Shape != null) == 1
            && encounter.Areas.Single(a => a.Shape != null).Location == new Vector3(10, 0, 20),
            "A new pull appended Flood instead of replacing its one slot.");
        encounter.Effect(1, 17369, Start.AddSeconds(70), 2);
        Check(!encounter.Areas.Any(a => a.Shape != null), "Flood actual effect retained its area.");
        encounter.Reset();
        encounter.Synchronize([boss with { Cast = Cast(17368) }], _ => true);
        var spark = encounter.Areas.Single().Shape!.Resolve(Vector3.Zero, 0, null);
        Check(spark.Contains(new(1.99f, 0, 11.99f), 0) && !spark.Contains(new(2.01f, 0, 1), 0)
            && !spark.Contains(new(0, 0, 12.01f), 0) && !spark.Contains(new(0, 0, -.01f), 0),
            "AetherialSpark changed its full width4/front length12.");
    }

    private static void Sugaar()
    {
        foreach (var action in new uint[] { 18100, 18101 })
        {
            var encounter = new SimpleHuntEncounter();
            var cast = Cast(action);
            var boss = Boss(1, 0x2875, cast);
            encounter.Synchronize([boss], _ => true);
            var first = encounter.Areas.Where(a => a.Shape != null).OrderBy(a => a.Stage).ToArray();
            var initial = action == 18100 ? 0f : MathF.PI;
            var increment = action == 18100 ? 2 * MathF.PI / 3 : -2 * MathF.PI / 3;
            Check(first.Length == 2 && first.All(a => a.Risky)
                && first[0].Activation == cast.FinishAt.AddSeconds(1.1f)
                && first[1].Activation == first[0].Activation.AddSeconds(2.7f)
                && first[0].Color != null && first[1].Color == null && Near(first[0].Rotation, initial),
                "Sugaar lost its initial direction, two risky previews, colors or float-derived clock.");
            var firstId = first[0].AreaId;
            encounter.Start(1, cast with { Rotation = .2f }, true);
            encounter.Synchronize([boss], _ => true);
            Check(encounter.Areas.Count(a => a.Shape != null) == 2
                && encounter.Areas.Any(a => a.AreaId == firstId && Near(a.Rotation, initial + .2f)),
                "Sugaar packet correction/observation duplicated its sequence or reset identity.");
            encounter.Finish(1, action, Start.AddSeconds(6));
            Check(encounter.Areas.Count(a => a.Shape != null) == 2, "Sugaar starter finish advanced its rotating cone.");
            for (var i = 1; i <= 3; ++i)
            {
                var at = 10d + i * 4;
                var follow = Cast(i % 2 == 0 ? 18099u : 18098u, at);
                encounter.Start(1, follow, true);
                encounter.Effect(1, follow.ActionId, Start.AddSeconds(at + 1), (uint)i);
                var before = encounter.Areas.Count(a => a.Shape != null);
                Check(before == Math.Min(2, 4 - i), "A Sugaar effect advanced its Finish-only sequence.");
                encounter.Finish(1, follow.ActionId, Start.AddSeconds(at + 2));
                var remaining = encounter.Areas.Where(a => a.Shape != null).OrderBy(a => a.Stage).ToArray();
                Check(remaining.Length == Math.Min(2, 3 - i), "Sugaar failed to consume exactly one actual short-cast finish.");
                if (remaining.Length > 0)
                    Check(remaining[0].Activation == Start.AddSeconds(at + 2).AddSeconds(2.7f)
                        && Near(remaining[0].Rotation, initial + .2f + increment * i),
                        "Sugaar next cone did not use actual finish+2.7 or the correct ±120-degree step.");
                if (i == 2) Check(remaining[0].Color == null, "Sugaar last cone retained the imminent color.");
            }
        }
    }

    private static void Pulls()
    {
        foreach (var (oid, action) in new[] { (0x2853u, 17366u), (0x2875u, 18100u) })
        {
            var encounter = new SimpleHuntEncounter();
            var cast = Cast(action);
            var boss = Boss(1, oid, cast) with { HitboxRadius = 5.5f };
            var player = new ReplayActorState { Id = 100, Position = new(30, 0, 0), HitboxRadius = .5f };
            encounter.Synchronize([boss, player], _ => true);
            var movement = encounter.Knockbacks(player).Single();
            Check(Vector3.Distance(movement.To, new(oid == 0x2875 ? 6 : 0, 0, 0)) < .0001f,
                "Pull distance or hitbox minimum changed.");
            if (oid == 0x2875)
                Check(encounter.Hints(player).Contains("About to be knocked into danger!"),
                    "Sugaar ignored landing inside its second displayed future cone.");
            Check(!encounter.Knockbacks(player with { Position = new(30.01f, 0, 0) }).Any()
                && !encounter.Knockbacks(player with { Position = Vector3.Zero }).Any(),
                "Pull affected a range-excluded/strictly coincident player.");
            foreach (var status in new uint[] { 3054, 160, 1209, 1722, 2663, 2345 })
            {
                var immune = player with { Statuses = [new(status, 0, 100, cast.FinishAt.AddTicks(1))] };
                Check(!encounter.Knockbacks(immune).Any(), "A source knockback immunity failed.");
                Check(encounter.Knockbacks(immune with { Statuses = [new(status, 0, 100, cast.FinishAt)] }).Any(),
                    "An immunity expiring exactly at activation remained active.");
            }
            encounter.Synchronize([boss with { Position = new(10, 0, 0) }, player], _ => true);
            movement = encounter.Knockbacks(player).Single();
            Check(Vector3.Distance(movement.To, new(oid == 0x2875 ? 16 : 0, 0, 0)) < .0001f,
                "Sugaar failed to follow its boss origin or Rusalka lost its fixed cast origin.");
            if (oid == 0x2875)
            {
                Check(!encounter.Knockbacks(player with { Position = new(16, 0, 0) }).Any(),
                    "Sugaar moved a player already at the hitbox minimum.");
            }
            encounter.Finish(1, action);
            Check(!encounter.Knockbacks(player).Any(), "Actual starter finish retained its pull projection.");
            encounter.Synchronize([boss with { InCombat = false }, player], _ => true);
            Check(!encounter.Areas.Any(a => a.Shape != null), "Disengaged pull boss retained its predicted area.");
        }
        var dual = new SimpleHuntEncounter();
        var observer = new ReplayActorState { Id = 100, Position = new(30, 0, 0), HitboxRadius = .5f };
        dual.Synchronize([
            Boss(1, 0x2875, Cast(18100)) with { HitboxRadius = 5.5f },
            Boss(2, 0x2875, Cast(18100)) with { Position = new(10, 0, 0), HitboxRadius = 5.5f }, observer], _ => true);
        var projections = dual.Knockbacks(observer).OrderBy(p => p.Source).ToArray();
        Check(projections.Length == 2 && projections.All(p => p.From == observer.Position)
            && projections[0].To == new Vector3(6, 0, 0) && projections[1].To == new Vector3(16, 0, 0),
            "Independent Sugaar instances chained their pulls through another module's landing.");
    }

    private static bool Near(float a, float b) => MathF.Abs(MathF.Atan2(MathF.Sin(a - b), MathF.Cos(a - b))) < .0001f;
    private static ReplayCast Cast(uint action, double start = 0)
        => new(action, 1, Vector3.Zero, 0, Start.AddSeconds(start), Start.AddSeconds(start + 5.3), true, false) { TotalTime = 5 };
    private static ReplayActorState Boss(ulong id, uint oid, ReplayCast? cast)
        => new() { Hp = 1000, Id = id, Oid = oid, InCombat = true, Targetable = true, Cast = cast };
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
