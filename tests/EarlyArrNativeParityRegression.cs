using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;

internal static class EarlyArrNativeParityRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        run("Early ARR parity Denn guards outrank boss and equivalent targets stay selected", () =>
        {
            var engine = new DungeonEncounter(1036);
            var center = new Vector3(-121, 0, 138);
            DungeonActor boss = new(1, 0x1A1, center, InCombat: true);
            DungeonActor guard = new(2, 0x1A2, center + Vector3.UnitX, InCombat: true);
            engine.Synchronize([boss, guard, guard with { Id = 3 }], center, Start);
            Check(engine.PriorityTarget(1, center) == 2, "Guard did not outrank Denn.");
            Check(engine.PriorityTarget(3, center) == 3, "An equally ranked current guard was replaced.");
            engine.Synchronize([boss, guard with { Dead = true }], center, Start.AddSeconds(1));
            Check(engine.PriorityTarget(2, center) == 1, "Dead guard prevented boss fallback.");
        });
        run("Early ARR parity Galvanth imp priority falls back to zero priority boss", () =>
        {
            var engine = new DungeonEncounter(1037);
            var center = new Vector3(-52.765f, 0, -12.789f);
            DungeonActor boss = new(1, 0x4C, center, InCombat: true);
            DungeonActor imp = new(2, 0x7D, center + Vector3.UnitX, InCombat: true);
            engine.Synchronize([boss, imp], center, Start);
            Check(engine.PriorityTarget(1, center) == 2, "Imp did not receive priority 1.");
            engine.Synchronize([boss, imp with { Dead = true }], center, Start.AddSeconds(1));
            Check(engine.PriorityTarget(2, center) == 1, "Priority zero boss was excluded after the imp died.");
            Check(engine.ForbiddenTargets.Length == 0, "Imp existence invented an unverified invulnerability status.");
        });
        run("Early ARR parity module enemy outlines reach previews with exact membership and no navigation", () =>
        {
            foreach (var (territory, oid, expected) in new (uint, uint, uint[])[]
            {
                (1036, 0x19B, [0x19B]), (1036, 0x1A1, [0x1A1, 0x1A2]),
                (1037, 0x237, [0x237]), (1037, 0x4C, [0x4C, 0x7D, 0x7E, 0x7F]),
                (1038, 0x387C, [0x387C]), (1038, 0x3870, [0x3870]), (1038, 0x38C9, [0x38C9]),
            })
            {
                var engine = new DungeonEncounter(territory);
                var actors = expected.Select((enemy, i) => new DungeonActor((ulong)i + 1, enemy, Vector3.Zero, InCombat: true, HitboxRadius: 2)).ToArray();
                engine.Synchronize([.. actors, new(999, 0xFFFF, Vector3.Zero, InCombat: true)], Vector3.Zero, Start);
                Check(engine.Active?.BossOid == oid, $"Failed to activate {oid:X}.");
                var outlines = engine.AmbientAoes().Where(a => a.Id.StartsWith("early-arr/enemy/")).ToArray();
                Check(outlines.Length == expected.Length && outlines.All(a => !a.Navigate && a.OutlineOnly && a.Display
                    && a.Color == new Vector4(1, 0, 0, 1)), "Enemy outlines lost source membership/color or entered navigation.");
                using var safe = new MasterPlayer.Scripting.ScriptSafeZoneApi();
                var draw = new MasterPlayer.Scripting.ScriptDrawApi();
                var output = new DungeonOutput(safe, draw, safe.Manager);
                output.Apply(engine, Start, true);
                Check(draw.Previews.Values.Count(p => p.Id.StartsWith("early-arr/enemy/")) == expected.Length
                    && safe.Manager.BuildSnapshot(Start).DangerAreas.Count == 0,
                    "Enemy model output did not reach the shared radar/world preview channel or fabricated navigation.");
                engine.ResetFight();
                output.Apply(engine, Start, true);
                Check(draw.Previews.Count == 0, "Module end retained actual enemy previews.");
                Check(!engine.AmbientAoes().Any(a => a.Id.StartsWith("early-arr/enemy/")), "Ended module retained enemy drawings.");
                engine.Synchronize(actors.Select(a => a with { HitboxRadius = 0 }), Vector3.Zero, Start);
                output.Apply(engine, Start, true);
                Check(!draw.Previews.Values.Any(p => p.Id.StartsWith("early-arr/enemy/")), "A missing recorded hitbox created an invalid circle.");
            }
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
