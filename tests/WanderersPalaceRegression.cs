using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using Fight = HalataliRegression.Fight;

internal static class WanderersPalaceRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Wanderers Palace three native arenas retain original centers and dimensions", () =>
        {
            foreach (var (boss, center, edge) in new[]
            {
                (0x41Cu, new Vector3(125, 0, 108), 20f),
                (0x41Du, new Vector3(43, 0, -232), 20f),
                (0x374u, new Vector3(73, 0, -435), 30f),
            })
            {
                using var f = new Fight(boss, 159);
                var geometry = f.Engine.Arena!;
                Check(geometry.Contains(center + Vector3.UnitX * (edge - .01f))
                    && !geometry.Contains(center + Vector3.UnitX * (edge + .01f))
                    && HostDrawingGeometry.Convert(geometry).Contains(center), "Native arena changed.");
            }
        });
        run("Wanderers Palace Inhale simultaneously predicts the follow-up grief one point one seconds later", () =>
        {
            using var f = new Fight(0x41C, 159);
            f.Cast(1, 950, 2.5, MathF.PI / 2);
            Check(f.Draw.Previews.Count == 2 && f.Snapshot.DangerAreas.Count == 2, "Inhale omitted follow-up.");
            var inhale = f.Draw.Previews["cast/1"].Geometry;
            Check(inhale.Contains(f.Center + Vector3.UnitX * 22.84f)
                && !inhale.Contains(f.Center + Vector3.UnitX * 22.86f)
                && !inhale.Contains(f.Center - Vector3.UnitX), "Inhale cone dimensions or direction changed.");
            var grief = f.Snapshot.DangerAreas.Single(d => d.Geometry is CircleGeometry);
            Check(Math.Abs((grief.DangerWindows.Single().DangerFrom - f.Start).TotalSeconds - 3.9) < .001, "Follow-up delay should follow the NPC-adjusted cast end by 1.1 seconds.");
            f.Complete(1, 2.5);
            Check(f.Draw.Previews.Count == 1 && f.Draw.Previews.Single().Value.Geometry is CircleGeometry { Radius: 8.85f },
                "Natural inhale end dropped grief.");
            f.Now = f.Start.AddSeconds(3);
            f.Actors[1] = f.Actors[1] with { Position = Vector3.UnitZ * 4 }; f.Scan();
            Check(f.Draw.Previews.Single().Value.Geometry.Contains(Vector3.UnitZ * 12)
                && Math.Abs((f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom - f.Start).TotalSeconds - 3.9) < .001,
                "Follow-up did not follow boss or clock was reset.");
            f.Cast(1, 942, .5);
            Check(f.Draw.Previews.Count == 1 && Math.Abs((f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom - f.Start).TotalSeconds - 3.8) < .001,
                "Actual grief cast did not correct prediction.");
            f.Complete(1, 3.5); Check(f.Draw.Previews.Count == 0, "Final grief cast leaked.");
        });
        run("Wanderers Palace cancelled Inhale and removed boss clear the entire predicted chain", () =>
        {
            using var f = new Fight(0x41C, 159);
            f.Cast(1, 950, 2.5);
            f.Engine.Finish(1, 950, f.Now.AddSeconds(1)); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancelled inhale left grief.");
            f.Now = f.Start.AddSeconds(10); f.Cast(1, 950, 2.5);
            f.Engine.RemoveActor(1); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Removed boss left chain.");
        });
        run("Wanderers Palace phlegm preserves six yalm display and separate planning margin", () =>
        {
            using var f = new Fight(0x41C, 159); f.Cast(1, 941, 2.5);
            Check(f.Draw.Previews.Single().Value.Geometry is CircleGeometry { Radius: 6 }
                && f.Snapshot.DangerAreas.Single().Geometry is CircleGeometry { Radius: 6.5f }, "Phlegm radius changed.");
        });
        run("Wanderers Palace moldy sneeze remains a ninety degree cleave and tank avoids facing party members", () =>
        {
            using var f = new Fight(0x41C, 159);
            f.Actors[100] = new(100, 0, f.Center + Vector3.UnitZ * 4, Hostile: false, PartySlot: 0);
            f.Actors[101] = new(101, 0, f.Center - Vector3.UnitZ * 4, Hostile: false, PartySlot: 1);
            f.Actors[1] = f.Actors[1] with { TargetId = 100 }; f.Scan();
            Check(f.Draw.Previews.Single().Value.Geometry is FanGeometry { Radius: 8.85f, AngleRadians: var a }
                && Math.Abs(a - MathF.PI / 2) < .001, "Sneeze inherited another boss's 120 degree cleave.");
            Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Actors[101].Position)
                && !f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Actors[100].Position), "Tank did not avoid cleaving party.");
            f.PlayerId = 101; f.Scan();
            Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Actors[100].Position), "Non-tank failed to avoid cleave.");
        });
        run("Wanderers Palace Bavarois kite is icon-target-specific follows boss and ends on primary flail only", () =>
        {
            using var f = new Fight(0x41D, 159);
            f.Actors[100] = new(100, 0, f.Center, Hostile: false);
            f.Actors[101] = new(101, 0, f.Center, Hostile: false); f.Scan();
            f.Engine.Icon(100, 1); f.Render();
            Check(f.Snapshot.DangerAreas.Count == 1 && f.Draw.Previews.Single().Value.Geometry is CircleGeometry { Radius: 8 }, "Icon target did not kite.");
            f.PlayerId = 101; f.Scan(); Check(f.Draw.Previews.Count == 0, "Unmarked player inherited kite.");
            f.PlayerId = 100; f.Actors[1] = f.Actors[1] with { Position = Vector3.UnitX * 12 }; f.Scan();
            Check(f.Draw.Previews.Single().Value.Geometry.Contains(Vector3.UnitX * 12), "Kite range stopped following boss.");
            f.Engine.Effect(11, 943, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 1, "Add flail incorrectly ended boss kite.");
            f.Engine.Effect(1, 943, 2, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0, "Primary flail retained kite.");
        });
        run("Wanderers Palace Tonberry King retains boss priority rather than chasing spawning adds", () =>
        {
            using var f = new Fight(0x374, 159);
            f.Actors[10] = new(10, 0x3A3, f.Center); f.Actors[11] = new(11, 0x8D5, f.Center); f.Scan();
            Check(f.Engine.PriorityTarget(10, f.Center) == 1, "King priority was lost.");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
