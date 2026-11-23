using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class HeavenswardIntroRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

    internal static void Run(Action<string, Action> run)
    {
        run("HW intro palace exact polygon, entrance difference and centered bounds", () =>
        {
            foreach (var (territory, oid, center, excluded, expectedCenter, radius, edges) in new[]
            {
                (561u, 0x1692u, new Vector3(-300, 0, -220), new Vector3(-300, 0, -244.2f), -219.55f, 24.65f, 48),
                (598u, 0x1818u, new Vector3(-300, 0, -236), new Vector3(-300, 0, -260), -235.5345f, 24.5f, 24),
            })
            {
                var encounter = new DungeonEncounter(territory);
                encounter.Synchronize([new(1, oid, center, InCombat: true)], center, Start);
                var arena = encounter.Arena!;
                Check(arena.Contains(center) && !arena.Contains(excluded), "Entrance was filled.");
                Check(MathF.Abs(arena.Position.Z - expectedCenter) < .001f, "Difference bounds did not recenter arena.");
                var angle = MathF.PI / edges;
                var direction = new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle));
                Check(!arena.Contains(center + direction * (radius - .01f), 0), "Polygon replaced by a circle.");
                Check(arena.Contains(center + direction * (radius * MathF.Cos(angle) - .02f), 0), "Polygon edge cut too far inward.");
                Check(arena.GetContours().Count == 1 && arena.GetContours()[0].Count > 20, "Triangulation seams became arena contours.");
            }
        });
        run("HW intro palace locked geometry, observed end and raidwide prediction", () =>
        {
            foreach (var (territory, oid, cone, circle, ground, raidwide) in new[]
            {
                (561u, 0x1692u, 6417u, 6418u, 6419u, 6420u),
                (598u, 0x1818u, 7129u, 7130u, 7131u, 7132u),
            })
            {
                var center = new Vector3(-300, 0, territory == 561 ? -220 : -236);
                var encounter = new DungeonEncounter(territory);
                var player = new DungeonActor(100, 0, center, PartySlot: 0, Hostile: false);
                var boss = new DungeonActor(1, oid, center, InCombat: true);
                encounter.Synchronize([boss, player], center, Start, playerId: 100);
                var cast = new DungeonCastObservation(cone, center, MathF.PI / 2, Start, Start.AddSeconds(3.3));
                encounter.StartCast(1, cast, Start);
                encounter.Synchronize([boss with { Position = center + Vector3.UnitX * 10, Rotation = 0, Cast = cast }, player],
                    center, Start.AddSeconds(8), playerId: 100);
                Check(encounter.Casts.Count == 1 && encounter.Casts[1].UntilObservedEnd, "Predicted finish expired a live cast.");
                var locked = encounter.Casts[1];
                var shape = locked.Rule.Shape!.Resolve(locked.Location, locked.Rotation, null);
                Check(shape.Contains(center + Vector3.UnitX * 17.9f) && !shape.Contains(center + Vector3.UnitZ * 12), "Cone followed moved caster.");
                encounter.Finish(1, cone, Start.AddSeconds(8));
                Check(encounter.Casts.Count == 0, "Observed cast end leaked.");
                foreach (var (action, radius) in new[] { (circle, 12f), (ground, 6f) })
                {
                    var location = center + Vector3.UnitZ * 5;
                    encounter.StartCast(1, new(action, location, 0, Start.AddSeconds(9), Start.AddSeconds(12.3)), Start.AddSeconds(9));
                    var area = encounter.Casts[1].Rule.Shape!.Resolve(location, 0, null);
                    Check(area.Contains(location + Vector3.UnitX * (radius - .01f)) && !area.Contains(location + Vector3.UnitX * (radius + .01f)),
                        "Circle radius or ground anchor differs.");
                    encounter.Finish(1, action, Start.AddSeconds(10));
                }
                encounter.StartCast(1, new(raidwide, center, 0, Start.AddSeconds(13), Start.AddSeconds(16.3)), Start.AddSeconds(13));
                Check(encounter.OutputCasts.All(c => c.Value.Rule.Shape == null), "Raidwide invented a danger circle.");
                Check(encounter.PredictedDamage.Single() is { TargetId: 100, DamageType: "Raidwide" } prediction
                    && prediction.Activation == Start.AddSeconds(16.3), "Raidwide lost activation/party prediction.");
            }
        });
        run("HW intro Regula cast hint countdown, current cast priority and independent turrets", () =>
        {
            var center = new Vector3(230, 0, 79);
            var encounter = new DungeonEncounter(560);
            DungeonActor boss = new(1, 0x161E, center, InCombat: true, TargetId: 100);
            DungeonActor first = new(2, 0x161F, center + Vector3.UnitX * 5, Cast: new(6032, center, 0, Start, Start.AddSeconds(5.3)));
            DungeonActor second = new(3, 0x1620, center - Vector3.UnitX * 5, Cast: new(6032, center, 0, Start, Start.AddSeconds(3.3)));
            DungeonActor player = new(100, 0, center, PartySlot: 0, Hostile: false);
            encounter.Synchronize([boss, first, second, player], center, Start, playerId: 100);
            var countdown = encounter.Hints(center).Where(h => h.StartsWith("Kill turret")).ToArray();
            Check(countdown.Length == 1 && countdown[0].Contains("5.3s"), "Countdown reordered first caster or duplicated.");
            Check(encounter.PriorityTarget(1, center) == 2 && encounter.PriorityTarget(3, center) == 3, "Current detonating turret priority/tie differs.");
            Check(!encounter.PredictedDamage.Any() && !encounter.OutputCasts.Any(c => c.Value.Rule.Shape != null), "Self Detonate invented damage output.");
            encounter.Synchronize([boss, first with { Cast = null }, second, player], center, Start.AddSeconds(1), playerId: 100);
            Check(encounter.PriorityTarget(2, center) == 3 && encounter.Hints(center).Any(h => h.Contains("2.3s")), "Current cast replacement retained priority/countdown.");
            encounter.Synchronize([boss, first with { Cast = null }, second with { Cast = null }, player], center, Start.AddSeconds(2), playerId: 100);
            Check(encounter.PriorityTarget(1, center) == 1 && !encounter.Hints(center).Any(h => h.StartsWith("Kill turret")), "Idle turrets kept forced priority.");
            Check(encounter.PriorityTarget(0, center + Vector3.UnitX * 4) == 1, "Passive nearby turret outranked the fighting boss.");
            first = first with { Oid = 0x1620, Cast = new(6031, center, 0, Start.AddSeconds(3), Start.AddSeconds(6.3)) };
            second = second with { Cast = first.Cast };
            encounter.Synchronize([boss, first, second, player], center, Start.AddSeconds(3), playerId: 100);
            Check(encounter.OutputCasts.Count() == 2, "Concurrent turret AOE lost.");
            encounter.Finish(2, 6031, Start.AddSeconds(4));
            Check(encounter.OutputCasts.Single().Value.Source == 3, "One turret ending cleared another.");
            var spread = encounter.Definitions.Single().Casts.Single(c => c.Action == 6027).Shape!.Resolve(center, 0, null);
            Check(spread.Contains(center + Vector3.UnitX * 15) && !spread.Contains(center - Vector3.UnitZ * 15), "240-degree spread treated as a small cone.");
            var slug = encounter.Definitions.Single().Casts.Single(c => c.Action == 6026).Shape!.Resolve(center, 0, null);
            Check(slug.Contains(center + Vector3.UnitZ * 59 + Vector3.UnitX * 1.99f)
                && !slug.Contains(center + Vector3.UnitZ * 59 + Vector3.UnitX * 2.01f), "Slug width/length differs.");
        });
        run("HW intro Regula first registration survives dictionary slot reuse and current clock correction", () =>
        {
            var center = new Vector3(230, 0, 79);
            var encounter = new DungeonEncounter(560);
            DungeonActor boss = new(1, 0x161E, center, InCombat: true);
            DungeonActor first = new(2, 0x161F, center, Cast: new(6032, center, 0, Start, Start.AddSeconds(5.3)));
            DungeonActor second = new(3, 0x1620, center, Cast: new(6032, center, 0, Start, Start.AddSeconds(7.3)));
            encounter.Synchronize([boss, first, second], center, Start);
            first = first with { Cast = first.Cast! with { FinishAt = Start.AddSeconds(5.9) } };
            encounter.Synchronize([boss, first, second], center, Start.AddSeconds(1));
            Check(encounter.Hints(center).Any(h => h.Contains("4.9s")), "Countdown ignored the current observed finish correction.");
            encounter.Finish(2, 6032, Start.AddSeconds(2));
            DungeonActor third = new(4, 0x161F, center, Cast: new(6032, center, 0, Start.AddSeconds(2), Start.AddSeconds(5.3)));
            encounter.Synchronize([boss, first with { Cast = null }, second, third], center, Start.AddSeconds(2));
            Check(encounter.Hints(center).Any(h => h.Contains("5.3s")), "New caster reused an erased dictionary slot ahead of the older caster.");
            encounter.RemoveActor(3);
            Check(encounter.Hints(center).Any(h => h.Contains("3.3s")), "Destroyed first caster did not promote its successor.");
            encounter.ResetFight();
            Check(!encounter.Hints(center).Any(h => h.StartsWith("Kill turret")), "Reset retained the registration list.");
        });
        run("HW intro enemy directional markers filter targetability and clamp outside actors", () =>
        {
            var center = new Vector3(230, 0, 79);
            var encounter = new DungeonEncounter(560);
            encounter.Synchronize([
                new(1, 0x161E, center, InCombat: true),
                new(2, 0x161F, center + Vector3.UnitX * 50, Rotation: 1),
                new(3, 0x1620, center, Targetable: false),
                new(4, 0x1621, center), new(5, 0x233C, center),
            ], center, Start);
            var markers = encounter.AmbientAoes().Where(a => a.Id.StartsWith("hw-intro/enemy/")).ToArray();
            Check(markers.Length == 2 && markers.All(a => a.Shape is LocalPolygonTemplate), "Enemy markers became hitboxes or included helpers.");
            var outside = markers.Single(a => a.Id.EndsWith("/2"));
            Check(outside.OutlineOnly && !outside.Navigate && outside.Rotation == 1 && outside.Position.X < 251,
                "Outside marker lost boundary clamping or direction.");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
