using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class CoilGauntletRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Coil gauntlet unclickable terminal pulls without combat and completes on either terminal", () =>
        {
            using var f = Scene();
            Check(f.Engine.Active != null && Stage(f) == 0 && f.Engine.Arena!.Contains(new(24.9f, 0, 0))
                && !f.Engine.Arena.Contains(new(25.1f, 0, 0)), "Terminal pull/boundary.");
            f.Actors[1] = f.Actors[1] with { Targetable = true }; f.Scan();
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0, "Start terminal complete.");
            f.Actors[1] = f.Actors[1] with { Targetable = false }; f.Scan();
            Check(f.Engine.Active != null, "New terminal pull did not restart.");
            f.Actors[2] = new(2, 0x1E86FA, Vector3.Zero); f.Scan();
            Check(f.Engine.Active == null, "End terminal complete.");
            f.Scan(); Check(f.Engine.Active == null, "Complete gate immediately resurrected fight.");
        });
        run("Coil gauntlet normal terminal destruction removes list entry before phase check", () =>
        {
            using var f = Scene();
            Add(f, 10, 0x7E1); Add(f, 11, 0x7E4); f.Scan();
            f.Engine.RemoveActor(11); f.Render(); Check(f.Engine.Active != null, "Enemy despawn ended fight.");
            f.Engine.RemoveActor(1); f.Actors.Remove(1); f.Scan(); Check(f.Engine.Active != null, "Empty terminal list incorrectly completed phase.");
            f.Actors[2] = new(2, 0x1E86FA, Vector3.Zero); f.Scan();
            Check(f.Engine.Active == null, "Remaining targetable end terminal did not complete phase.");
        });
        run("Coil gauntlet early wave conditions activate cleave and rook only at source states", () =>
        {
            using var f = Scene();
            Add(f, 10, 0x7E1); f.Scan(); Check(Stage(f) == 1, "First wave.");
            Add(f, 11, 0x7E4); f.Actors[11] = f.Actors[11] with { TargetId = 101 }; f.Scan();
            Check(Stage(f) == 2 && f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("coil/cleave")), "Dread state did not enable cleave.");
            Add(f, 12, 0x7E3); f.Scan(); Check(Stage(f) == 3, "Rook state.");
            Add(f, 13, 0x7E2); f.Scan(); Check(Stage(f) == 4, "Mixed alive gate.");
            Add(f, 14, 0x7E0); f.Scan(); Check(Stage(f) == 5, "Final mixed gate.");
            f.Now = f.Now.AddSeconds(500); f.Engine.Tick(f.Now);
            Check(Stage(f) == 5 && f.Engine.Active != null, "Terminal state incorrectly timed out.");
        });
        run("Coil gauntlet timeout uses 62.1 then 61 and resets at each observed transition", () =>
        {
            using var f = Scene();
            f.Now = f.Start.AddSeconds(62.099); f.Engine.Tick(f.Now); Check(Stage(f) == 0, "First overdue too early.");
            f.Now = f.Start.AddSeconds(62.1); f.Engine.Tick(f.Now); Check(Stage(f) == 1, "First overdue.");
            f.Now = f.Start.AddSeconds(123.099); f.Engine.Tick(f.Now); Check(Stage(f) == 1, "Second overdue too early.");
            f.Now = f.Start.AddSeconds(62.1).AddSeconds(61); f.Engine.Tick(f.Now); Check(Stage(f) == 2, "Second overdue.");
            f.Now = f.Start.AddSeconds(1000); f.Engine.Tick(f.Now);
            Check(Stage(f) == 3, "Long tick incorrectly replayed all nominal waves.");
        });
        run("Coil gauntlet first conditions accept targetable dead actors later conditions require alive", () =>
        {
            using var f = Scene();
            Add(f, 10, 0x7E1); f.Actors[10] = f.Actors[10] with { Dead = true };
            Add(f, 11, 0x7E4); f.Actors[11] = f.Actors[11] with { Dead = true };
            Add(f, 12, 0x7E3); Add(f, 13, 0x7E2); f.Scan();
            Check(Stage(f) == 3, "Source early condition or later alive condition differs.");
        });
        run("Coil gauntlet rook personal rectangle follows source position and cast facing only targeted player", () =>
        {
            using var f = Scene(); ReachRooks(f);
            var cast = new DungeonCastObservation(1237, Vector3.Zero, MathF.PI / 2, f.Now, f.Now.AddSeconds(3.3), 100);
            f.Actors[12] = f.Actors[12] with { Cast = cast }; f.Engine.StartCast(12, cast, f.Now); f.Render();
            var aoe = f.Engine.AmbientAoes().Single(a => a.Id.StartsWith("coil4/rook"));
            Check(aoe.Shape is RayRectangleTemplate { Length: 50, MinLocalX: -50, MaxLocalX: 50 }
                && aoe.Rotation == MathF.PI / 2 && aoe.Activation == cast.FinishAt, "Source rectangle.");
            f.Actors[12] = f.Actors[12] with { Position = new(2, 0, 4) }; f.Scan();
            Check(f.Engine.AmbientAoes().Single(a => a.Id.StartsWith("coil4/rook")).Position == new Vector3(2, 0, 4), "Rook position frozen.");
            f.PlayerId = 101; f.Scan(); Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("coil4/rook")), "Non-target received personal rectangle.");
            f.PlayerId = 100; f.Engine.Finish(12, 1237); f.Render();
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("coil4/rook")), "Stale raw cast revived cancelled personal rectangle.");
        });
        run("Coil gauntlet dread priority native dual views no invented knight reflect restriction", () =>
        {
            using var f = Scene(); ReachRooks(f);
            Check(f.Engine.PriorityTarget(10, Vector3.Zero) == 11 && f.Draw.Previews.Count > 0 && f.Snapshot.DangerAreas.Count > 0,
                "Dread priority/dual view output.");
            f.Actors[100] = f.Actors[100] with { Position = new(0, 0, 8) }; f.Scan();
            Check(f.Engine.Hints(null).Contains("离开顺劈范围"), "Dread cleave warning missing.");
            Check(!f.Engine.ForbiddenTargets.Any() && f.Engine.GazeFacing(Vector3.Zero, 0) == null, "Invented control absent from source.");
        });
        run("Coil gauntlet wipe clears output and resets wave without personal death resetting event state", () =>
        {
            using var f = Scene(); ReachRooks(f);
            f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan(); Check(Stage(f) == 3, "Death reset wave.");
            f.Engine.DirectorUpdate(0x40000005); f.Scan();
            Check(f.Engine.Active == null && f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Wipe output leak.");
            foreach (var id in new ulong[] { 10, 11, 12 }) f.Actors.Remove(id);
            f.Engine.DirectorUpdate(0x40000011); f.Scan(); Check(Stage(f) == 0, "Recommence wave reset.");
        });
    }

    private static Fight Scene()
    {
        var f = new Fight(0x1E86F9, 244);
        f.Actors[1] = f.Actors[1] with { Targetable = false, InCombat = false };
        f.Actors[100] = new(100, 0, new(0, 0, 15), PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, new(0, 0, 5), PartySlot: 1, Hostile: false);
        f.Scan(); return f;
    }
    private static void Add(Fight f, ulong id, uint oid) => f.Actors[id] = new(id, oid, Vector3.Zero, InCombat: true);
    private static void ReachRooks(Fight f)
    {
        Add(f, 10, 0x7E1); Add(f, 11, 0x7E4); Add(f, 12, 0x7E3);
        f.Actors[11] = f.Actors[11] with { TargetId = 101 }; f.Scan();
    }
    private static int Stage(Fight f) => (int)typeof(DungeonEncounter).GetField("gauntletStage", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(f.Engine)!;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
