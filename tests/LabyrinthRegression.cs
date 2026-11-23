using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class LabyrinthRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Labyrinth four native bosses correct territory and exact resolved BoneDragon arena", () =>
        {
            Check(ArrDungeonCatalog.IsScript("masterplayer.alliance.labyrinth")
                && LabyrinthCatalog.Create().Length == 4, "Catalog/entry.");
            foreach (var definition in LabyrinthCatalog.Create())
            {
                using var f = Scene(definition.BossOid);
                Check(ArrDungeonCatalog.Owns(definition.BossOid, 174) && !ArrDungeonCatalog.Owns(definition.BossOid, 245)
                    && f.Engine.Active?.BossOid == definition.BossOid, "Ownership.");
            }
            var type = typeof(BossMod.BossModule).Assembly.GetType("BossMod.RealmReborn.Alliance.A11BoneDragon.A11BoneDragon")!;
            var source = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            var native = LabyrinthArenas.BoneDragon(7);
            for (var x = -466f; x <= -434; x += .37f)
                for (var z = -21f; z <= 46; z += .41f)
                    Check(native.Contains(new(x, 7, z), 0) == source.Contains(new BossMod.WPos(x, z) - source.Center), "Bone arena union mismatch.");
            Check(native.GetContours().Count == 1 && native.GetContours().Single().All(p => p.Y == 7), "Arena drawing/height.");
        });
        run("Labyrinth BoneDragon resurrection retains phase on death HP0 and combat loss until destroyed", () =>
        {
            using var f = Scene(0x92B);
            Start(f, 10, 749, 2);
            f.Actors[1] = f.Actors[1] with { Dead = true, Hp = 0, InCombat = false, Targetable = false }; f.Scan();
            Check(f.Engine.Active != null && f.Engine.Casts.Count == 1 && f.Engine.PreserveStageAcrossBattleEnd,
                "Bone resurrection reset component.");
            f.Actors[1] = f.Actors[1] with { Dead = false, Hp = 1000, InCombat = true, Targetable = true }; f.Scan();
            Check(f.Engine.Casts.Count == 1, "Resurrection discarded helper.");
            f.Actors.Remove(1); f.Scan();
            Check(f.Engine.Active == null && f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Destroyed primary cleanup.");
        });
        run("Labyrinth other bosses HP0 personal death and Director wipe reset boundaries", () =>
        {
            foreach (var boss in new uint[] { 0x92E, 0x932, 0x938 })
            {
                using var f = Scene(boss);
                f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan();
                Check(f.Engine.Active != null && f.Engine.PreserveFightAcrossPlayerDeath, "Personal death ended fight.");
                f.Engine.DirectorUpdate(0x40000005); f.Scan(); Check(f.Engine.Active == null, "Wipe repulled.");
                f.Engine.DirectorUpdate(0x40000011); f.Scan(); Check(f.Engine.Active != null, "Recommence.");
                f.Actors[1] = f.Actors[1] with { Hp = 0 }; f.Scan(); Check(f.Engine.Active == null, "HP0 ended too late.");
            }
        });
        foreach (var (boss, action, source) in new (uint, uint, BossMod.AOEShape)[]
        {
            (0x92B, 749, new BossMod.AOEShapeCircle(6)),
            (0x92B, 750, new BossMod.AOEShapeCone(105, new(MathF.PI / 3))),
            (0x92B, 1828, new BossMod.AOEShapeCone(7.8f, new(MathF.PI / 4))),
            (0x92E, 758, new BossMod.AOEShapeCircle(6)),
            (0x92E, 760, new BossMod.AOEShapeCircle(6)),
            (0x92E, 1829, new BossMod.AOEShapeCircle(5)),
            (0x92E, 1759, new BossMod.AOEShapeCircle(6.8f)),
            (0x932, 1789, new BossMod.AOEShapeCircle(8.4f)),
            (0x938, 1741, new BossMod.AOEShapeCircle(3)),
            (0x938, 1742, new BossMod.AOEShapeCircle(4)),
            (0x938, 1743, new BossMod.AOEShapeCircle(5)),
            (0x938, 1744, new BossMod.AOEShapeCircle(6)),
            (0x938, 1780, new BossMod.AOEShapeCone(15, new(MathF.PI / 3))),
            (0x938, 1736, new BossMod.AOEShapeCone(80, new(MathF.PI / 8))),
            (0x938, 1730, new BossMod.AOEShapeCircle(35)),
        })
            run($"Labyrinth {boss:X} action{action} geometry matches source and persists until observed finish", () =>
            {
                using var f = Scene(boss); Start(f, 10, action, 2, .7f);
                var cast = f.Engine.Casts[10];
                var shape = cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null);
                for (var x = -36f; x <= 36; x += .73f)
                    for (var z = -36f; z <= 36; z += .79f)
                        Check(shape.Contains(cast.Location + new Vector3(x, 0, z), .00001f)
                            == source.Check(new(cast.Location.X + x, cast.Location.Z + z), new(cast.Location.X, cast.Location.Z), new(.7f)), "AOE source geometry.");
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Dual view/navigation.");
                f.Now = f.Now.AddSeconds(20); f.Engine.Tick(f.Now); f.Render();
                Check(f.Engine.Casts.Count == 1 && f.Draw.Previews.Count == 1, "Nominal expiry substituted for Finish.");
                f.Engine.Finish(10, action, f.Now, true); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Finish cleanup.");
            });
        run("Labyrinth Phlegethon four Megiddo helper hits keep independent size clocks and drawing", () =>
        {
            using var f = Scene(0x938);
            for (uint i = 0; i < 4; ++i) Start(f, 10 + i, 1741 + i, 3 + i);
            Check(f.Engine.Casts.Count == 4 && f.Draw.Previews.Count == 4 && f.Snapshot.DangerAreas.Count == 4, "Four independent helpers.");
            f.Engine.Finish(11, 1742, f.Now, true); f.Render();
            Check(f.Engine.Casts.Count == 3 && f.Engine.Casts[12].Rule.Shape is CircleTemplate { Radius: 5 }, "Finish consumed other helpers.");
        });
        run("Labyrinth BoneDragon Stone predicts actual party target and EvilEye remains a cone", () =>
        {
            using var f = Scene(0x92B); Start(f, 10, 970, 1, target: 100);
            Check(f.Engine.PredictedDamage.Single().TargetId == 100, "Stone target prediction.");
            Start(f, 11, 750, 3);
            Check(!f.Engine.Gazes.Any(), "Cone invented facing/gaze restriction.");
        });
        run("Labyrinth Meteor freezes alive comets origin and clock supports passable hitbox", () =>
        {
            using var f = Scene(0x932); var c = f.Engine.Arena!.Position;
            f.Actors[20] = new(20, 0x936, c + new Vector3(10, 0, 0), HitboxRadius: 2.4f);
            f.Actors[21] = new(21, 0x936, c - new Vector3(10, 0, 0), Dead: true, HitboxRadius: 2.4f); f.Scan();
            Start(f, 1, 1756, 10);
            var safe = Group(f).Single(a => a.Safe); var geometry = safe.Shape.Resolve(c, 0, null);
            Check(safe.Activation == f.Now.AddSeconds(10.3) && geometry.Contains(c + new Vector3(15, 0, 0))
                && !geometry.Contains(c - new Vector3(15, 0, 0)), "Comet alive filter or clock.");
            Check(!geometry.Contains(c + new Vector3(10.19f, 0, 0)) && geometry.Contains(c + new Vector3(10.21f, 0, 0)),
                "0.2m inner radius or hitbox became impassable.");
            Check(!f.Engine.Hints(c + new Vector3(15, 0, 0)).Contains("躲在彗星后")
                && f.Engine.Hints(c - new Vector3(15, 0, 0)).Contains("躲在彗星后"), "Personal shelter hint.");
            f.Actors[20] = f.Actors[20] with { Dead = true, Position = c + new Vector3(0, 0, 10) }; f.Scan();
            Check(geometry.Contains(c + new Vector3(15, 0, 0)) && Group(f).Length == 2, "Existing cast shelter followed comet.");
            f.Engine.Finish(1, 1756, f.Now, true); f.Render();
            Check(Group(f).Length == 0 && f.Draw.Previews.Count == 0, "Meteor finish cleanup.");
        });
        run("Labyrinth Meteor no comet output absent and caster destruction releases shelter", () =>
        {
            using var f = Scene(0x932); Start(f, 10, 1756, 10); Check(Group(f).Length == 0, "Invented no-blocker safezone.");
            f.Engine.Finish(10, 1756, f.Now, true); f.Now = f.Now.AddSeconds(1);
            f.Actors[20] = new(20, 0x936, f.Engine.Arena!.Position + new Vector3(10, 0, 0), HitboxRadius: 2.4f); f.Scan();
            Start(f, 10, 1756, 10); Check(Group(f).Length == 2, "Shelter missing.");
            f.Engine.RemoveActor(10, f.Now); f.Actors.Remove(10); f.Render(); Check(Group(f).Length == 0, "Destroyed caster retained shelter.");
        });
        run("Labyrinth Charybdis effect plus0.1 predicts then one live source consumes one event", () =>
        {
            using var f = Scene(0x932); var c = f.Engine.Arena!.Position;
            f.Engine.Effect(1, 1752, 1, f.Now, 100);
            f.Engine.Effect(1, 1752, 2, f.Now, 100); f.Render();
            var predicted = f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("trial/voidzone/event")).ToArray();
            Check(predicted.Length == 2 && predicted.All(a => a.Activation == f.Now.AddSeconds(.1)), "Effect clock.");
            f.Actors[20] = new(20, 0x98C, f.Actors[100].Position, EventState: 7); f.Scan();
            Check(f.Engine.AmbientAoes().Count(a => a.Id.StartsWith("trial/voidzone/event")) == 2, "Retired source consumed.");
            f.Actors[20] = f.Actors[20] with { EventState = 0 }; f.Scan();
            Check(f.Engine.AmbientAoes().Count(a => a.Id.StartsWith("trial/voidzone/event")) == 1
                && f.Engine.AmbientAoes().Single(a => a.Id.StartsWith("trial/voidzone/live")).Shape is CircleTemplate { Radius: 6 }, "Live matching/radius.");
        });
        run("Labyrinth Meteor actual solver reaches comet shadow and owner clears both views", () =>
        {
            using var f = Scene(0x932); var c = f.Engine.Arena!.Position;
            f.Actors[100] = f.Actors[100] with { Position = c + new Vector3(10, 0, 4) };
            f.Actors[20] = new(20, 0x936, c + new Vector3(10, 0, 0), HitboxRadius: 2.4f); f.Scan();
            Start(f, 1, 1756, 5);
            var snapshot = f.Snapshot;
            var result = new SafeZoneSolver().Solve(snapshot,
                new(f.Actors[100].Position, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = result.ContinuationPath.Count > 0 ? result.ContinuationPath : result.ExecutablePath;
            Check(result.Status == SolutionStatus.Success && !result.IsLeastDangerFallback && path.Count > 1
                && Group(f).Single(a => a.Safe).Shape.Resolve(c, 0, null).Contains(path[^1].Position), "Meteor actual route.");
            f.Output.Clear(); Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Owner leak.");
        });
    }

    private static Fight Scene(uint boss)
    {
        var f = new Fight(boss, 174);
        var center = f.Engine.Arena!.Position;
        f.Actors[1] = f.Actors[1] with { Position = center, Hp = 1000, MaxHp = 1000 };
        f.Actors[100] = new(100, 0, center + new Vector3(8, 0, 0), PartySlot: 0, PartyRole: "Tank", Hostile: false);
        f.Scan(); return f;
    }
    private static void Start(Fight f, ulong source, uint action, double seconds, float rotation = 0, ulong target = 0)
    {
        if (!f.Actors.ContainsKey(source))
        {
            f.Actors[source] = new(source, 0x233C, f.Engine.Arena!.Position); f.Scan();
        }
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(seconds + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static DungeonAmbientAoe[] Group(Fight f) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("labyrinth/")).ToArray();
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
