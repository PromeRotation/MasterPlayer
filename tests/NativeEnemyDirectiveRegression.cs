using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using static HalataliRegression;

internal static class NativeEnemyDirectiveRegression
{
    public static void Run(Action<string, Action> run)
    {
        run("Native enemy parity tank pull uses both hitboxes strict two yalm threshold and aggro facing", Pull);
        run("Native enemy parity Garuda first Aerial Blast changes boss and Suparna positions without forcing provoke", Garuda);
        run("Native enemy parity Titan zero tank distance and gaoler ownership preserve provoke policy", Titan);
        run("Native enemy parity Ifrit nails exclude big nail dots and release restriction after last nail", Ifrit);
        run("Native enemy parity Caduceus clone uses strict 25 yalm boundary and OT ownership", Caduceus);
        run("Native enemy parity Twintania death sentence flips ownership on cast start", Twintania);
        run("Native enemy parity ranged range goal does not force chasing and tank route reaches published goal", Planning);
        run("Native enemy parity optional combat goals preserve all danger margin cells", Margin);
        run("Native enemy parity facing fallback excludes inner three yalms and prefers outer safe axis", Axis);
        run("Native enemy parity six encounters publish directional actor markers with source colors and visibility", Markers);
    }

    private static Fight Scene(uint oid, uint territory, string assignment = "MT")
    {
        var f = new Fight(oid, territory);
        f.Engine.CurrentAssignment = assignment;
        f.Engine.CurrentPartyAssignments = [assignment, "OT", "H1", "H2", "M1", "M2", "R1", "R2"];
        f.Actors[1] = f.Actors[1] with { Hp = 1000, MaxHp = 1000, TargetId = 100, HitboxRadius = 3 };
        f.Actors[100] = new(100, 0, new(0, 0, 4), PartySlot: 0, PartyRole: "Tank", ClassJob: 19,
            Hostile: false, TargetId: 1, HitboxRadius: 1);
        f.Actors[101] = new(101, 0, new(5, 0, 5), PartySlot: 1, PartyRole: "Tank", ClassJob: 21, Hostile: false);
        f.Scan();
        return f;
    }

    private static DungeonEnemyDirective Boss(Fight f) => f.Engine.EnemyDirectives().Single(e => e.Actor.Id == 1);
    private static DungeonAmbientAoe Goal(Fight f, string suffix)
        => f.Engine.AmbientAoes().Single(a => a.Id == "enemy/tank/" + suffix);

    private static void Markers()
    {
        foreach (var (oid, territory) in new (uint, uint)[]
        {
            (0x90F, 348), (0xF5, 297), (0xF8, 296), (0xD3, 295), (0x7D7, 241), (0x7E5, 245),
        })
        {
            using var f = Scene(oid, territory);
            f.Actors[1] = f.Actors[1] with { Rotation = MathF.PI / 2 };
            f.Scan();
            var marker = f.Engine.AmbientAoes().Single(a => a.Id == "enemy/marker/1");
            Check(!marker.Navigate && marker.Display && !marker.OutlineOnly && marker.Color == new Vector4(1, 0, 0, 1),
                "Boss marker entered navigation or lost its enemy color and filled arrow.");
            var geometry = marker.Shape.Resolve(marker.Position, marker.Rotation, null);
            Check(geometry.Contains(marker.Position + Vector3.UnitX * .5f)
                && !geometry.Contains(marker.Position + Vector3.UnitZ * .5f), "Boss marker lost its observed heading.");
            f.Actors[1] = f.Actors[1] with { Targetable = false };
            f.Scan();
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "enemy/marker/1") == (oid is 0xF8 or 0x7E5),
                "Boss marker ignored the source untargetable exception.");
        }
        using var garuda = Scene(0xF5, 297);
        garuda.Actors[10] = new(10, 0xF3, Vector3.UnitX, Targetable: false);
        garuda.Actors[11] = new(11, 0x620, Vector3.UnitZ, Targetable: false);
        garuda.Scan();
        var monolith = garuda.Engine.AmbientAoes().Single(a => a.Id == "enemy/marker/10");
        Check(monolith.Color == new Vector4(1, 128f / 255, 0, 1)
            && !garuda.Engine.AmbientAoes().Any(a => a.Id == "enemy/marker/11"),
            "Monolith object color or plume targetability differs from the source.");
        garuda.Actors[10] = garuda.Actors[10] with { Dead = true };
        garuda.Scan();
        Check(!garuda.Engine.AmbientAoes().Any(a => a.Id == "enemy/marker/10"), "Dead monolith remained visible.");
        using var ifrit = Scene(0xD3, 295);
        ifrit.Actors[10] = new(10, 0xD4, Vector3.UnitX);
        ifrit.Scan();
        Check(ifrit.Engine.AmbientAoes().Single(a => a.Id == "enemy/marker/10").Color == monolith.Color,
            "Nail marker lost the object color.");
        ifrit.Actors[10] = ifrit.Actors[10] with { Targetable = false };
        ifrit.Scan();
        Check(!ifrit.Engine.AmbientAoes().Any(a => a.Id == "enemy/marker/10"), "Untargetable nail retained its marker.");
    }

    private static void Pull()
    {
        using var f = Scene(0x90F, 348);
        var directive = Boss(f);
        var actual = f.Actors[1].Position;
        var expected = directive.Position - Vector3.Normalize(actual - directive.Position) * 6;
        Check(Vector3.Distance(Goal(f, "pull").Position, expected) < .001f, "Tank pull omitted a hitbox or used the wrong side.");
        f.Actors[1] = f.Actors[1] with { Position = directive.Position + Vector3.UnitX * 2 };
        f.Scan();
        Check(!f.Engine.AmbientAoes().Any(a => a.Id == "enemy/tank/pull")
            && Goal(f, "front").Shape.Resolve(Goal(f, "front").Position, Goal(f, "front").Rotation, null)
                .Contains(f.Actors[1].Position - Vector3.UnitZ * 6), "Exactly two yalms must use the expected facing axis.");
        var wrongFacing = f.Actors[1].Position + new Vector3(MathF.Sin(MathF.PI * 40 / 180), 0,
            -MathF.Cos(MathF.PI * 40 / 180)) * 6;
        Check(!Goal(f, "front").Shape.Resolve(Goal(f, "front").Position, 0, null).Contains(wrongFacing)
            && !Goal(f, "axis").Shape.Resolve(Goal(f, "axis").Position, Goal(f, "axis").Rotation, null).Contains(wrongFacing),
            "A forty-degree facing error incorrectly satisfied tank facing.");
        // Garuda default MT ownership remains valid before acquiring aggro.
        using var g = Scene(0xF5, 297);
        var desired = Boss(g).Position;
        g.Actors[1] = g.Actors[1] with { Position = desired, TargetId = 101 };
        g.Scan();
        Check(!g.Engine.AmbientAoes().Any(a => a.Id == "enemy/tank/front") && Goal(g, "range").Safe,
            "A tank without aggro was forced to the front.");
    }

    private static void Garuda()
    {
        using var f = Scene(0xF5, 297, "OT");
        f.Actors[10] = new(10, 0x621, new(0, 0, 15), TargetId: 101);
        f.Scan();
        var before = Boss(f);
        Check(Vector3.Distance(before.Position, new(0, 0, -8)) < .001f, "Garuda pre-blast destination changed.");
        var sister = f.Engine.EnemyDirectives().Single(e => e.Actor.Id == 10);
        Check(sister.ShouldBeTanked && !sister.PreferProvoking && !f.Engine.EnemyProvokeActions().Any(a => a.Target == 10),
            "Suparna ownership incorrectly implied provoke.");
        f.Engine.Effect(1, 1554, 1, f.Now);
        var after = Boss(f);
        Check(Math.Abs(after.Rotation - MathF.PI * .75f) < .001f
            && Vector3.Distance(after.Position, new(18 / MathF.Sqrt(2), 0, -18 / MathF.Sqrt(2))) < .001f,
            "Aerial Blast did not activate the new tank destination.");
        sister = f.Engine.EnemyDirectives().Single(e => e.Actor.Id == 10);
        Check(Math.Abs(sister.Rotation + MathF.PI / 4) < .001f, "Suparna did not change rotation after Aerial Blast.");
        using var mt = Scene(0xF5, 297);
        mt.Actors[20] = new(20, 0x620, new(0, 0, 10), TargetId: 100);
        mt.Scan();
        Check(mt.Engine.PriorityTarget(1, mt.Actors[100].Position) == 20,
            "Tank aggro ownership hid a higher-priority owned damage target.");
    }

    private static void Titan()
    {
        using var f = Scene(0xF8, 296, "OT");
        f.Actors[1] = f.Actors[1] with { TargetId = 101 };
        f.Actors[10] = new(10, 0x8F2, Vector3.UnitX * 8, TargetId: 101);
        f.Scan();
        Check(Boss(f).TankDistance == 0, "Titan retained default two yalm tank distance.");
        var add = f.Engine.EnemyDirectives().Single(e => e.Actor.Id == 10);
        Check(add.ShouldBeTanked && !add.PreferProvoking && !f.Engine.EnemyProvokeActions().Any(a => a.Target == 10),
            "Titan gaoler ownership incorrectly forced provoke.");
    }

    private static void Ifrit()
    {
        using var f = Scene(0xD3, 295);
        f.Actors[1] = f.Actors[1] with { Position = new(0, 0, -18) };
        f.Actors[20] = new(20, 0xD4, new(0, 0, -10));
        f.Actors[21] = new(21, 0x91B, new(5, 0, -10));
        f.Scan();
        Check(f.Engine.ForbiddenDotTargets.SequenceEqual([20UL]), "Ifrit DoT restriction did not isolate the small nail.");
        Check(!f.Engine.EnemyDirectives().Single(e => e.Actor.Id == 21).ShouldBeTanked, "Large nail was assigned to tank.");
        f.Actors[20] = f.Actors[20] with { Dead = true };
        f.Actors[21] = f.Actors[21] with { Dead = true };
        f.Scan();
        Check(f.Engine.ForbiddenDotTargets.Length == 0, "DoT restriction survived the last nail.");
    }

    private static void Caduceus()
    {
        using var f = Scene(0x7D7, 241, "OT");
        var center = f.Engine.Arena!.Position;
        f.Actors[1] = f.Actors[1] with { Position = center, Hp = 500, TargetId = 101 };
        f.Actors[2] = new(2, 0x7D7, center + Vector3.UnitX * 25, TargetId: 101, Hp: 500, MaxHp: 1000);
        f.Scan();
        var clone = f.Engine.EnemyDirectives().Single(e => e.Actor.Id == 2);
        Check(clone.ShouldBeTanked && clone.PreferProvoking && clone.Position == clone.Actor.Position, "Clone at 25 yalms was forcibly repositioned.");
        f.Actors[2] = f.Actors[2] with { Position = center + Vector3.UnitX * 24.9f };
        f.Scan();
        clone = f.Engine.EnemyDirectives().Single(e => e.Actor.Id == 2);
        var destination = CaduceusPlatforms.Center(6);
        Check(Math.Abs(clone.Position.X - destination.X) < .001f && Math.Abs(clone.Position.Z - destination.Y) < .001f,
            "Nearby clone did not receive platform six.");
        f.Engine.CurrentAssignment = "MT";
        f.Engine.CurrentPartyAssignments = ["MT", "OT"];
        Check(!f.Engine.EnemyDirectives().Single(e => e.Actor.Id == 2).ShouldBeTanked, "MT also claimed the OT clone.");
    }

    private static void Twintania()
    {
        using var f = Scene(0x7E5, 245);
        Check(Boss(f).ShouldBeTanked && Boss(f).Position == new Vector3(0, 0, 5), "Twintania opening MT ownership missing.");
        f.Cast(1, 1458, 4);
        Check(!Boss(f).ShouldBeTanked, "Death Sentence failed to release MT on cast start.");
        f.Engine.CurrentAssignment = "OT";
        f.Engine.CurrentPartyAssignments = ["OT", "MT"];
        Check(Boss(f).ShouldBeTanked, "OT did not claim the next Death Sentence.");
    }

    private static void Planning()
    {
        using var f = Scene(0x90F, 348);
        f.Actors[100] = f.Actors[100] with { Position = new(8, 0, 0) };
        f.Scan();
        var goal = Goal(f, "pull");
        Check(f.Snapshot.GoalAreas.Any(g => g.Geometry.Contains(goal.Position)), "Tank goal was not published to MP SafeZone.");
        var snapshot = f.Snapshot;
        var solution = new SafeZoneSolver().Solve(snapshot,
            new(f.Actors[100].Position, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
        Check(!solution.IsLeastDangerFallback && solution.ExecutablePath.Concat(solution.ContinuationPath)
            .Any(p => goal.Shape.Resolve(goal.Position, goal.Rotation, null).Contains(p.Position)), "MP solver did not reach the native tank goal.");
        f.Actors[100] = f.Actors[100] with { PartyRole = "Ranged", ClassJob = 23 };
        f.Scan();
        var range = f.Engine.AmbientAoes().Single(a => a.Id == "enemy/ranged/range");
        Check(range.Shape.Resolve(range.Position, range.Rotation, null).Contains(f.Actors[100].Position), "Ranged position within attack range was forced closer.");
    }

    private static void Margin()
    {
        using var f = Scene(0x90F, 348);
        f.Actors[100] = f.Actors[100] with { PartyRole = "Ranged", ClassJob = 23 };
        f.Scan();
        f.Safe.AddPersistentDanger(new CircleTemplate(2), new StaticTransform(Vector3.Zero, 0));
        var snapshot = f.Snapshot;
        var nativeGoals = snapshot.GoalAreas.Where(g => g.GoalWindows.All(w => !w.OverrideDangerMargin)).ToArray();
        Check(nativeGoals.Length > 0, "Optional goal policy was lost by DungeonOutput.");
        var baseline = new SafeZoneSnapshot(snapshot.Version, f.Now, snapshot.ArenaBoundary,
            snapshot.HardObstacles, snapshot.DangerAreas, []);
        var withGoals = new SafeZoneSnapshot(snapshot.Version, f.Now, snapshot.ArenaBoundary,
            snapshot.HardObstacles, snapshot.DangerAreas, nativeGoals);
        var options = new SolveOptions { GridResolution = .5f, HardSafetyMargin = 2, ArenaSafetyMargin = 0 };
        var before = RasterizedMap.Build(baseline, f.Actors[100].Position, options, f.Now);
        var after = RasterizedMap.Build(withGoals, f.Actors[100].Position, options, f.Now);
        var checkedMargin = 0;
        for (var row = 0; row < before.Rows; ++row)
            for (var col = 0; col < before.Columns; ++col)
            {
                if (before.IsDangerCoreAt(col, row) || before.PixelMaxGAt(col, row) is < 0 or float.MaxValue) continue;
                ++checkedMargin;
                Check(after.PixelMaxGAt(col, row) == before.PixelMaxGAt(col, row),
                    "An optional attack range goal removed the danger margin.");
            }
        Check(checkedMargin > 0, "The regression did not exercise any expanded danger cell.");
    }

    private static void Axis()
    {
        using var f = Scene(0xF5, 297);
        var directive = Boss(f);
        f.Actors[1] = f.Actors[1] with { Position = directive.Position };
        f.Actors[100] = f.Actors[100] with { Position = directive.Position + new Vector3(2, 0, -3.1f) };
        f.Scan();
        var axis = Goal(f, "axis");
        var geometry = axis.Shape.Resolve(axis.Position, axis.Rotation, null);
        var direction = new Vector3(MathF.Sin(axis.Rotation), 0, MathF.Cos(axis.Rotation));
        Check(!geometry.Contains(directive.Position + direction * 2.9f)
            && geometry.Contains(directive.Position + direction * 3.1f), "Facing fallback crossed the inner three yalms.");
        var window = f.Snapshot.GoalAreas.Single(g => g.Geometry.Contains(axis.Position)
            && g.GoalWindows.Any(w => w.Priority == 2)).GoalWindows.Single();
        Check(window is { Preference: PositionPreference.Center, PreferredPosition: not null }
            && Vector3.Distance(window.PreferredPosition.Value, directive.Position + direction * 6) < .001f,
            "DungeonOutput lost the outer endpoint preference.");
        f.Safe.AddPersistentDanger(new CircleTemplate(1), new StaticTransform(directive.Position + direction * 6, 0));
        var snapshot = f.Snapshot;
        var map = RasterizedMap.Build(snapshot, f.Actors[100].Position,
            new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
        var cells = Enumerable.Range(0, map.Rows).SelectMany(row => Enumerable.Range(0, map.Columns)
            .Where(col => map.PixelPriorityAt(col, row) == 2 && map.PixelMaxGAt(col, row) == float.MaxValue)
            .Select(col => (Point: map.CellCenter(col, row), Score: map.PixelGoalPreferenceScoreAt(col, row)))).ToArray();
        Check(cells.Length > 0, "No safe axis fallback cells remained.");
        var best = cells.MaxBy(c => c.Score);
        Check(Vector3.Dot(best.Point - directive.Position, direction) > 4.5f,
            "Facing fallback preferred the inner point near the player over the outer safe point.");
        using var titan = Scene(0xF8, 296);
        titan.Actors[1] = titan.Actors[1] with { Position = Boss(titan).Position, HitboxRadius = 1 };
        titan.Actors[100] = titan.Actors[100] with { HitboxRadius = 1 };
        titan.Scan();
        Check(!titan.Engine.AmbientAoes().Any(a => a.Id == "enemy/tank/axis"), "Range at most three created an invalid fallback rectangle.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
