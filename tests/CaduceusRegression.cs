using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class CaduceusRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Caduceus pull requires original above half HP and phase survives HP0 until actual death", () =>
        {
            using var f = Scene();
            Check(f.Engine.Active?.BossOid == 0x7D7 && f.Engine.Arena is RectangleGeometry, "Native identity.");
            f.Actors[1] = f.Actors[1] with { Hp = 0, InCombat = false }; f.Scan();
            Check(f.Engine.Active != null, "HP0 or combat loss ended source phase.");
            f.Engine.ResetFight(); f.Actors[1] = f.Actors[1] with { Hp = 500, InCombat = true }; f.Scan();
            Check(f.Engine.Active == null, "Clone half HP incorrectly pulled.");
            f.Actors[1] = f.Actors[1] with { Hp = 501 }; f.Scan();
            Check(f.Engine.Active != null, "Original half+1 failed pull.");
        });
        run("Caduceus clone locks first identity and primary death destruction waits for valid clone", () =>
        {
            using var f = Scene();
            Split(f); f.Actors[1] = f.Actors[1] with { Dead = true, Hp = 0 }; f.Scan();
            Check(f.Engine.Active != null, "Primary death ended live clone.");
            f.Engine.RemoveActor(1, f.Now); f.Actors.Remove(1); f.Scan();
            Role(f, "MT");
            Check(f.Engine.Active != null && f.Engine.PrimaryId == 1 && f.Engine.PreserveStageAcrossBattleEnd
                && f.Engine.PriorityTarget(0, P(5)) == 2, "Destroyed primary rebound or zero-priority clone was lost.");
            f.Actors[2] = f.Actors[2] with { Targetable = false }; f.Scan();
            Check(f.Engine.Active == null, "Invalid clone retained dead original.");
        });
        run("Caduceus does not rebind destroyed first clone and no nominal enrage timeout", () =>
        {
            using var f = Scene(); Split(f);
            f.Engine.RemoveActor(2, f.Now); f.Actors.Remove(2);
            f.Actors[3] = new(3, 0x7D7, P(6), InCombat: true, Hp: 400, MaxHp: 1000); f.Scan();
            f.Now = f.Start.AddSeconds(800); f.Engine.Tick(f.Now); f.Render();
            Check(f.Engine.Active != null, "Nominal enrage finished phase.");
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan();
            Check(f.Engine.Active == null, "Replacement clone silently rebound.");
        });
        run("Caduceus thirteen platform polygons preserve centers winding and octagon dimensions", () =>
        {
            using var f = Scene();
            var outlines = Group(f, "platform/outline");
            Check(outlines.Length == 13 && f.Draw.Previews.Count >= 13, "Dual view platform outlines missing.");
            for (var i = 0; i < 13; ++i)
            {
                var poly = CaduceusPlatforms.Polygons[i];
                var center = poly.Aggregate(Vector2.Zero, (sum, p) => sum + p) / poly.Length;
                var geometry = outlines[i].Shape.Resolve(outlines[i].Position, 0, null);
                Check(geometry.Contains(new(center.X, 0, center.Y)), "Physical platform center treated as outside.");
                Check(poly.Length == (i == 12 ? 8 : 6), "Platform sides.");
                Check(geometry.GetContours().Single().All(p => p.Y == CaduceusPlatforms.Heights[i]), "World height lost.");
                for (var x = -9f; x < 9; x += .71f)
                    for (var z = -9f; z < 9; z += .67f)
                    {
                        var point = new Vector3(center.X + x, 0, center.Y + z);
                        var expected = GeometryHelper.PointInPolygon2D(new(point.X, point.Z), poly);
                        Check(geometry.Contains(point) == expected, "Platform physical polygon mismatch.");
                    }
            }
        });
        run("Caduceus terrain union blocks outside and only five height-gated zero width high edges", () =>
        {
            using var f = Scene();
            var geometry = Terrain(f);
            Check(geometry.Contains(Vector3.Zero) && !geometry.Contains(P(5)), "Platform outside inverted.");
            foreach (var (lower, upper) in CaduceusPlatforms.HighEdges)
            {
                var (a, b) = CaduceusPlatforms.Edge(lower, upper);
                var midpoint = (a + b) / 2;
                var across = Vector2.Normalize(CaduceusPlatforms.Center(upper) - CaduceusPlatforms.Center(lower));
                Vector3 At(Vector2 p) => new(p.X, 0, p.Y);
                var low = new CaduceusPlatformTemplate(0x1FFF, true, ActorHeight: CaduceusPlatforms.Heights[upper] - .11f).Resolve(default, 0, null);
                var high = new CaduceusPlatformTemplate(0x1FFF, true, ActorHeight: CaduceusPlatforms.Heights[upper] - .1f).Resolve(default, 0, null);
                Check(low.IntersectsSegment(At(midpoint - across * .2f), At(midpoint + across * .2f)), "Thin edge missed raster segment.");
                Check(!high.IntersectsSegment(At(midpoint - across * .2f), At(midpoint + across * .2f)), "Height boundary overblocked.");
                Check(!low.Contains(At(CaduceusPlatforms.Center(upper))), "Whole upper platform forbidden.");
            }
            var (from, to) = (5, 4);
            var edge = CaduceusPlatforms.Edge(from, to);
            var mid = (edge.A + edge.B) / 2;
            var d = Vector2.Normalize(CaduceusPlatforms.Center(to) - CaduceusPlatforms.Center(from));
            Check(!geometry.IntersectsSegment(new(mid.X - d.X * .2f, 0, mid.Y - d.Y * .2f),
                new(mid.X + d.X * .2f, 0, mid.Y + d.Y * .2f)), "Ordinary seam became wall.");
        });
        run("Caduceus active platforms use six second event clock and raw state cannot immediately undo packet", () =>
        {
            using var f = Scene(); f.Now = f.Start.AddSeconds(11); f.Scan();
            f.Actors[10] = new(10, 0x1E8729, P(0)); f.Scan();
            f.Engine.EventState(10, 2); f.Render(); f.Scan();
            var personal = Group(f, "platform/personal").Single();
            Check(personal.Activation == f.Now.AddSeconds(6), "Event clock drift or stale raw overwrote packet.");
            Check(Group(f, "platform/outline").Single(a => a.Id.EndsWith("/0")).Color == new Vector4(1, 0, 0, 1), "Active marker lost.");
            f.Now = f.Now.AddSeconds(1); f.Scan();
            Check(Group(f, "platform/personal").Length == 0, "New raw state never converged.");
        });
        run("Caduceus spawner R2 on highest active and R1 elsewhere with inset and split disables spawning", () =>
        {
            using var f = Scene(); f.Now = f.Start.AddSeconds(11); f.Scan();
            Role(f, "R2"); f.Actors[10] = new(10, 0x1E8729, P(0), EventState: 2); f.Scan();
            Check(Group(f, "platform/personal").Single().Shape is CaduceusPlatformTemplate { Inverted: true, Inset: 1 }, "R2 spawner.");
            Role(f, "R1"); f.Scan();
            Check(Group(f, "platform/personal").Single().Shape is CaduceusPlatformTemplate { Inverted: false }, "Other role must avoid.");
            f.Actors[10] = f.Actors[10] with { EventState = 0 }; f.Scan();
            f.Actors[11] = new(11, 0x1E8729, P(8), EventState: 2); f.Scan();
            Check(Group(f, "platform/personal").Single().Shape is CaduceusPlatformTemplate { Inverted: true }, "R1 spawner.");
            Split(f);
            Check(Group(f, "platform/personal").Single().Shape is CaduceusPlatformTemplate { Inverted: false }, "Split still spawned slimes.");
        });
        run("Caduceus first ten seconds and healer ranged offtank preferred platform restrictions", () =>
        {
            using var f = Scene(); Role(f, "H1"); f.Scan();
            Check(Group(f, "platform/preferred").Length == 0, "Early strategy positioning.");
            f.Now = f.Start.AddSeconds(10); f.Scan();
            Check(Group(f, "platform/preferred").Single().Shape is CaduceusPlatformTemplate { Mask: 1 << 5, Inset: 1 }, "Healer platform.");
            Role(f, "R1"); f.Scan();
            Check(Group(f, "platform/preferred").Single().Shape is CaduceusPlatformTemplate { Mask: 1 << 8 }, "R1 platform.");
            Role(f, "R2"); f.Actors[100] = f.Actors[100] with { Position = P(0) with { Y = 4.39f } }; f.Scan();
            Check(Group(f, "platform/preferred").Length == 0, "R2 lower layer forced onto high platform.");
            f.Actors[100] = f.Actors[100] with { Position = P(0) }; f.Scan();
            Check(Group(f, "platform/preferred").Single().Shape is CaduceusPlatformTemplate { Mask: 1 }, "R2 high platform.");
            Role(f, "OT"); f.Actors[1] = f.Actors[1] with { Hp = 729 }; f.Scan();
            Check(Group(f, "offtank").Length == 1, "OT preposition threshold.");
            f.Actors[1] = f.Actors[1] with { Hp = 730 }; f.Scan();
            Check(Group(f, "offtank").Length == 0, "73% must be strict.");
        });
        run("Caduceus slime delivery uses observed HP while target priority includes pending damage", () =>
        {
            using var f = Scene(); f.Now = f.Start.AddSeconds(11);
            f.Actors[10] = new(10, 0x7D8, P(5), TargetId: 100, Hp: 201, MaxHp: 1000,
                IncomingEffects: [new(0, 2, 0, false, false) { HpDelta = -1 }]); f.Scan();
            var delivery = Group(f, "slime/delivery").Single();
            Check(Vector3.Distance(delivery.Position, f.Actors[1].Position) > 6, "Delivery prematurely used pending HP.");
            Check(f.Engine.PriorityTarget(10, P(5)) == 1, "At20% predicted slime should lose priority.");
            f.Actors[10] = f.Actors[10] with { IncomingEffects = [] }; f.Scan();
            Check(f.Engine.PriorityTarget(1, P(5)) == 10, "Above20% owned slime priority.");
            f.Actors[10] = f.Actors[10] with { Hp = 200 }; f.Scan();
            Check(Group(f, "slime/delivery").Single().Position == f.Actors[1].Position, "Low HP delivery not into boss.");
            f.Actors[10] = f.Actors[10] with { Hp = 900, TargetId = 101 }; f.Scan();
            Check(Group(f, "slime/delivery").Length == 0 && f.Engine.PriorityTarget(10, P(5)) == 1, "Other player's slime attacked.");
        });
        run("Caduceus split priority honors MT OT three seconds five percent and lower30 split", () =>
        {
            using var f = Scene(); Split(f); Role(f, "H2");
            Check(f.Engine.PriorityTarget(2, P(5)) == 1, "Early pickup window.");
            f.Now = f.Now.AddSeconds(3); f.Scan();
            Check(f.Engine.PriorityTarget(1, P(5)) == 2, "H2 clone assignment.");
            Role(f, "MT"); Check(f.Engine.PriorityTarget(2, P(5)) == 1, "MT assignment.");
            Role(f, "OT"); Check(f.Engine.PriorityTarget(1, P(5)) == 2, "OT assignment.");
            Role(f, "H1"); f.Actors[2] = f.Actors[2] with { Hp = 551 }; f.Scan();
            Check(f.Engine.PriorityTarget(1, P(5)) == 2, "Positive HP difference.");
            f.Actors[2] = f.Actors[2] with { Hp = 449 }; Role(f, "H2"); f.Scan();
            Check(f.Engine.PriorityTarget(2, P(5)) == 1, "Negative HP difference underflow.");
            f.Actors[1] = f.Actors[1] with { Hp = 300 }; f.Actors[2] = f.Actors[2] with { Hp = 300 }; f.Scan();
            Check(f.Engine.PriorityTarget(2, P(5)) == 2 && f.Engine.PriorityTarget(1, P(5)) == 1, "Below30 equal priority.");
        });
        run("Caduceus scales is soft target priority and never hard forbidden target", () =>
        {
            using var f = Scene(); f.Actors[1] = f.Actors[1] with { Hp = 729, Statuses = [349] }; f.Scan();
            Check(f.Engine.PriorityTarget(1, P(5)) == 0 && f.Engine.ForbiddenTargets.Length == 0, "Scales became hard shield block.");
            f.Actors[1] = f.Actors[1] with { Hp = 730 }; f.Scan();
            Check(f.Engine.PriorityTarget(0, P(5)) == 1, "73% boundary.");
        });
        run("Caduceus HoodSwing timer primary only and both bosses cleave during casting", () =>
        {
            using var f = Scene(); Split(f);
            f.Engine.Effect(2, 1208, 1, f.Now);
            Check(f.Engine.Hints(P(5)).Any(h => h.StartsWith("下次顺劈约 0.0")), "Clone reset global timer.");
            f.Engine.Effect(1, 1208, 2, f.Now);
            Check(f.Engine.Hints(P(5)).Any(h => h.StartsWith("下次顺劈约 18.0")), "Primary timer missing.");
            f.Actors[1] = f.Actors[1] with { Cast = new(1209, P(2), .8f, f.Now, f.Now.AddSeconds(2.3)) };
            f.Actors[2] = f.Actors[2] with { TargetId = 101 }; f.Scan();
            Check(f.Engine.Casts.ContainsKey(1) && f.Engine.Casts[1].Rotation == .8f
                && f.Engine.AmbientAoes().Count(a => a.Id.StartsWith("coil/cleave/outline")) == 2, "Casting suppressed cleave.");
            f.Engine.Effect(1, 1209, 3, f.Now); f.Engine.Tick(f.Now.AddSeconds(10)); f.Render();
            Check(f.Engine.Casts.ContainsKey(1), "WhipBack ended on effect/timer.");
        });
        run("Caduceus Regorge and Syrup predictions use distinct delays live sources state7 and no timeout", () =>
        {
            using var f = Scene();
            f.Engine.Effect(1, 1210, 1, f.Now, 100, P(0));
            f.Engine.Effect(1, 1214, 2, f.Now, 101, P(0)); f.Render();
            var p = Group(f, "puddle/event");
            Check(p.Length == 2 && p[0].Activation == f.Now.AddSeconds(2.1)
                && p[1].Activation == f.Now.AddSeconds(.3) && p[0].Position == f.Actors[100].Position, "Voidzone prediction.");
            f.Now = f.Now.AddSeconds(100); f.Engine.Tick(f.Now); f.Render();
            Check(Group(f, "puddle/event").Length == 2, "Event prediction nominal timeout.");
            f.Actors[20] = new(20, 0x1E8B20, f.Actors[100].Position, EventState: 7); f.Scan();
            Check(Group(f, "puddle/event").Length == 2 && Group(f, "puddle/live").Length == 0, "State7 included.");
            f.Actors[20] = f.Actors[20] with { EventState = 0 }; f.Scan();
            Check(Group(f, "puddle/event").Length == 1 && Group(f, "puddle/live").Length == 1, "Matching prediction not consumed.");
        });
        run("Caduceus nine jump edges are directional within2.5 and current player switching resets sample", () =>
        {
            foreach (var (lower, upper) in CaduceusPlatforms.JumpEdges)
            {
                var edge = CaduceusPlatforms.Edge(lower, upper);
                var middle = (edge.A + edge.B) / 2;
                var direction = Vector2.Normalize(CaduceusPlatforms.Center(upper) - CaduceusPlatforms.Center(lower));
                Check(CaduceusPlatforms.WantJump(middle - direction, direction), "Jump approach missed.");
                Check(!CaduceusPlatforms.WantJump(middle + direction, -direction), "Reverse descent requested jump.");
                Check(!CaduceusPlatforms.WantJump(middle - direction * 2.6f, direction), "Jump too far.");
            }
            using var f = Scene();
            var e = CaduceusPlatforms.Edge(3, 1); var mid = (e.A + e.B) / 2;
            var d = Vector2.Normalize(CaduceusPlatforms.Center(1) - CaduceusPlatforms.Center(3));
            f.Actors[100] = f.Actors[100] with { Position = new(mid.X - d.X * 1.5f, 0, mid.Y - d.Y * 1.5f) }; f.Scan();
            f.Actors[100] = f.Actors[100] with { Position = new(mid.X - d.X, 0, mid.Y - d.Y) }; f.Scan();
            Check(f.Engine.RequireCaduceusJump, "Native previous position input missing.");
            f.PlayerId = 101; f.Scan(); Check(!f.Engine.RequireCaduceusJump, "New player inherited movement.");
        });
        run("Caduceus actual solver traverses ordinary shared platform seam", () =>
        {
            using var f = Scene();
            f.Actors[100] = f.Actors[100] with { Position = P(5) }; f.Scan();
            var goal = P(4);
            f.Safe.AddPersistentSafeArea(new CircleTemplate(1), new StaticTransform(goal, 0), 100);
            var snapshot = f.Snapshot;
            var result = new SafeZoneSolver().Solve(snapshot,
                new(P(5), 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = result.ContinuationPath.Count > 0 ? result.ContinuationPath : result.ExecutablePath;
            Check(result.Status == SolutionStatus.Success && !result.IsLeastDangerFallback && path.Count > 1,
                "Native terrain union disconnected adjacent walkable platforms.");
        });
        run("Caduceus pending HP decoding respects source retaliation confirmation high value and legacy JSON", () =>
        {
            ulong Raw(byte type, ushort value, byte flags = 0, byte extra = 0)
                => type | ((ulong)value << 48) | ((ulong)flags << 40) | ((ulong)extra << 32);
            Check(ReplayIncomingEffect.PendingHpDelta(Raw(3, 100), 2, 1, 2, false, false) == -100, "Damage.");
            Check(ReplayIncomingEffect.PendingHpDelta(Raw(5, 100), 2, 1, 2, false, true) == 0, "Confirmed target.");
            Check(ReplayIncomingEffect.PendingHpDelta(Raw(6, 100, 0x80), 1, 1, 2, false, true) == -100, "Retaliation source.");
            Check(ReplayIncomingEffect.PendingHpDelta(Raw(3, 100, 0x80), 2, 1, 2, false, false) == 0, "Wrong retaliation actor.");
            Check(ReplayIncomingEffect.PendingHpDelta(Raw(4, 2, 0x40, 1), 2, 1, 2, false, false) == 65538, "Extended heal.");
            Check(ReplayIncomingEffect.PendingHpDelta(Raw(3, 100, 0x80), 1, 1, 2, true, false) == 0, "Confirmed source.");
            var legacy = JsonSerializer.Deserialize<ReplayIncomingEffect>("{\"Slot\":0,\"GlobalSequence\":1,\"TargetIndex\":0,\"TargetConfirmed\":false,\"Knockback\":false}")!;
            Check(legacy.HpDelta == 0 && JsonSerializer.Deserialize<ReplayIncomingEffect>(JsonSerializer.Serialize(legacy with { HpDelta = -50 }))!.HpDelta == -50,
                "Recording schema compatibility.");
        });
        run("Caduceus jump execution respects movement authorization death throttle and owner cleanup", () =>
        {
            var control = new CaduceusJumpControl();
            var now = DateTime.UtcNow;
            var player = new ReplayActorState { Id = 100 };
            var count = 0;
            bool Jump() { ++count; return false; }
            control.Update(true, false, player, now, Jump);
            control.Update(true, true, player with { Dead = true }, now, Jump);
            control.Update(false, true, player, now, Jump);
            Check(count == 0, "Inactive gate executed native action.");
            control.Update(true, true, player, now, Jump);
            control.Update(true, true, player, now.AddSeconds(.09), Jump);
            control.Update(true, true, player, now.AddSeconds(.1), Jump);
            Check(count == 2, "Attempt throttle differs from source.");
            control.Clear();
            control.Update(true, true, player, now, Jump);
            Check(count == 3, "Owner cleanup kept stale throttle.");
        });
    }

    private static Fight Scene()
    {
        var f = new Fight(0x7D7, 241);
        f.Safe.DangerMarginForTest = 0;
        f.Actors[1] = new(1, 0x7D7, P(2), InCombat: true, TargetId: 101, HitboxRadius: 3, Hp: 1000, MaxHp: 1000);
        f.Actors[100] = new(100, 0, P(5), PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, P(2) + Vector3.UnitZ * 2, PartySlot: 1, Hostile: false);
        f.Engine.CurrentPartyAssignments = ["Unassigned", "MT", "H1", "H2", "M1", "M2", "R1", "R2"];
        f.Scan(); return f;
    }
    private static void Split(Fight f)
    {
        f.Actors[1] = f.Actors[1] with { Hp = 500 };
        f.Actors[2] = new(2, 0x7D7, P(6), InCombat: true, TargetId: 101, HitboxRadius: 3, Hp: 500, MaxHp: 1000);
        f.Scan();
    }
    private static void Role(Fight f, string role) => f.Engine.CurrentPartyAssignments = [role, "MT", "H1", "H2", "M1", "M2", "R1", "R2"];
    private static Vector3 P(int platform)
    {
        var p = CaduceusPlatforms.Center(platform);
        return new(p.X, CaduceusPlatforms.Heights[platform], p.Y);
    }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix)
        => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("caduceus/" + prefix)).ToArray();
    private static IGeometryShape Terrain(Fight f)
    {
        var a = Group(f, "platform/terrain").Single(); return a.Shape.Resolve(a.Position, 0, null);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
