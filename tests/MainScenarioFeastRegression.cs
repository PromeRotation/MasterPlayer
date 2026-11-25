using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;

internal static partial class JobQuestRegression
{
    private static void FeastElectrocutionBoundaries()
    {
        var (e, boss, player) = MainScenario(863, 0x295A);
        var center = boss.Position;
        var actors = new List<DungeonActor> { boss, player };
        DungeonActor first = new(501, 0x295B, center + Vector3.UnitX * 4, IsEnemy: true, ObjectIndex: 4, Hp: 100);
        DungeonActor second = new(502, 0x295B, center + Vector3.UnitX * 7, IsEnemy: true, ObjectIndex: 6, HitboxRadius: 5, Hp: 100);
        actors.Add(first);
        actors.Add(second);
        for (var i = 0; i < 11; ++i)
            actors.Add(new((ulong)(10 + i), 0x295B, center, Cast: new(16286, center, 0, Start, Start.AddSeconds(10))));
        void Sync(double t) => e.Synchronize(actors, player.Position, Start.AddSeconds(t), playerId: 100);
        Sync(0);
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("feast/electrocution/") && a.Navigate) == 11
            && e.PriorityTarget(1, player.Position) == 1, "Feast eleven lightning casts incorrectly activated target override.");
        actors.Add(new(21, 0x295B, center, Cast: new(16286, center, 0, Start, Start.AddSeconds(10))));
        Sync(1);
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("feast/electrocution/")) == 12
            && e.AmbientAoes().Where(a => a.Id.StartsWith("feast/electrocution/")).All(a => !a.Navigate && a.Display)
            && e.Hints(player.Position).Contains("GTFO from aoe!") && e.PriorityTarget(1, player.Position) == 502,
            "Feast twelve must preserve display/hint and choose closest hitbox instead of navigation.");
        actors[3] = second with { Hp = 1 };
        Sync(1.1);
        Check(e.PriorityTarget(1, player.Position) == 1, "Closest pending-dead ball regained priority or fallback chose another ball.");
        actors.RemoveRange(2, 2);
        Sync(1.2);
        Check(e.PriorityTarget(1, player.Position) == 1, "Twelve without potential ball lost all-target priority zero.");
        actors.Add(new(600, 0x29E8, center + Vector3.UnitX, IsEnemy: true, ObjectIndex: 8, Hp: 100));
        Sync(1.3);
        Check(e.PriorityTarget(1, player.Position) == 600 && !e.ForbiddenTargets.Contains(600ul)
            && e.AmbientAoes().Any(a => a.Id == "job-quest/enemy/600"), "Serpent Adds priority/marker or incidental permission.");
        actors.Add(new(22, 0x295B, center, Cast: new(16286, center, 0, Start, Start.AddSeconds(10))));
        Sync(2);
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("feast/electrocution/") && a.Navigate) == 13,
            "Feast thirteen incorrectly retained the exactly-twelve branch.");
        e.Finish(22, 16286, Start.AddSeconds(3));
        Check(e.AmbientAoes().Where(a => a.Id.StartsWith("feast/electrocution/")).All(a => !a.Navigate),
            "Individual Finish did not restore exactly twelve behavior.");
    }

    private static void FeastBurnAndKnockback()
    {
        var (e, boss, player) = MainScenario(863, 0x295A);
        var actors = new List<DungeonActor> { boss, player };
        for (var i = 0; i < 6; ++i)
            actors.Add(new((ulong)(10 + i), 0x29C2, boss.Position,
                Cast: new(16290, boss.Position, 0, Start, Start.AddSeconds(10 - i))));
        e.Synchronize(actors, player.Position, Start, playerId: 100);
        Check(e.OutputCasts.Count() == 5 && e.OutputCasts.Last().Value.Source == 14, "Feast Burn cap or start order.");
        e.Finish(10, 16290, Start.AddSeconds(1));
        Check(e.OutputCasts.Last().Value.Source == 15 && e.OutputCasts.Last().Value.FinishAt == Start.AddSeconds(5),
            "Feast sixth Burn failed original-clock promotion.");
        var (kb, b, p) = MainScenario(863, 0x295A);
        var origin = b.Position - Vector3.UnitZ * 4;
        p = p with { Position = b.Position + Vector3.UnitX * 5, Rotation = 1.2f };
        var cast = new DungeonCastObservation(16292, origin, 2, Start, Start.AddSeconds(3));
        kb.Synchronize([b with { Cast = cast }, p], p.Position, Start, playerId: 100);
        var landing = kb.AmbientAoes().Single(a => a.Id == "feast/kb/1/player");
        var offset = landing.Position - p.Position;
        Check(MathF.Abs(offset.X / offset.Z - 1.25f) < .001f && kb.Arena!.Contains(landing.Position)
            && landing.Rotation == p.Rotation && !landing.Navigate && !kb.PredictedDamage.Any(),
            "Feast Spiritcall must push away from cast location along true wall ray with no extra navigation/damage.");
        kb.Finish(1, 16292, Start.AddSeconds(1));
        Check(!kb.AmbientAoes().Any(a => a.Id.StartsWith("feast/kb/")), "Feast cancelled Spiritcall retained projection.");
    }
}
