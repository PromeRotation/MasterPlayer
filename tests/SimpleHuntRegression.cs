using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts all source cast rules exact geometry activation and damage classification", Rules);
        run("SimpleHunts same OID instances independent observed endings locked poses and stale casts", Instances);
        run("SimpleHunts boss lifecycle personal death exclusions and explicit damage targets", Lifecycle);
        run("SimpleHunts event before snapshot and old generations never end or resurrect newer casts", EventOrdering);
    }

    private static void Rules()
    {
        var expected = new Dictionary<uint, (string Kind, float Range, float HalfWidthOrAngle, double Seconds)>
        {
            [27725] = ("circle", 8, 0, 5), [27724] = ("rect", 60, 4, 4),
            [39623] = ("circle", 5, 0, 5), [39624] = ("cone", 40, MathF.PI / 8, 5),
            [8188] = ("cone", 8.23f, MathF.PI / 3, 3), [8187] = ("circle", 6, 0, 3), [8189] = ("damage", 0, 0, 3),
            [7997] = ("rect", 55.04f, 4, 3.5), [8000] = ("damage", 0, 0, 5), [8002] = ("damage", 0, 0, 5),
            [16830] = ("cone", 12.025f, MathF.PI / 3, 3), [16831] = ("cone", 34.025f, MathF.PI / 3, 5),
            [16832] = ("rect", 49.025f, 3, 3), [16833] = ("damage", 0, 0, 3),
            [27269] = ("rect", 40, 7, 3), [27270] = ("cone", 25, MathF.PI / 4, 4),
            [27271] = ("circle", 17, 0, 3), [27272] = ("damage", 0, 0, 5), [27273] = ("damage", 0, 0, 5),
        };
        var shapes = 0;
        var predictions = 0;
        foreach (var definition in SimpleHuntCatalog.Definitions)
        {
            foreach (var rule in definition.Casts.Where(r => expected.ContainsKey(r.Action)))
            {
                var encounter = new SimpleHuntEncounter();
                var location = new Vector3(10, 0, 20);
                var spec = expected[rule.Action];
                var cast = new ReplayCast(rule.Action, 101, location, MathF.PI / 2, Start, Start.AddSeconds(spec.Seconds + .3), false, false);
                encounter.Synchronize([Actor(1, definition.Oid, cast), new() { Id = 100 }, new() { Id = 101 }], _ => true);
                var pending = encounter.Casts.Single();
                Check(pending.Activation == Start.AddSeconds(spec.Seconds + .3) && pending.Location == location && pending.Rotation == MathF.PI / 2,
                    $"{definition.Name}/{rule.Name} changed the source cast pose or added finish delay twice.");
                if (rule.Shape != null)
                {
                    ++shapes;
                    var geometry = rule.Shape.Resolve(location, pending.Rotation, null);
                    Check(geometry.Contains(location + Vector3.UnitX * 2, 0), "Forward cast interior not dangerous.");
                    Check(!geometry.Contains(location + Vector3.UnitX * 100, 0), "Cast exceeds source dimensions.");
                    if (rule.Shape is not CircleTemplate)
                        Check(!geometry.Contains(location - Vector3.UnitX * 2, 0), "Directional cast includes its rear.");
                    Check(geometry.Contains(location + Vector3.UnitX * (spec.Range - .01f), 0)
                        && !geometry.Contains(location + Vector3.UnitX * (spec.Range + .01f), 0), "Source range boundary changed.");
                    if (spec.Kind == "rect")
                        Check(geometry.Contains(location + Vector3.UnitX * 2 + Vector3.UnitZ * (spec.HalfWidthOrAngle - .01f), 0)
                            && !geometry.Contains(location + Vector3.UnitX * 2 + Vector3.UnitZ * (spec.HalfWidthOrAngle + .01f), 0),
                            "Source rectangle full/half width changed.");
                    if (spec.Kind == "cone")
                    {
                        Vector3 Point(float angle) => location + new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle)) * (spec.Range / 2);
                        Check(geometry.Contains(Point(spec.HalfWidthOrAngle - .001f), 0)
                            && !geometry.Contains(Point(spec.HalfWidthOrAngle + .001f), 0), "Source cone half-angle changed.");
                    }
                }
                else ++predictions;
                var damage = encounter.PredictedDamage([new(0, "Tank", 100), new(1, "Healer", 101)]).ToArray();
                Check(damage.Length == (rule.DamageType == "Raidwide" ? 2 : rule.DamageType == "Tankbuster" ? 1 : 0),
                    "Damage classification depends on hint text or invents extra hits.");
                Check(damage.All(d => d.Activation == pending.Activation), "Damage clock differs from the source cast.");
            }
        }
        Check(shapes == 13 && predictions == 6, "Unexpected source rule coverage.");
        var tail = SimpleHuntCatalog.Find(0x2820)!.Casts.Single(r => r.Action == 16831).Shape!.Resolve(Vector3.Zero, 0, null);
        Check(tail.Contains(new Vector3(20, 0, 20), 0), "TailDrive followed its stale comment instead of the source 120-degree constructor.");
        Check(!tail.Contains(new Vector3(30, 0, 1), 0), "TailDrive exceeds its source angle.");
    }

    private static void Instances()
    {
        var encounter = new SimpleHuntEncounter();
        var cast = new ReplayCast(27724, 100, new(10, 0, 20), MathF.PI / 2, Start, Start.AddSeconds(4.3), false, false);
        var first = Actor(1, 0x3672, cast);
        var second = Actor(2, 0x3672, cast with { Location = new(30, 0, 40) });
        encounter.Synchronize([first, second], _ => true);
        Check(encounter.Casts.Count == 2, "Same-OID instances were merged.");
        encounter.Start(1, cast with { Location = new(11, 0, 21) }, true);
        encounter.Synchronize([first with { Position = new(80, 0, 80), Rotation = 3 }, second], _ => true);
        Check(encounter.Casts.Single(c => c.Source == 1).Location == new Vector3(11, 0, 21),
            "Object rotation/location overwrote the authoritative cast packet.");
        encounter.Finish(1, 27724, Start.AddSeconds(1));
        encounter.Synchronize([first, second], _ => true);
        Check(encounter.Casts.Count == 1 && encounter.Casts.Single().Source == 2, "Cancel resurrected from stale object cast or cleared another instance.");
        // A delayed cast remains active past its estimated activation until an actual observation ends it.
        encounter.Synchronize([first with { Cast = null }, second], _ => true);
        Check(encounter.Casts.Single().Activation == Start.AddSeconds(4.3), "An observed active cast expired by a wall-clock heuristic.");
        encounter.Synchronize([first with { Cast = null }, second with { Cast = null }], _ => true);
        Check(encounter.Casts.Count == 0, "Observed cast finish retained output.");
        encounter.Synchronize([first with { Cast = cast with { StartedAt = Start.AddSeconds(10), FinishAt = Start.AddSeconds(14.3) } }], _ => true);
        Check(encounter.Casts.Count == 1, "A later identical action was suppressed by an old cancellation.");
        encounter.Finish(1, 27724, Start.AddSeconds(1));
        Check(encounter.Casts.Count == 1, "A late old finish removed the next cast.");
        encounter.Synchronize([first with { Cast = new(999, 0, Vector3.Zero, 0, Start.AddSeconds(20), Start.AddSeconds(21), false, false) }], _ => true);
        Check(encounter.Casts.Count == 0, "An unhandled replacement cast retained the preceding hazard.");
    }

    private static void Lifecycle()
    {
        var encounter = new SimpleHuntEncounter();
        var cast = new ReplayCast(27273, 101, Vector3.Zero, 0, Start, Start.AddSeconds(5.3), false, false);
        var boss = Actor(1, 0x35C0, cast) with { TargetId = 100 };
        var player = new ReplayActorState { Id = 100, Dead = true };
        encounter.Synchronize([boss, player], _ => true);
        Check(encounter.Active && encounter.Handles(1, 27273, _ => true), "Personal death or combat ending reset a fighting hunt.");
        Check(!encounter.Handles(100, 27273, _ => true) && !encounter.Handles(1, 27724, _ => true),
            "Action-only suppression claimed an unrelated caster or wrong OID.");
        var party = new ReplayPartyMember[] { new(0, "Tank", 100), new(1, "Healer", 101) };
        Check(encounter.PredictedDamage(party).Single().TargetId == 101, "Single-target prediction substituted the boss's current target.");
        encounter.Reset();
        encounter.Synchronize([boss with { Cast = cast with { TargetId = 1 } }], _ => true);
        Check(encounter.PredictedDamage(party).Single().TargetId == 100, "Self-targeted single-target cast did not use the boss target.");
        encounter.Synchronize([boss with { TargetId = 101, Cast = cast with { TargetId = 1 } }], _ => true);
        Check(encounter.PredictedDamage(party).Single().TargetId == 101, "Self-targeted damage failed to follow the boss's current target.");
        encounter.Synchronize([boss with { InCombat = false }], _ => true);
        Check(!encounter.Active && encounter.Casts.Count == 0, "Boss disengagement retained a session.");
        encounter.Synchronize([boss], _ => false);
        Check(!encounter.Active && encounter.Casts.Count == 0 && !encounter.Handles(1, 27273, _ => false), "Disabled mechanism claimed or produced output.");
        encounter.Reset();
        encounter.Synchronize([boss with { Targetable = false }], _ => true);
        Check(!encounter.Active, "A never-targetable hunt activated before its source pull condition.");
        encounter.Synchronize([boss], _ => true);
        encounter.Synchronize([boss with { Targetable = false }], _ => true);
        Check(encounter.Active, "An engaged hunt reset merely because it became untargetable.");
        encounter.Reset();
        encounter.Synchronize([boss with { Cast = cast with { ActionId = 27272 } }, player, new() { Id = 101 }], _ => true);
        Check(encounter.PredictedDamage([.. party, new(2, "Tank", 102)]).Single().TargetId == 101,
            "Raidwide included dead or absent party actors.");
    }

    private static void EventOrdering()
    {
        var encounter = new SimpleHuntEncounter();
        var cast = new ReplayCast(27724, 100, Vector3.Zero, 0, Start, Start.AddSeconds(4.3), false, false);
        var actor = Actor(1, 0x3672, cast);
        encounter.Synchronize([actor with { Cast = null }], _ => true);
        encounter.Start(1, cast, true);
        encounter.Synchronize([actor with { Cast = null }], _ => true);
        Check(encounter.Casts.Count == 1, "A not-yet-observed cast was treated as an observed finish.");
        encounter.Synchronize([actor], _ => true);
        Check(encounter.Casts.Single().Observed, "A later matching snapshot never confirmed the packet cast.");
        encounter.Synchronize([actor with { Cast = null }], _ => true);
        Check(encounter.Casts.Count == 0, "An observed cast was not ended by its later empty snapshot.");
        var next = cast with { StartedAt = Start.AddSeconds(10), FinishAt = Start.AddSeconds(14.3) };
        encounter.Synchronize([actor with { Cast = next }], _ => true);
        encounter.Synchronize([actor], _ => true);
        encounter.Start(1, cast, true);
        Check(encounter.Casts.Single().StartedAt == next.StartedAt, "An older packet/snapshot replaced a later generation.");
        encounter.Finish(1);
        encounter.Start(1, cast, true);
        Check(encounter.Casts.Count == 0, "An old cancelled generation resurrected after a later cast ended.");
    }

    private static ReplayActorState Actor(ulong id, uint oid, ReplayCast cast)
        => new() { Id = id, Oid = oid, InCombat = true, Targetable = true, Cast = cast };

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new Exception(reason);
    }
}
