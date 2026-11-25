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
                (565u, 0x16C6u, new Vector3(300, 0, 374), new Vector3(300, 0, 350), 374.3645f, 24.18f, 32),
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
                foreach (var (pair, index) in new[] { (circle, 12f), (ground, 6f) }.Select((pair, index) => (pair, index)))
                {
                    var (action, radius) = pair;
                    var started = 9 + index * 2;
                    var location = center + Vector3.UnitZ * 5;
                    encounter.StartCast(1, new(action, location, 0, Start.AddSeconds(started), Start.AddSeconds(started + 3.3)), Start.AddSeconds(started));
                    var area = encounter.Casts[1].Rule.Shape!.Resolve(location, 0, null);
                    Check(area.Contains(location + Vector3.UnitX * (radius - .01f)) && !area.Contains(location + Vector3.UnitX * (radius + .01f)),
                        "Circle radius or ground anchor differs.");
                    encounter.Finish(1, action, Start.AddSeconds(started + 1));
                }
                encounter.StartCast(1, new(raidwide, center, 0, Start.AddSeconds(13), Start.AddSeconds(16.3)), Start.AddSeconds(13));
                Check(encounter.OutputCasts.All(c => c.Value.Rule.Shape == null), "Raidwide invented a danger circle.");
                Check(encounter.PredictedDamage.Single() is { TargetId: 100, DamageType: "Raidwide" } prediction
                    && prediction.Activation == Start.AddSeconds(16.3), "Raidwide lost activation/party prediction.");
            }
        });
        run("HW intro Edda gaze range, facing deadline, same-position and helper independence", () =>
        {
            var center = new Vector3(300, 0, 374);
            var encounter = new DungeonEncounter(565);
            DungeonActor boss = new(1, 0x16C6, center, InCombat: true, TargetId: 100);
            DungeonActor player = new(100, 0, center - Vector3.UnitZ * 120, PartySlot: 0, Hostile: false);
            DungeonActor gargoyle = new(2, 0x16E8, center);
            DungeonActor demon = new(3, 0x16E9, center);
            void Sync(double time) => encounter.Synchronize([boss, player, gargoyle, demon], player.Position,
                Start.AddSeconds(time), playerId: 100);
            Sync(0);
            boss = boss with { Cast = new(6403, center, 0, Start, Start.AddSeconds(3.3)) };
            Sync(.1);
            Check(encounter.Hints(player.Position).Contains("Turn away from gaze!"), "Gaze incorrectly limited to 40.");
            Check(encounter.GazeFacing(player.Position, 0) == null, "Gaze facing started before lead window.");
            Check(encounter.AmbientAoes().Count(a => a.Id.StartsWith("edda/gaze/")) == 3, "Eye, pupil or arc missing.");
            Sync(2.6);
            Check(encounter.GazeFacing(player.Position, 0) is { } facing && MathF.Cos(facing) < -.9f, "Gaze did not request PR facing at deadline.");
            player = player with { Rotation = MathF.PI };
            Sync(2.7);
            Check(!encounter.Hints(player.Position).Contains("Turn away from gaze!"), "Safe facing retained gaze warning.");
            player = player with { Position = center, Rotation = 0 };
            Sync(2.8);
            Check(!encounter.Hints(player.Position).Contains("Turn away from gaze!"), "Zero eye direction should normalize to zero.");
            boss = boss with { Cast = null };
            Sync(2.9);
            Check(!encounter.Gazes.Any() && !encounter.AmbientAoes().Any(a => a.Id.StartsWith("edda/gaze/"))
                && encounter.GazeFacing(player.Position, 0) == null, "Snapshot end inside the old 150ms grace window leaked gaze/facing.");
            boss = boss with { Cast = null };
            gargoyle = gargoyle with { Cast = new(6404, center, MathF.PI / 2, Start.AddSeconds(4), Start.AddSeconds(8.3)) };
            demon = demon with { Cast = new(6405, center + Vector3.UnitZ * 8, 0, Start.AddSeconds(4), Start.AddSeconds(8.3)) };
            Sync(4);
            Check(!encounter.Gazes.Any() && encounter.OutputCasts.Count() == 2, "Gaze finish or concurrent helpers lost.");
            var rect = encounter.Casts[2].Rule.Shape!.Resolve(center, MathF.PI / 2, null);
            Check(rect.Contains(center + Vector3.UnitX * 57.2f + Vector3.UnitZ * 2.99f)
                && !rect.Contains(center + Vector3.UnitX * 57.2f + Vector3.UnitZ * 3.01f), "Desolation width/range.");
            var eye = encounter.Casts[3].Rule.Shape!.Resolve(demon.Cast.Location, 0, null);
            Check(eye.Contains(demon.Cast.Location + Vector3.UnitX * 5.99f)
                && !eye.Contains(demon.Cast.Location + Vector3.UnitX * 6.01f), "Terror Eye range.");
            Sync(12);
            Check(encounter.OutputCasts.Count() == 2, "Observed overdue helper casts expired.");
            gargoyle = gargoyle with { Cast = null };
            Sync(12.1);
            Check(encounter.OutputCasts.Single().Value.Source == 3, "One helper finish cleared another.");
            demon = demon with { Cast = null };
            boss = boss with { Cast = new(6400, center, 0, Start.AddSeconds(13), Start.AddSeconds(15.3), 100) };
            Sync(13);
            Check(encounter.PredictedDamage.Single().TargetId == 100, "Dark Harvest target prediction.");
            foreach (var (action, inner, outer) in new[] { (6398u, 0f, 16f), (6399u, 4.5f, 50f) })
            {
                var geometry = encounter.Definitions.Single().Casts.Single(r => r.Action == action).Shape!.Resolve(center, 0, null);
                Check(geometry.Contains(center + Vector3.UnitX * (outer - .01f))
                    && !geometry.Contains(center + Vector3.UnitX * (outer + .01f))
                    && (inner == 0 || !geometry.Contains(center + Vector3.UnitX * (inner - .01f))), "In Health geometry.");
            }
            boss = boss with { Cast = new(6402, center, 0, Start.AddSeconds(16), Start.AddSeconds(19.3)) };
            Sync(16);
            Check(encounter.PredictedDamage.Single().DamageType == "Raidwide"
                && encounter.OutputCasts.All(c => c.Value.Rule.Shape == null), "Black Honeymoon prediction or invented AOE.");
        });
        run("HW intro multiple gazes retain a narrow shared safe heading", () =>
        {
            var center = new Vector3(300, 0, 374);
            var encounter = new DungeonEncounter(565);
            var actors = new List<DungeonActor> { new(100, 0, center, PartySlot: 0, Hostile: false) };
            foreach (var (degrees, i) in new[] { 0f, 89f, 178f, 267f }.Select((d, i) => (d, i)))
            {
                var radians = degrees * MathF.PI / 180;
                var location = center + new Vector3(MathF.Sin(radians), 0, MathF.Cos(radians)) * 10;
                actors.Add(new((ulong)i + 1, i == 0 ? 0x16C6u : 0x16E8u, location, InCombat: true,
                    Cast: new(6403, location, 0, Start, Start.AddSeconds(3.3))));
            }
            encounter.Synchronize(actors, center, Start.AddSeconds(2.9), playerId: 100);
            var heading = encounter.GazeFacing(center, 0);
            Check(heading is { } value && actors.Skip(1).All(a =>
                MathF.Abs(MathF.Atan2(MathF.Sin(value - MathF.Atan2(a.Position.X - center.X, a.Position.Z - center.Z)),
                    MathF.Cos(value - MathF.Atan2(a.Position.X - center.X, a.Position.Z - center.Z)))) > MathF.PI / 4 + .01f),
                "Safe 312..315 degree interval had no candidate.");
        });
        run("HW intro Ixtab AhPuch independent pox filters, draw anchors, adds and raidwide deadlines", () =>
        {
            foreach (var (territory, oid, addOid, pox, eruption, flame, scream, shadow) in new[]
            {
                (564u, 0x16B9u, 0x1764u, 6434u, 6430u, 6431u, 6433u, 6432u),
                (601u, 0x181Bu, 0x1906u, 7146u, 7142u, 7143u, 7145u, 7144u),
            })
            {
                var encounter = new DungeonEncounter(territory);
                var center = new Vector3(-300, 0, territory == 564 ? -226 : -236);
                DungeonActor boss = new(1, oid, center, InCombat: true, TargetId: 100);
                DungeonActor player = new(100, 0, center, PartySlot: 0, Hostile: false);
                DungeonActor zone = new(2, 0x1E8EA9, center + new Vector3(.1234f, 0, .5678f), Targetable: false,
                    AnimationState1: 1, EventState: 0);
                DungeonActor add = new(3, addOid, center + Vector3.UnitX * 12);
                void Sync(double t) => encounter.Synchronize([boss, player, zone, add], center, Start.AddSeconds(t), playerId: 100);
                Sync(0);
                Check(encounter.PriorityTarget(1, center) == 3, "Adds priority did not override the boss.");
                var areas = encounter.AmbientAoes().Where(a => a.Id.StartsWith("palace/pox/")).ToArray();
                Check(areas.Length == 2 && areas.Single(a => a.Display).Position == zone.Position
                    && areas.Single(a => a.Navigate).Position != zone.Position, "Pox draw and quantized navigation anchors merged.");
                Check(encounter.Hints(center).Contains("GTFO from voidzone!"), "Inside-pox warning missing.");
                zone = zone with { AnimationState1 = 0, EventState = 7 };
                Sync(1);
                Check(!encounter.AmbientAoes().Any(a => a.Id.StartsWith("palace/pox/")), "Inactive pox leaked.");
                zone = zone with { AnimationState1 = territory == 564 ? (byte)1 : (byte)0, EventState = territory == 564 ? (byte)7 : (byte)0 };
                Sync(2);
                Check(encounter.AmbientAoes().Count(a => a.Id.StartsWith("palace/pox/")) == 2, "Wrong floor's pox filter used.");
                foreach (var (action, radius) in new[] { (pox, 8f), (eruption, 4f) })
                {
                    var geometry = encounter.Definitions.Single().Casts.Single(c => c.Action == action).Shape!.Resolve(center, 0, null);
                    Check(geometry.Contains(center + Vector3.UnitX * (radius - .01f))
                        && !geometry.Contains(center + Vector3.UnitX * (radius + .01f)), "Pox/Eruption cast radius.");
                }
                var rect = encounter.Definitions.Single().Casts.Single(c => c.Action == flame).Shape!.Resolve(center, 0, null);
                Check(rect.Contains(center + new Vector3(3.99f, 0, 53.79f))
                    && !rect.Contains(center + new Vector3(4.01f, 0, 53.79f)), "Entropic Flame geometry.");
                foreach (var (action, index) in new[] { scream, shadow }.Select((action, index) => (action, index)))
                {
                    var started = 3 + index * 5;
                    boss = boss with { Cast = new(action, center, 0, Start.AddSeconds(started), Start.AddSeconds(started + 3.3)) };
                    Sync(started);
                    Check(encounter.PredictedDamage.Single() is { DamageType: "Raidwide", TargetId: 100 } damage
                        && damage.Activation == Start.AddSeconds(started + 3.3), "Raidwide prediction missing.");
                    Check(encounter.OutputCasts.All(c => c.Value.Rule.Shape == null), "Raidwide invented a danger circle.");
                    if (action == scream) Check(encounter.Hints(center).Any(h => h.Contains("Fear")), "Scream lost fear/add deadline hint.");
                    boss = boss with { Cast = null };
                    Sync(started + 4);
                }
                Check(encounter.AmbientAoes().Count(a => a.Id.StartsWith("hw-intro/enemy/")) == 2, "Boss/add markers incomplete.");
                boss = boss with { Dead = true };
                Sync(13);
                Check(encounter.Active == null && !encounter.AmbientAoes().Any(), "Boss death leaked floor-specific output.");
            }
            var ah = new DungeonEncounter(601);
            ah.Synchronize([new(1, 0x181B, new(-300, 0, -236), InCombat: true)], new(-300, 0, -236), Start);
            var outline = ah.Arena!.GetContours().Single();
            Check(outline.Count == 88 && MathF.Abs(ah.Arena.Position.Z + 236.05f) < .001f,
                "Ah Puch lost exact 88-vertex outline/recenter.");
            Check(!ah.Arena.Contains(new(-277, 0, -250), 0) && ah.Arena.Contains(new(-280, 0, -240), 0),
                "Ah Puch irregular edge replaced by circle.");
            Check(ah.Arena.Contains(new(-309, 0, -258), 0) && !ah.Arena.Contains(new(-290.96f, 0, -214.1f), 0)
                && outline.Any(p => Vector3.Distance(p, new(-289.68f, 0, -259.66f)) < .001f),
                "Ah Puch world vertices were mirrored by the local coordinate convention.");
        });
        run("HW intro Aldynn donut sector, Tizona priority and state-filtered obstacles", () =>
        {
            var center = new Vector3(-35.75f, 0, -205.5f);
            var encounter = new DungeonEncounter(533);
            DungeonActor boss = new(1, 0x154E, center, InCombat: true, TargetId: 100);
            DungeonActor player = new(100, 0, center, PartySlot: 0, Hostile: false);
            DungeonActor tizona = new(2, 0x1552, center + Vector3.UnitX * 10);
            DungeonActor zone = new(3, 0x1E9FA8, center, Targetable: false);
            encounter.Synchronize([boss, player, tizona, zone], center, Start, playerId: 100);
            Check(encounter.PriorityTarget(1, center) == 2, "Tizona priority5 missing.");
            var obstacle = encounter.AmbientAoes().Single(a => a.Obstacle);
            Check(obstacle.Shape is CircleTemplate { Radius: 4 } && obstacle.Display, "Demoralize is not a visible R4 obstacle.");
            var curse = encounter.Definitions.Single().Casts.Single(c => c.Action == 5765).Shape!.Resolve(center, 0, null);
            Check(!curse.Contains(center + Vector3.UnitZ) && curse.Contains(center + Vector3.UnitZ * 6.99f)
                && !curse.Contains(center - Vector3.UnitZ * 5) && !curse.Contains(center + Vector3.UnitZ * 7.01f),
                "The Curse lost inner radius, outer radius or 180 degree facing.");
            encounter.Synchronize([boss, player, tizona, zone with { EventState = 7 }], center, Start.AddSeconds(1), playerId: 100);
            Check(!encounter.AmbientAoes().Any(a => a.Obstacle), "Inactive Demoralize persisted.");
        });
        run("HW intro VIth Regula dynamic swords distinguish finish, effect, death and destruction", () =>
        {
            var center = new Vector3(252.75f, 0, 553);
            var encounter = new DungeonEncounter(459);
            DungeonActor boss = new(1, 0xF1C, center, InCombat: true, TargetId: 100);
            DungeonActor player = new(100, 0, center, PartySlot: 0, Hostile: false);
            DungeonActor a = new(2, 0xF5D, center, Targetable: false);
            DungeonActor b = new(3, 0xF5D, center + Vector3.UnitX * 2);
            DungeonActor puddle = new(4, 0x1E88F5, center, Dead: true, Targetable: false, EventState: 7);
            void Sync(double t) => encounter.Synchronize([boss, player, a, b, puddle], center, Start.AddSeconds(t), playerId: 100);
            Sync(0);
            Check(!encounter.AmbientAoes().Any(x => x.Id.StartsWith("hw-quest/terminus/"))
                && encounter.AmbientAoes().Count(x => x.Id.StartsWith("hw-intro/enemy/")) == 3,
                "Swords need visible untargetable markers before activation.");
            Check(encounter.AmbientAoes().Single(x => x.Id == "hw-intro/enemy/2").Color == new Vector4(1, 1, 0, 1),
                "Sword marker lost source danger color.");
            Check(encounter.AmbientAoes().Single(x => x.Obstacle).Shape is CircleTemplate { Radius: 8 },
                "Puddle must not gain EventState/death filtering.");
            boss = boss with { Cast = new(4005, center, 0, Start.AddSeconds(1), Start.AddSeconds(4.3)) };
            Sync(1);
            Check(encounter.AmbientAoes().Count(x => x.Id.StartsWith("hw-quest/terminus/")) == 2, "4005 did not activate swords.");
            a = a with { Position = center + new Vector3(3, 0, 4), Rotation = MathF.PI / 2 };
            boss = boss with { Cast = null };
            Sync(5);
            var sword = encounter.AmbientAoes().Single(x => x.Id == "hw-quest/terminus/2");
            Check(sword.Rotation == MathF.PI / 2 && Vector3.Distance(sword.Position, a.Position) < .05f,
                "Sword did not follow live position and rotation.");
            encounter.Effect(2, 3825, 1, Start.AddSeconds(5));
            Check(encounter.AmbientAoes().Count(x => x.Id.StartsWith("hw-quest/terminus/")) == 2, "Effect ended sword group.");
            a = a with { Dead = true };
            Sync(6);
            Check(encounter.AmbientAoes().Count(x => x.Id.StartsWith("hw-quest/terminus/")) == 1, "Death ended other swords.");
            encounter.Synchronize([boss, player, b, puddle], center, Start.AddSeconds(7), playerId: 100);
            Check(!encounter.AmbientAoes().Any(x => x.Id.StartsWith("hw-quest/terminus/")), "Destroyed dead sword did not close group.");
            encounter.StartCast(1, new(4005, center, 0, Start.AddSeconds(8), Start.AddSeconds(11.3)), Start.AddSeconds(8));
            Check(encounter.AmbientAoes().Count(x => x.Id.StartsWith("hw-quest/terminus/")) == 1, "New visual did not reopen group.");
            encounter.RemoveActor(3);
            Check(!encounter.AmbientAoes().Any(x => x.Id.StartsWith("hw-quest/terminus/")), "ObjectRemove route did not close group.");
        });
        run("HW intro Tisiphone precombat hints, exact class roles, rolling zombie danger and markers", () =>
        {
            var center = new Vector3(-300, 0, -236);
            var encounter = new DungeonEncounter(602);
            DungeonActor boss = new(1, 0x181C, center, Name: "Tisiphone");
            DungeonActor player = new(100, 0, center, PartySlot: 0, PartyRole: "Ranged", Hostile: false, ClassJob: 25);
            DungeonActor zombie = new(2, 0x18ED, center + Vector3.UnitX * 4);
            DungeonActor succubus = new(3, 0x18EE, center + Vector3.UnitX * 8, Targetable: false);
            DungeonActor gargoyle = new(4, 0x18EB, center);
            void Sync(double t, bool enabled = true) => encounter.Synchronize([boss, player, zombie, succubus, gargoyle],
                center, Start.AddSeconds(t), _ => enabled, 100);
            Sync(0);
            Check(encounter.Active == null && encounter.Hints(center).Any(h => h.Contains("4 zombies") && h.Contains("life-steal")),
                "Constructor hint missing before phase entry.");
            Sync(.1, false);
            Check(!encounter.Hints(center).Any(h => h.Contains("4 zombies")), "Disabled Tisiphone retained constructor hint.");
            boss = boss with { InCombat = true };
            Sync(1);
            Check(!encounter.Hints(center).Any(h => h.Contains("4 zombies")) && encounter.PriorityTarget(2, center) == 1,
                "Phase entry kept constructor hint or caster did not prefer boss.");
            var zones = encounter.AmbientAoes().Where(a => a.Id.StartsWith("tisiphone/zombie/")).ToArray();
            Check(zones.Length == 2 && zones.All(a => !a.Display && a.Navigate)
                && zones.Single(a => a.Id.EndsWith("/near")).Shape is CircleTemplate { Radius: 3 }
                && zones.Single(a => a.Id.EndsWith("/future")) is { Shape: CircleTemplate { Radius: 8 }, Activation: var first }
                && first == Start.AddSeconds(6), "Zombie navigation radii/display/deadline.");
            zombie = zombie with { Dead = true, Targetable = false, Position = center + Vector3.UnitX * 6 };
            boss = boss with { InCombat = false };
            Sync(3);
            zones = encounter.AmbientAoes().Where(a => a.Id.StartsWith("tisiphone/zombie/")).ToArray();
            Check(encounter.PreserveStageAcrossBattleEnd && zones.Length == 2 && zones.All(a => a.Position == zombie.Position)
                && zones.Single(a => a.Id.EndsWith("/future")).Activation == Start.AddSeconds(8),
                "Zombie deadline froze or dead/untargetable zombie got filtered.");
            Check(encounter.AmbientAoes().Count(a => a.Id.StartsWith("hw-intro/enemy/")) == 1,
                "Dead/untargetable adds or non-AddsMulti gargoyle gained marker.");
            zombie = zombie with { Dead = false, Targetable = true };
            foreach (var job in new uint[] { 5, 6, 7, 23, 24, 25, 26, 27, 28, 31, 33, 35, 36, 38, 40, 42 })
            {
                player = player with { ClassJob = job };
                Sync(4);
                Check(encounter.PriorityTarget(2, center) == 1 && encounter.AmbientAoes().Any(a => a.Id.StartsWith("tisiphone/zombie/")),
                    "Ranged/healer class lost boss priority or zombie danger.");
            }
            foreach (var job in new uint[] { 0, 1, 19, 22, 41, 43 })
            {
                player = player with { ClassJob = job };
                Sync(5);
                Check(encounter.PriorityTarget(1, center) == 2 && !encounter.AmbientAoes().Any(a => a.Id.StartsWith("tisiphone/zombie/")),
                    "PartyRole fallback incorrectly classified unknown/melee/tank class.");
            }
        });
        run("HW intro Tisiphone nine cast components, lifesteal hint, raidwide and exact shared arena", () =>
        {
            var center = new Vector3(-300, 0, -236);
            var encounter = new DungeonEncounter(602);
            DungeonActor boss = new(1, 0x181C, center, InCombat: true, TargetId: 100);
            DungeonActor player = new(100, 0, center, PartySlot: 0, Hostile: false, ClassJob: 19);
            DungeonActor helper = new(2, 0x18EB, center);
            void Sync(double t) => encounter.Synchronize([boss, player, helper], center, Start.AddSeconds(t), playerId: 100);
            Sync(0);
            Check(encounter.Definitions.Single().Casts.Length == 9 && encounter.Arena!.Contains(new(-309, 0, -258), 0)
                && !encounter.Arena.Contains(new(-290.96f, 0, -214.1f), 0), "Tisiphone catalog or shared world outline.");
            foreach (var (action, radius) in new[] { (7108u, 10f), (7113u, 6f), (7150u, 5f), (7109u, 10f) })
            {
                var geometry = encounter.Definitions.Single().Casts.Single(c => c.Action == action).Shape!.Resolve(center, 0, null);
                Check(geometry.Contains(center + Vector3.UnitX * (radius - .01f))
                    && !geometry.Contains(center + Vector3.UnitX * (radius + .01f)), "Tisiphone circle radius.");
            }
            boss = boss with { Cast = new(7153, center, 0, Start.AddSeconds(1), Start.AddSeconds(6.3)) };
            helper = helper with { Cast = new(7112, center, MathF.PI / 2, Start.AddSeconds(1), Start.AddSeconds(5.3)) };
            Sync(1);
            Check(encounter.PredictedDamage.Single().DamageType == "Raidwide"
                && encounter.Hints(center).Any(h => h.Contains("Heavy Raidwide")), "Blood Rain prediction/hint missing.");
            Sync(10);
            Check(encounter.OutputCasts.Count(c => c.Value.Rule.Shape != null) == 1 && encounter.PredictedDamage.Any(), "Overdue casts expired.");
            helper = helper with { Cast = null };
            boss = boss with { Cast = new(7110, center, 0, Start.AddSeconds(11), Start.AddSeconds(13.3), 3) };
            Sync(11);
            Check(!encounter.PredictedDamage.Any() && encounter.Hints(center).Any(h => h.Contains("life stealing")),
                "Fatal Allure invented party damage or lost lifesteal hint.");
            boss = boss with { Dead = true };
            Sync(12);
            Check(encounter.Active == null && !encounter.AmbientAoes().Any() && !encounter.PredictedDamage.Any(), "Tisiphone death cleanup.");
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
        run("HW intro Divine pair preserves concurrent casts after primary death destruction and battle end", () =>
        {
            var encounter = new DungeonEncounter(456);
            DungeonActor boss = new(1, 0x1010, Vector3.Zero, InCombat: true);
            DungeonActor second = new(2, 0x1011, Vector3.UnitX * 2, InCombat: true,
                Cast: new(1316, Vector3.Zero, 0, Start, Start.AddSeconds(3.3)));
            DungeonActor player = new(100, 0, Vector3.Zero, PartySlot: 0, Hostile: false);
            DungeonActor chain = new(3, 0x102C, Vector3.UnitX * 4);
            encounter.Synchronize([second, player], Vector3.Zero, Start, playerId: 100);
            Check(encounter.Active == null, "Secondary alone must not activate the source module.");
            encounter.Synchronize([boss, second, player, chain], Vector3.Zero, Start, playerId: 100);
            var arena = encounter.Arena;
            Check(encounter.PriorityTarget(1, Vector3.Zero) == 3, "Steel chain priority was not applied.");
            Check(encounter.AmbientAoes().Count(a => a.Id.StartsWith("hw-intro/enemy/")) == 3
                && encounter.AmbientAoes().Single(a => a.Id.EndsWith("enemy/3")).Color == new Vector4(1, 128f / 255, 0, 1),
                "Boss/chain markers or object color.");
            boss = boss with { Dead = true, Hp = 0 };
            second = second with { InCombat = false, Hp = 0 };
            encounter.Synchronize([boss, second, player, chain], Vector3.Zero, Start.AddSeconds(1), playerId: 100);
            Check(encounter.Active != null && ReferenceEquals(arena, encounter.Arena)
                && encounter.Casts.Count == 1 && encounter.PreserveStageAcrossBattleEnd, "Primary death restarted or ended the pair.");
            encounter.RemoveActor(1);
            Check(encounter.Active != null && encounter.Casts.Count == 1, "Primary destruction reset surviving boss cast.");
            encounter.Synchronize([second, player, chain], Vector3.Zero, Start.AddSeconds(10), playerId: 100);
            Check(encounter.Active != null && encounter.Casts.Count == 1, "Overdue secondary cast or missing primary ended the pair.");
            encounter.RemoveActor(2);
            Check(encounter.Active == null && !encounter.AmbientAoes().Any(), "Both destroyed did not end despite surviving chain.");
        });
        run("HW intro Divine seven exact rules charge snapshot location and raidwide without circle", () =>
        {
            var encounter = new DungeonEncounter(456);
            var origin = new Vector3(.01f, 0, .02f);
            DungeonActor boss = new(1, 0x1010, Vector3.Zero, InCombat: true);
            DungeonActor source = new(2, 0xEC4, origin);
            DungeonActor player = new(100, 0, Vector3.Zero, PartySlot: 0, Hostile: false);
            encounter.Synchronize([boss, source, player], Vector3.Zero, Start, playerId: 100);
            Check(encounter.Definitions.Single().Casts.Length == 7, "Seven source components were not migrated.");
            var charge = new DungeonCastObservation(3993, origin + Vector3.UnitX * 10, null, Start, Start.AddSeconds(2.3));
            encounter.StartCast(2, charge, Start);
            var pending = encounter.Casts[2];
            var shape = pending.Rule.Shape!.Resolve(pending.Location, pending.Rotation, null);
            Check(shape.Contains(pending.Location + new Vector3(9.9f, 0, 1.99f))
                && !shape.Contains(pending.Location + new Vector3(9.9f, 0, 2.01f))
                && !shape.Contains(pending.Location + new Vector3(10.1f, 0, 0)), "Charge length/direction/full width4.");
            encounter.Synchronize([boss, source with { Position = Vector3.UnitZ * 10, Cast = charge }, player],
                Vector3.Zero, Start.AddSeconds(1), playerId: 100);
            encounter.StartCast(2, charge, Start.AddSeconds(1.1));
            Check(encounter.Casts[2] == pending, "Duplicate charge followed source movement.");
            encounter.Finish(2, 3993, Start.AddSeconds(2));
            encounter.StartCast(1, new(3992, Vector3.Zero, 0, Start.AddSeconds(3), Start.AddSeconds(7.3)), Start.AddSeconds(3));
            Check(!encounter.OutputCasts.Any(c => c.Value.Rule.Shape != null)
                && encounter.PredictedDamage.Single().Activation == Start.AddSeconds(7.3), "Raidwide created a circle or lost damage.");
            Check(!encounter.Arena!.Contains(new(0, 0, -10)) && encounter.Arena.Contains(new(0, 0, -9.2f))
                && encounter.Arena.Contains(Vector3.Zero), "Exact capsule entrance difference.");
        });
        run("HW intro Divine observed short charge cancellation bypasses packet grace", () =>
        {
            var encounter = new DungeonEncounter(456);
            DungeonActor boss = new(1, 0x1010, Vector3.Zero, InCombat: true);
            DungeonActor source = new(2, 0xEC4, Vector3.Zero);
            DungeonActor player = new(100, 0, Vector3.Zero, PartySlot: 0);
            encounter.Synchronize([boss, source, player], Vector3.Zero, Start, playerId: 100);
            var charge = new DungeonCastObservation(3993, Vector3.UnitX * 10, null, Start, Start.AddSeconds(3));
            encounter.StartCast(2, charge, Start);
            encounter.Synchronize([boss, source with { Cast = charge }, player], Vector3.Zero, Start.AddSeconds(.05), playerId: 100);
            encounter.Synchronize([boss, source, player], Vector3.Zero, Start.AddSeconds(.1), playerId: 100);
            Check(!encounter.OutputCasts.Any(), "Divine locked geometry bypassed real cast observation and retained a cancelled charge.");
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
