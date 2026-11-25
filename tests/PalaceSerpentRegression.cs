using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class PalaceSerpentRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

    internal static void Run(Action<string, Action> run)
    {
        foreach (var (territory, oid, fire, ice, fear) in new[]
        {
            (563u, 0x16ACu, 6427u, 6428u, 6429u), (600u, 0x181Au, 7139u, 7140u, 7141u),
        })
        {
            run($"Palace serpent {territory} exact world arena and cleave target-specific navigation", () =>
            {
                var center = new Vector3(-300, 5, -236);
                var encounter = new DungeonEncounter(territory);
                DungeonActor boss = new(1, oid, center, InCombat: true, TargetId: 100);
                DungeonActor tank = new(100, 0, center + Vector3.UnitZ * 5, PartySlot: 0, Hostile: false);
                DungeonActor other = new(101, 0, center + Vector3.UnitX * 5, PartySlot: 1, Hostile: false);
                void Sync(double t, ulong view = 101) => encounter.Synchronize([boss, tank, other], other.Position,
                    Start.AddSeconds(t), playerId: view);
                Sync(0);
                Check(encounter.Arena!.Position.Y == 5, "Arena lost sampled height.");
                if (territory == 563)
                    Check(!encounter.Arena.Contains(new(-300, 5, -260.45f), 0)
                        && encounter.Arena.Contains(new(-300, 5, -260.39f), 0), "Ningishzida entrance difference.");
                else
                    Check(encounter.Arena.GetContours().Single().Count == 66
                        && encounter.Arena.GetContours().Single().Any(p => Vector3.Distance(p, new(-290.26f, 5, -259.65f)) < .001f),
                        "Alfard exact world vertices.");
                var cleaves = encounter.AmbientAoes().Where(a => a.Name == "Dissever").ToArray();
                Check(cleaves.Length == 2 && cleaves.Single(a => a.Display).OutlineOnly, "Cleave missing outline/nav split.");
                var nav = cleaves.Single(a => a.Navigate);
                Check(nav.Shape.Resolve(nav.Position, nav.Rotation, null).Contains(center + Vector3.UnitZ * 10.7f), "Cleave did not face current target.");
                Sync(1, 100);
                var avoid = encounter.AmbientAoes().Single(a => a.Id.EndsWith("/avoid/101"));
                Check(avoid.Shape is FanTemplate { Radius: 100 }
                    && avoid.Shape.Resolve(avoid.Position, avoid.Rotation, null).Contains(center + Vector3.UnitX * 90),
                    "Main target lost R100 teammate avoid cone.");
                boss = boss with { Cast = new(9999, center, 0, Start.AddSeconds(2), Start.AddSeconds(5)) };
                Sync(2);
                Check(!encounter.AmbientAoes().Any(a => a.Name == "Dissever"), "Unsupported caster action did not hide cleave.");
                boss = boss with { Cast = null };
                Sync(3);
                encounter.StartCast(1, new(fear, center, 0, Start.AddSeconds(3), Start.AddSeconds(9)), Start.AddSeconds(3));
                Check(!encounter.AmbientAoes().Any(a => a.Name == "Dissever"), "Packet before snapshot retained cleave.");
                encounter.Finish(1, fear, Start.AddSeconds(4));
                Check(encounter.AmbientAoes().Any(a => a.Name == "Dissever"), "Packet finish did not restore cleave.");
                encounter.StartCast(1, new(9999, center, 0, Start.AddSeconds(5), Start.AddSeconds(9)), Start.AddSeconds(5));
                Check(!encounter.AmbientAoes().Any(a => a.Name == "Dissever"), "Unknown packet did not hide cleave.");
                encounter.Finish(1, 9999, Start.AddSeconds(6));
                Check(encounter.AmbientAoes().Any(a => a.Name == "Dissever"), "Unknown packet finish did not restore cleave.");
                encounter.StartCast(1, new(9999, center, 0, Start.AddSeconds(5), Start.AddSeconds(9)), Start.AddSeconds(7));
                Check(encounter.AmbientAoes().Any(a => a.Name == "Dissever"), "Late unknown packet resurrected a finished generation.");
            });
            run($"Palace serpent {territory} live target casts, four effects, typed puddle handoff and no timeout", () =>
            {
                var center = new Vector3(-300, 0, -236);
                var encounter = new DungeonEncounter(territory);
                DungeonActor boss = new(1, oid, center, InCombat: true);
                DungeonActor player = new(100, 0, center + Vector3.UnitX * 12, PartySlot: 0, Hostile: false);
                DungeonActor? zone = null;
                void Sync(double t) => encounter.Synchronize(zone == null ? [boss, player] : [boss, player, zone],
                    player.Position, Start.AddSeconds(t), playerId: 100);
                DungeonAmbientAoe[] Puddles() => encounter.AmbientAoes().Where(a => a.Id.StartsWith("trial/voidzone/")).ToArray();
                Sync(0);
                Check(encounter.Hints(player.Position).Any(h => h.Contains("x2 Fire")) && encounter.AmbientAoes()
                    .Single(a => a.Id == "palace-serpent/bait").Activation == Start.AddSeconds(10), "Initial bait cycle/rolling deadline.");
                boss = boss with { Cast = new(fire, center, 0, Start.AddSeconds(1), Start.AddSeconds(4.3), 100) };
                Sync(1);
                player = player with { Position = center + new Vector3(13, 0, 2) };
                Sync(2);
                Check(Puddles().Single().Position == player.Position && Puddles().Single().Activation == Start.AddSeconds(4.3)
                    && !encounter.OutputCasts.Any(c => c.Value.Rule.Shape != null), "Cast prediction froze target or duplicated generic cast.");
                boss = boss with { Cast = boss.Cast! with { TargetId = 999, Location = center + Vector3.UnitZ * 15 } };
                Sync(2.1);
                Check(Puddles().Single().Position == boss.Cast.Location, "Missing updated target did not use current fallback location.");
                boss = boss with { Cast = boss.Cast! with { TargetId = 100 } };
                Sync(2.2);
                Check(Puddles().Single().Position == player.Position, "Snapshot TargetId correction was ignored.");
                boss = boss with { Cast = null };
                Sync(3);
                Check(Puddles().Length == 0, "Cast ending retained cast-predicted puddle.");
                for (uint i = 0; i < 4; ++i)
                {
                    var action = i < 2 ? fire : ice;
                    encounter.Effect(1, action, i + 1, Start.AddSeconds(4 + i), target: 100,
                        location: center, occurredAt: Start.AddSeconds(4 + i));
                    encounter.Effect(1, action, i + 1, Start.AddSeconds(4 + i), target: 100, location: center);
                }
                Check(Puddles().Length == 4 && Puddles().First().Position == player.Position
                    && Puddles().First().Activation is { } activation && Math.Abs((activation - Start.AddSeconds(6.1)).TotalSeconds) < .000001,
                    "Event target snapshot, +2.1s clock or duplicate barrier.");
                Check(encounter.Hints(player.Position).Any(h => h.StartsWith("Run to the middle")), "Four puddles did not switch phase hint.");
                var bait = encounter.AmbientAoes().Single(a => a.Id == "palace-serpent/bait");
                var outside = bait.Shape.Resolve(bait.Position, 0, null);
                Check(!outside.Contains(center) && outside.Contains(center + Vector3.UnitX * 10), "Fourth effect did not invert middle constraint.");
                zone = new(2, 0x1E8D9B, player.Position, Targetable: false);
                Sync(8);
                Check(Puddles().Count(a => a.Id.Contains("/event/")) == 3 && Puddles().Count(a => a.Id.Contains("/live/")) == 1,
                    "Fire spawn consumed more than one prediction or failed handoff.");
                Sync(9);
                Check(Puddles().Count(a => a.Id.Contains("/event/")) == 2, "Persistent source must consume one matching prediction per update.");
                zone = zone with { EventState = 7 };
                Sync(30);
                Check(Puddles().Length == 2, "Wrong-type ice predictions consumed or timed out.");
                encounter.Effect(1, fear, 5, Start.AddSeconds(31));
                Check(encounter.Hints(player.Position).Any(h => h.Contains("x2 Fire")), "Fear effect did not reset five-event loop.");
                boss = boss with { Cast = new(fear, center, 0, Start.AddSeconds(32), Start.AddSeconds(38.3)) };
                Sync(32);
                var donut = encounter.OutputCasts.Single(c => c.Value.Rule.Action == fear).Value;
                Check(donut.Rule.Shape!.Resolve(center, 0, null).Contains(center + Vector3.UnitX * 6)
                    && !donut.Rule.Shape.Resolve(center, 0, null).Contains(center), "Fear Itself donut.");
                Sync(60);
                Check(Puddles().Length == 2 && encounter.Casts.Count == 1, "Predictions/live cast expired by time.");
                boss = boss with { Dead = true };
                Sync(61);
                Check(encounter.Active == null && !encounter.AmbientAoes().Any(), "Serpent death leaked cycle/puddles.");
            });
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
