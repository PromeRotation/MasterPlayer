using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class HadesRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Hades first live gaol gates giant target selects gaol and publishes offset radius six goal", () =>
        {
            using var f = Scene(); f.Actors[2] = new(2, 0x294A, new(100, 0, 80), InCombat: true);
            f.Actors[20] = new(20, 0x294B, new(112, 0, 100)); f.Scan();
            Check(f.Engine.ForbiddenTargets.SequenceEqual([2UL]) && f.Engine.PriorityTarget(2, f.Actors[100].Position) == 20,
                "Gaol did not gate giant target or prioritize rescue.");
            var goal = Group(f, "gaol/goal").Single();
            Check(goal is { Safe: true, Display: false, GoalPriority: 10, Shape: CircleTemplate { Radius: 6 } }
                && goal.Position == new Vector3(112, 0, 101) && f.Snapshot.GoalAreas.Count == 1, "Gaol goal geometry/weight differs.");
            f.Actors[20] = f.Actors[20] with { Dead = true }; f.Actors[21] = new(21, 0x294B, new(90, 0, 100)); f.Scan();
            Check(f.Engine.ForbiddenTargets.Length == 0 && !Group(f, "gaol/goal").Any(),
                "Source first dead gaol incorrectly fell through to second live gaol.");
            f.Engine.RemoveActor(20); f.Actors.Remove(20); f.Scan();
            Check(f.Engine.ForbiddenTargets.SequenceEqual([2UL]) && Group(f, "gaol/goal").Single().Position.X == 90,
                "Destroyed first gaol prevented next gaol from becoming first.");
        });
        run("Hades broken faith animation appends raw snapshots and only exact-position destruction clears", () =>
        {
            using var f = Scene(); f.Actors[20] = new(20, 0x1EAD28, new(85.123f, 2, 100), Rotation: .3f); f.Scan();
            f.Engine.Animation(20, 0x00010002, f.Now); f.Engine.Animation(20, 0x00010002, f.Now); f.Render();
            Check(Group(f, "faith").Length == 2 && Group(f, "faith").All(a => a.Position.X == 85.123f
                && a.Activation == f.Start.AddSeconds(8.5)), "Animation callbacks lost raw snapshots or duplicate events.");
            f.Now = f.Start.AddSeconds(100); f.Engine.Effect(10, 16718, 1, f.Now); f.Render();
            Check(Group(f, "faith").Length == 2, "Time or hit erased animation predictions.");
            f.Actors[20] = f.Actors[20] with { Position = new(85.124f, 2, 100), Dead = true }; f.Scan();
            f.Engine.RemoveActor(20); f.Actors.Remove(20); f.Scan();
            Check(Group(f, "faith").Length == 2, "Different destruction position removed locked snapshots.");
            f.Actors[21] = new(21, 0x1EAD28, new(85.123f, 5, 100)); f.Scan();
            f.Engine.RemoveActor(21); f.Actors.Remove(21); f.Scan();
            Check(!Group(f, "faith").Any(), "Same XZ destruction failed to clear all co-located snapshots.");
        });
        run("Hades persistent animation snapshot is not replayed each frame or rebound after destruction", () =>
        {
            using var f = Scene(); f.Actors[20] = new(20, 0x1EAD28, new(85, 0, 100), EventAnimation: 0x00010002); f.Scan(); f.Scan();
            Check(Group(f, "faith").Length == 1, "Persistent EAnim repeated callbacks.");
            f.Engine.RemoveActor(20); f.Scan();
            Check(!Group(f, "faith").Any(), "Stale raw animation resurrected destroyed source.");
        });
        run("Hades icon eruption consumes two per own effect but only one per nether effect", () =>
        {
            using var f = Scene();
            f.Engine.Icon(100, 139, f.Now, 101); f.Engine.Icon(101, 139, f.Now); f.Engine.Icon(102, 139, f.Now); f.Render();
            Check(Group(f, "bait/16723").Count(a => a.Display) == 3
                && f.Engine.PredictedDamage.All(p => p.Activation == f.Start.AddSeconds(3)), "Eruption icon delay/target differs.");
            f.Engine.Effect(10, 16723, 1, f.Now, 102); f.Render();
            Check(Group(f, "bait/16723").Count(a => a.Display) == 1
                && f.Engine.PredictedDamage.Single().TargetId == 102, "Source double-FIFO consumption differs.");
            f.Engine.Effect(10, 16755, 2, f.Now); f.Render();
            Check(!Group(f, "bait/16723").Any(), "Nether effect failed single consumption.");
        });
        run("Hades captivity icon independent radius clock FIFO and permanent dead-target cleanup", () =>
        {
            using var f = Scene(); f.Engine.Icon(101, 120, f.Now); f.Engine.Icon(102, 120, f.Now); f.Render();
            Check(Group(f, "bait/16745").Count(a => a.Display && a.Shape is CircleTemplate { Radius: 8 }) == 2
                && f.Engine.PredictedDamage.All(p => p.Activation == f.Start.AddSeconds(4.7)), "Captivity marker differs.");
            f.Engine.Effect(10, 16745, 1, f.Now, 102); f.Render();
            Check(f.Engine.PredictedDamage.Single().TargetId == 102, "Captivity matched target instead of FIFO.");
            f.Actors[102] = f.Actors[102] with { Dead = true }; f.Scan();
            f.Actors[102] = f.Actors[102] with { Dead = false }; f.Scan();
            Check(!Group(f, "bait/16745").Any(), "Revival restored discarded icon.");
        });
        run("Hades ravenous and dual baits await actual effect and differ in damage types", () =>
        {
            using var f = Scene(); Start(f, 10, 16728, target: 101); Start(f, 11, 16738, target: 102);
            End(f, 10, 16728); End(f, 11, 16738);
            Check(Group(f, "bait/16728").Any() && Group(f, "bait/16738").Any()
                && f.Engine.PredictedDamage.Any(p => p.TargetId == 101 && p.DamageType == "Raidwide")
                && f.Engine.PredictedDamage.Any(p => p.TargetId == 102 && p.DamageType == "Tankbuster"), "Effect-ended bait or type differs.");
            f.Engine.Effect(11, 16738, 1, f.Now); f.Render();
            Check(Group(f, "bait/16728").Any() && !Group(f, "bait/16738").Any(), "Dual hit erased independent ravenous.");
            f.Engine.Effect(10, 16728, 2, f.Now); f.Render(); Check(!Group(f, "bait/16728").Any(), "Ravenous source hit did not consume.");
        });
        run("Hades effect-ended bait source destruction and target death permanently discard it", () =>
        {
            using var f = Scene(); Start(f, 10, 16728, target: 101); Start(f, 11, 16738, target: 102);
            f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan();
            f.Actors[101] = f.Actors[101] with { Dead = false }; f.Scan();
            Check(!Group(f, "bait/16728").Any(), "Effect-ended dead target resurrected.");
            f.Engine.RemoveActor(11); f.Scan();
            Check(!Group(f, "bait/16738").Any(), "Destroyed source raw cast restored effect-ended bait.");
        });
        run("Hades ancient darkness follows dead target and ends only by its cast", () =>
        {
            using var f = Scene(); Start(f, 10, 17811, target: 101);
            f.Actors[101] = f.Actors[101] with { Dead = true, Position = new(111, 0, 100) }; f.Scan();
            Check(Group(f, "bait/17811").Single(a => a.Display).Position.X == 111, "Normal cast dead target was hidden.");
            f.Engine.Effect(10, 17811, 1, f.Now); f.Render(); Check(Group(f, "bait/17811").Any(), "Normal cast ended at effect.");
            End(f, 10, 17811); Check(!Group(f, "bait/17811").Any(), "Normal cast actual finish leaked.");
        });
        run("Hades hellborn cone displays at target but marked navigation originates at source", () =>
        {
            using var f = Scene(); Start(f, 10, 16751, target: 100);
            var display = Group(f, "bait/16751").Single(a => a.Display);
            Check(display.Position.X == 105 && display.Shape is FanTemplate { Radius: 100 }
                && Group(f, "bait/16751").Where(a => !a.Display).All(a => a.Position.X == 100)
                && f.Engine.PredictedDamage.Single().DamageType == "Raidwide", "Hellborn source centerAtTarget or default type differs.");
            var geometry = display.Shape.Resolve(display.Position, display.Rotation, null);
            Check(HostDrawingGeometry.Convert(geometry).Contains(new(115, 0, 100)), "World Hellborn cone conversion differs.");
            End(f, 10, 16751); Check(!Group(f, "bait/16751").Any(), "Hellborn finish leaked.");
        });
        run("Hades ancient water finite stack has independent target finishes and no invented shared damage", () =>
        {
            using var f = Scene(); Start(f, 10, 17812, target: 101); Start(f, 11, 17812, target: 102);
            Check(Group(f, "stack").Count(a => a.Display) == 2 && !f.Engine.PredictedDamage.Any(), "Stack source prediction differs.");
            End(f, 10, 17812); Check(Group(f, "stack").Count(a => a.Display) == 1, "Stack finish erased other target.");
            f.Actors[102] = f.Actors[102] with { Dead = true }; f.Scan(); Check(!Group(f, "stack").Any(), "Dead stack target still active.");
            f.Actors[102] = f.Actors[102] with { Dead = false }; f.Scan(); Check(Group(f, "stack").Any(), "Normal stack failed to return.");
        });
        run("Hades nether supports both tether directions shared batch clock and actual untether", () =>
        {
            using var f = Scene(); f.Engine.Tether(20, 17, true, f.Now, 100);
            f.Now = f.Start.AddSeconds(1); f.Engine.Tether(101, 17, true, f.Now, 21); f.Render();
            Check(Group(f, "bait/16755").Count(a => a.Display && a.Id.EndsWith("/display")) == 2
                && Group(f, "bait/16755").Count(a => a.Id.EndsWith("/tether")) == 2
                && f.Engine.PredictedDamage.All(p => p.Activation == f.Start.AddSeconds(4.7) && p.DamageType == "Tankbuster"),
                "Tether sides or shared time differs.");
            f.Engine.Effect(20, 16755, 1, f.Now); f.Render();
            Check(Group(f, "bait/16755").Count(a => a.Id.EndsWith("/display")) == 2, "Hit removed healthy tether.");
            f.Engine.Tether(20, 17, false, f.Now, 100); f.Render();
            Check(Group(f, "bait/16755").Count(a => a.Id.EndsWith("/display")) == 1, "Untether did not remove matched pair.");
            f.Engine.Tether(101, 17, false, f.Now, 21);
            f.Now = f.Start.AddSeconds(10); f.Engine.Tether(20, 17, true, f.Now, 100); f.Render();
            Check(f.Engine.PredictedDamage.Single().Activation == f.Start.AddSeconds(4.7), "Untether prematurely reset batch clock.");
        });
        run("Hades nether dead-target prune leaves old clock until a subsequent empty effect resets it", () =>
        {
            using var f = Scene(); f.Engine.Tether(20, 17, true, f.Now, 100);
            f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan();
            Check(Group(f, "bait/16755").Any(), "Tether dead target pruned per frame.");
            f.Engine.Effect(20, 16755, 1, f.Now); f.Render(); Check(!Group(f, "bait/16755").Any(), "Hit failed dead-target prune.");
            f.Now = f.Start.AddSeconds(10); f.Engine.Tether(101, 17, true, f.Now, 21); f.Render();
            Check(f.Engine.PredictedDamage.Single().Activation == f.Start.AddSeconds(4.7), "Prune cleared clock after final removal.");
            f.Engine.Tether(101, 17, false, f.Now, 21); f.Engine.Effect(20, 16755, 2, f.Now);
            f.Now = f.Start.AddSeconds(20); f.Engine.Tether(102, 17, true, f.Now, 21); f.Render();
            Check(f.Engine.PredictedDamage.Single().Activation == f.Start.AddSeconds(20).AddSeconds(4.7), "Empty hit did not reset batch.");
        });
        run("Hades doom tower default-time two capacity and destruction clears all without doom status inference", () =>
        {
            using var f = Scene(); f.Actors[30] = new(30, 0x1EAD29, f.Actors[100].Position);
            f.Actors[31] = new(31, 0x1EAD29, new(80, 0, 100)); f.Scan();
            Check(Group(f, "tower").Count(a => a.Display) == 2 && !f.Engine.Hints(f.Actors[100].Position).Contains("进入死亡宣告塔")
                && f.Engine.PredictedDamage.Single() is { TargetId: 100, Activation: var at } && at == default,
                "Occupied tower hint or default damage differs.");
            f.Engine.RemoveActor(31); f.Actors.Remove(31); f.Scan();
            Check(!Group(f, "tower").Any(), "One Doom tower destruction did not clear all.");
        });
        run("Hades explicit same-pair retether restores pruned bait and retains batch clock", () =>
        {
            using var f = Scene(); f.Engine.Tether(101, 17, true, f.Now, 20);
            f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan();
            f.Engine.Effect(20, 16755, 1, f.Now);
            f.Actors[101] = f.Actors[101] with { Dead = false }; f.Now = f.Start.AddSeconds(10); f.Scan();
            f.Engine.Tether(101, 17, true, f.Now, 20); f.Render();
            Check(Group(f, "bait/16755").Any() && f.Engine.PredictedDamage.Single().Activation == f.Start.AddSeconds(4.7),
                "Real repeated tether was mistaken for a raw snapshot or reset its old clock.");
        });
        run("Hades marked stack still joins another under-capacity stack as actual source logic", () =>
        {
            using var f = Scene(); Start(f, 10, 17812, target: 100); Start(f, 11, 17812, target: 101);
            var join = Group(f, "stack/join").Single();
            Check(!join.Shape.Resolve(join.Position, join.Rotation, null).Contains(f.Actors[101].Position)
                && !Group(f, "stack").Any(a => a.Id.EndsWith("/avoid")), "Marked player failed source under-capacity join.");
        });
        run("Hades personal death and revival keep giant history while primary is gone", () =>
        {
            using var f = Scene(); f.Actors[2] = new(2, 0x294A, new(100, 0, 100), InCombat: true); f.Scan();
            f.Engine.RemoveActor(1); f.Actors.Remove(1); Start(f, 10, 16732);
            f.Actors[100] = f.Actors[100] with { Dead = true };
            f.Engine.Synchronize(f.Actors.Values, null, f.Now, playerId: 100); f.Render();
            Check(f.Engine.Active != null && f.Engine.PreserveFightAcrossPlayerDeath && f.Draw.Previews.ContainsKey("cast/10"),
                "Personal death cleared giant history.");
            f.Actors[100] = f.Actors[100] with { Dead = false }; f.Scan();
            Check(f.Engine.Active != null && f.Engine.PrimaryId == 1, "Revival lost retained primary.");
        });
        run("Hades simple rectangle widths source snapshots actual independent finishes and cone angle", () =>
        {
            using var f = Scene(); Start(f, 10, 16715); Start(f, 11, 16732);
            Check(f.Draw.Previews["cast/10"].Geometry.Contains(new(109.9f, 0, 119))
                && !f.Draw.Previews["cast/10"].Geometry.Contains(new(110.1f, 0, 119))
                && f.Draw.Previews["cast/11"].Geometry.Contains(new(107.9f, 0, 199)), "Full widths differ.");
            f.Now = f.Start.AddSeconds(100); f.Render(); Check(f.Draw.Previews.ContainsKey("cast/10"), "Time removed active cast.");
            End(f, 10, 16715); Check(f.Draw.Previews.ContainsKey("cast/11"), "One finish removed other cast.");
            End(f, 11, 16732); Start(f, 10, 16740);
            Check(f.Draw.Previews["cast/10"].Geometry.Contains(new(120, 0, 130))
                && !f.Draw.Previews["cast/10"].Geometry.Contains(new(130, 0, 120)), "Echo source 90-degree cone differs.");
        });
        run("Hades raidwides consume six watched casts only and no-cast pulses do not invent prediction", () =>
        {
            using var f = Scene();
            foreach (var action in new[] { 17815u, 16768u, 16757u, 16758u, 16759u, 16748u })
            { Start(f, 10, action); Check(f.Engine.PredictedDamage.Count() == 3, "Watched raidwide missing."); End(f, 10, action); }
            foreach (var action in new[] { 16730u, 16761u, 16762u, 16758u }) f.Engine.Effect(10, action, action, f.Now);
            f.Render(); Check(!f.Engine.PredictedDamage.Any(), "Effect-only pulse invented a cast prediction.");
        });
        run("Hades dead or removed initial primary preserves giant phase including battle end then final death clears", () =>
        {
            using var f = Scene(); f.Actors[2] = new(2, 0x294A, new(100, 0, 100), InCombat: true); f.Scan();
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan(); Start(f, 10, 16732);
            Check(f.Engine.PreserveStageAcrossBattleEnd && f.Engine.PrimaryId == 1 && f.Engine.Active != null, "Giant phase lost initial primary.");
            f.Engine.RemoveActor(1); f.Actors.Remove(1); f.Scan();
            Check(f.Engine.Active != null && f.Engine.PreserveStageAcrossBattleEnd, "Destroyed primary ended live giant.");
            f.Actors[2] = f.Actors[2] with { Dead = true }; f.Scan();
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Final phase death leaked.");
        });
        run("Hades no giant uses vacuous end and source placeholder statuses or icons create no control", () =>
        {
            using var f = Scene(); f.Engine.Icon(100, 343, f.Now); f.Engine.Icon(100, 40, f.Now);
            Check(!Group(f, "bait").Any() && f.Engine.ForbiddenTargets.Length == 0
                && ArrDungeonCatalog.IsScript("masterplayer.trial.hades"), "Placeholder inferred mechanics or missing owner.");
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan(); Check(f.Engine.Active == null, "Absent giant forced unseen phase.");
        });
    }
    private static Fight Scene()
    {
        var f = new Fight(0x2949, 847); var c = new Vector3(100, 0, 100);
        f.Actors[1] = f.Actors[1] with { Position = c };
        f.Actors[100] = new(100, 0, c + Vector3.UnitX * 5, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c + Vector3.UnitZ * 10, PartySlot: 1, Hostile: false);
        f.Actors[102] = new(102, 0, c - Vector3.UnitX * 10, PartySlot: 2, Hostile: false);
        f.Actors[10] = new(10, 0x233C, c); f.Actors[11] = new(11, 0x233C, c);
        f.Actors[20] = new(20, 0x29D8, c - Vector3.UnitX * 15);
        f.Actors[21] = new(21, 0x29D9, c + Vector3.UnitX * 15); f.Scan(); return f;
    }
    private static void Start(Fight f, ulong source, uint action, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, 0, f.Now, f.Now.AddSeconds(5.3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action)
    {
        f.Engine.Tick(f.Now); f.Engine.Finish(source, action); f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render();
    }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix) => f.Engine.AmbientAoes()
        .Where(a => a.Id.StartsWith("hades/" + prefix, StringComparison.Ordinal)).ToArray();
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
