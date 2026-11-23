using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class SastashaHardRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Sastasha Hard arenas match actual upstream circle and rectangle dimensions", () =>
        {
            var definitions = SastashaHardCatalog.Create();
            var karlabos = definitions[0].Arena(Vector3.Zero);
            var madison = definitions[1].Arena(Vector3.Zero);
            Check(karlabos is CircleGeometry { Radius: 21, Position: var p } && p == new Vector3(166, 0, -85), "Karlabos arena changed.");
            var world = HostDrawingGeometry.Convert(madison);
            foreach (var contains in new Func<Vector3, bool>[] { p => madison.Contains(p), p => world.Contains(p) })
                Check(contains(new(-85.01f, 0, 155.99f)) && !contains(new(-84.99f, 0, 136))
                    && !contains(new(-103, 0, 156.01f)), "Madison arena half extents were mistaken for full dimensions.");
            Check(ArrDungeonCatalog.Owns(0xCBA, 387) && ArrDungeonCatalog.Owns(0xCBB, 387)
                && !ArrDungeonCatalog.Owns(0xCBA, 1036), "Hard and normal identities were mixed.");
        });
        run("Sastasha Hard Bloodstain uses actual seven-point-five radius and independent navigation margin", () =>
        {
            using var f = new Fight(0xCBB, 387); f.Actors[10] = new(10, 0xCBC, f.Center); f.Scan(); f.Cast(10, 1099, 2.5);
            Check(f.Draw.Previews.Single().Value.Geometry is CircleGeometry { Radius: 7.5f }
                && f.Snapshot.DangerAreas.Single().Geometry is CircleGeometry { Radius: 8 }
                && f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(2.8), "Bloodstain used comment range or lost timing.");
            f.Complete(10, 2.5); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Add cast finish leaked.");
        });
        run("Sastasha Hard Rive uses actual eighteen-by-three locked ray rather than inaccurate thirty-by-two comment", () =>
        {
            using var f = new Fight(0xCBB, 387); f.Actors[10] = new(10, 0xCBD, f.Center); f.Scan(); f.Cast(10, 1299, 2.5, MathF.PI / 2);
            var shape = f.Draw.Previews.Single().Value.Geometry;
            Check(shape.Contains(new(17.99f, 0, 1.49f)) && !shape.Contains(new(18.01f, 0, 0)) && !shape.Contains(new(1, 0, 1.51f))
                && !shape.Contains(new(-.01f, 0, 0)), "Rive dimensions/direction changed.");
        });
        run("Sastasha Hard Hornswaggle keeps upstream estimated ninety-degree five-yalm fan", () =>
        {
            using var f = new Fight(0xCBB, 387); f.Cast(1, 3062, 2.5);
            Check(f.Draw.Previews.Single().Value.Geometry is FanGeometry { Radius: 5, AngleRadians: var a } && Math.Abs(a - MathF.PI / 2) < .001,
                "Unverified Hornswaggle estimates were silently changed.");
        });
        run("Sastasha Hard Tail Screw requires stun not interrupt and cancels cleanly", () =>
        {
            using var f = new Fight(0xCBA, 387); f.Cast(1, 3060, 3);
            Check(f.Engine.Hints(f.Center).Any(h => h.Contains("眩晕") && h.Contains("不能用打断"))
                && f.Snapshot.DangerAreas.Count == 0, "Tail Screw misclassified as interrupt or spatial AOE.");
            f.Engine.Finish(1, 3060); f.Render();
            Check(!f.Engine.Hints(f.Center).Any(h => h.Contains("螺旋尾")), "Cancelled Tail Screw retained warning.");
        });
        run("Sastasha Hard prey follows marked tank perspective while slime selects only valid cleanse targets", () =>
        {
            using var f = new Fight(0xCBA, 387);
            f.Actors[100] = new(100, 0, f.Center, PartySlot: 0, PartyRole: "MT", Statuses: [420], Hostile: false);
            f.Actors[101] = new(101, 0, Vector3.UnitX * 5, PartySlot: 4, PartyRole: "D1", Statuses: [569], Hostile: false);
            f.Actors[102] = new(102, 0, Vector3.UnitX * 35, PartySlot: 5, PartyRole: "D2", Statuses: [569], Hostile: false);
            f.Scan();
            Check(f.Draw.Previews.Count == 1 && f.Engine.Hints(f.Center).Any(h => h.Contains("穿过"))
                && f.Engine.SlimeCleanseTarget(24)?.Id == 101 && f.Engine.SlimeCleanseTarget(23)?.Id == 101
                && f.Engine.SlimeCleanseTarget(19) == null, "Prey or cleanse job/range filtering differs.");
            f.PlayerId = 101; f.Scan();
            Check(!f.Engine.Hints(f.Center).Any(h => h.Contains("穿过")) && f.Engine.SlimeCleanseTarget(40)?.Id == 101,
                "Perspective change retained tank-only instructions or missed self cleanse.");
            f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 101, StatusId = 569, Timestamp = f.Now }, f.Now); f.Render();
            Check(f.Engine.SlimeCleanseTarget(24)?.Id == 102, "Removed Slime retained old target instead of next in-range member.");
            f.Actors[101] = f.Actors[101] with { Statuses = [] };
            f.Actors[102] = f.Actors[102] with { Dead = true }; f.Scan();
            Check(f.Engine.SlimeCleanseTarget(24) == null, "Dead party member retained cleanse target.");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
