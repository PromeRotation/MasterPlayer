using MasterPlayer.Mechanics.Crucible;
using MasterPlayer.Mechanics.Crucible.Components;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class MindflayerWaterRegression
{
    public static void Run(Action<string, Action> run)
    {
        run("B33 11:29 ARR thunder places its whole footprint outside both landed pools", () =>
        {
            using var f = new Fight();
            f.Icon(135);
            f.World.CurrentTime = f.Epoch.AddSeconds(5.96);
            f.Engine.Effect(f.Boss, new() { Action = new(49200), GlobalSequence = 1 });
            Check(f.Water.CurrentBaits.Count == 0, "water did not resolve");
            f.World.CurrentTime = f.Epoch.AddSeconds(6.747);
            f.Pool(new(117.5523f, -1.8832f));
            f.Pool(new(120.6489f, 4.2952f));
            f.World.CurrentTime = f.Epoch.AddSeconds(22.174);
            f.Icon(344);
            f.Render();
            Check(f.Water.CurrentBaits.Count == 0 && f.ThunderZones().Length == 2,
                "thunder only inspected unresolved water icons, not landed water");
            var solution = f.Solve();
            Check(solution.IsSuccess && solution.TargetPosition != null, $"no placement outside water: {solution.Status}");
            var target = WPos.FromVec3(solution.TargetPosition!.Value);
            Check(f.Water.Pools.All(p => !target.InCircle(p.Position, 14)), "thunder footprint overlaps water");
            Check(f.Engine.Module!.Arena.InBounds(target), "placement leaves the arena");
            Check(solution.ExecutablePath[^1].PlannedArrivalTime < f.World.FutureTime(5.9), "placement is too late");
            Check(f.Snapshot.GoalAreas.All(g => !g.Geometry.Contains(f.Player.Position.ToVec3())), "attack preference pulls player back into water");
            f.Player.Position = target;
            f.World.CurrentTime = f.World.FutureTime(5.2);
            f.Render();
            Check(f.Snapshot.DangerAreas.All(d => !d.Geometry.Contains(target.ToVec3())), "placed thunder still chases its own circle");
            f.World.CurrentTime = f.World.FutureTime(0.79);
            f.Engine.Effect(f.Boss, new() { Action = new(49206), GlobalSequence = 2 });
            f.Render();
            Check(f.ThunderZones().Length == 0 && f.Water.Pools.Count() == 2, "thunder effect did not release placement independently of pools");
        });

        run("B33 water-only pools are outlined without forcing escape from the player's own water", () =>
        {
            using var f = new Fight();
            var baseline = f.Snapshot.DangerAreas.Count;
            f.Pool(f.Player.Position);
            f.Icon(135);
            f.Render();
            Check(f.Snapshot.DangerAreas.Count == baseline, "water became a persistent forbidden area");
            var aoes = f.Water.ActiveAOEs(0, f.Player).ToArray();
            Check(aoes.Length == 2 && aoes.All(a => !a.Risky), "missing harmless self bait or pool outline");
            var previews = f.Draw.Previews.Values.Where(p => p.Geometry is CircleGeometry { Radius: 8 }).ToArray();
            Check(previews.Length == 2 && previews.All(p => p.OutlineOnly), "pool preview missing from shared drawing output");
        });

        foreach (var cleanup in new[] { "state7", "removed", "death" })
            run($"B33 live water pool {cleanup} clears its placement constraint and drawing", () =>
            {
                using var f = new Fight();
                var pool = f.Pool(f.Player.Position);
                f.Icon(344); f.Render();
                Check(f.ThunderZones().Length == 1, "missing ground pool constraint");
                if (cleanup == "state7") pool.EventState = 7;
                else f.Engine.Destroy(pool, cleanup == "removed");
                f.Render();
                Check(!f.Water.Pools.Any() && f.ThunderZones().Length == 0, "inactive pool still blocks placement");
                Check(f.Water.ActiveAOEs(0, f.Player).Length == 0, "inactive pool still drawn");
            });

        foreach (var (icon, action, visual, name) in new[] { (135u, 49200u, 49199u, 14633u), (344u, 49206u, 49205u, 14633u),
            (100u, 49243u, 49243u, 14648u), (704u, 49246u, 0u, 14648u) })
            run($"high-two icon {icon} duplicates keep deadlines and clean up between waves", () =>
            {
                using var f = new Fight(name);
                var component = f.Engine.Module!.Components.OfType<High2CrucibleSpread>().Single(c => c.WatchedAction == action);
                f.Icon(icon);
                var deadline = component.CurrentBaits.Single().Activation;
                f.World.CurrentTime = f.World.FutureTime(1);
                f.Icon(icon);
                Check(component.CurrentBaits.Single().Activation == deadline, "duplicate icon postponed the deadline");
                f.World.CurrentTime = deadline;
                f.Engine.Effect(f.Boss, new() { Action = new(action), GlobalSequence = 1 });
                Check(component.CurrentBaits.Count == 0, "actual effect left stale bait");
                f.Icon(icon);
                Check(component.CurrentBaits.Single().Activation > deadline, "new wave failed to rearm");
                if (visual != 0)
                {
                    f.Engine.Cast(f.Boss, new() { Action = new(visual), TargetID = f.Boss.InstanceID, FinishAt = f.World.FutureTime(4) });
                    f.Engine.FinishCast(f.Boss, visual, true);
                    Check(component.CurrentBaits.Count == 0, "visual cancellation left the icon bait");
                    f.Icon(icon);
                }
                f.World.CurrentTime = component.CurrentBaits.Single().Activation.AddSeconds(1.01);
                f.Render();
                Check(component.CurrentBaits.Count == 0, "missing effect left a permanent bait");
                f.Icon(icon); f.Engine.Destroy(f.Player, false);
                Check(component.CurrentBaits.Count == 0, "target death left stale bait");
            });

        foreach (var visual in new[] { 49199u, 49205u })
            run($"B33 normal visual {visual} completion holds placement until actual effect", () =>
            {
                using var f = new Fight();
                var component = visual == 49199 ? (High2CrucibleSpread)f.Water : f.Thunder;
                f.Icon(visual == 49199 ? 135u : 344u);
                f.Engine.Cast(f.Boss, new() { Action = new(visual), FinishAt = f.World.FutureTime(5.3), TargetID = f.Boss.InstanceID });
                f.World.CurrentTime = f.World.FutureTime(5.3);
                f.Engine.FinishCast(f.Boss, visual);
                f.Render();
                Check(component.CurrentBaits.Count == 1, "normal visual completion released the bait early");
            });

        run("B33 late activation scans only real pool objects and does not invent missing icons", () =>
        {
            using var f = new Fight();
            var pool = f.Pool(new(117.5523f, -1.8832f));
            f.Engine.Reset(); f.Engine.SelectGroup(); f.Render();
            Check(f.Water.Pools.Single() == pool && f.ThunderZones().Length == 0, "late activation did not recover real water safely");
            pool.Position = new(123, 3); f.Icon(344); f.Render();
            Check(f.ThunderZones().Single().Shape.Distance(pool.Position) < 0, "constraint kept an old icon/pool location");
            f.Engine.Destroy(f.Boss, true); f.Render();
            Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "group end retained water geometry");
        });
    }

    private sealed class Fight : IDisposable
    {
        public readonly DateTime Epoch = DateTime.UtcNow.AddMinutes(1);
        public readonly WorldState World = new();
        public readonly ScriptSafeZoneApi SafeZone = new() { DangerMarginForTest = 0.5f };
        public readonly ScriptDrawApi Draw = new();
        public readonly CrucibleEngine Engine;
        public readonly CrucibleOutput Output;
        public readonly Actor Boss, Player;
        private ulong _id = 100;
        public CrucibleMindflayerWater Water => Engine.Module!.FindComponent<CrucibleMindflayerWater>()!;
        public CrucibleMindflayerThunder Thunder => Engine.Module!.FindComponent<CrucibleMindflayerThunder>()!;
        public SafeZoneSnapshot Snapshot => SafeZone.Manager.BuildSnapshot(World.CurrentTime);
        public Fight(uint name = 14633)
        {
            World.CurrentTime = Epoch;
            Boss = new() { InstanceID = 1, OID = 19677, NameID = name, Position = new(120, 0), HPMP = (100, 100),
                Type = ActorType.Enemy, HitboxRadius = 3, IsTargetable = true, InCombat = true };
            Player = new() { InstanceID = 2, Position = new(120.2564f, -0.2899f), HPMP = (100, 100), Type = ActorType.Player, IsTargetable = true };
            World.Actors.Add(Boss); World.Actors.Add(Player); World.Player = Player;
            Engine = new(World, 32, 45); Engine.SelectGroup();
            Output = new(SafeZone, Draw, SafeZone.Manager, _ => { }, () => 0.5f);
            Render();
        }
        public Actor Pool(WPos position)
        {
            var a = new Actor { InstanceID = _id++, OID = 0x1E9998, Position = position };
            World.Actors.Add(a); Engine.Dispatch(c => c.OnActorCreated(a)); return a;
        }
        public void Icon(uint icon) => Engine.Dispatch(c => c.OnEventIcon(Player, icon, Player.InstanceID));
        public AIHints.ForbiddenZone[] ThunderZones()
        {
            var hints = new AIHints(); Thunder.AddAIHints(0, Player, PartyRolesConfig.Assignment.Unassigned, hints);
            return hints.ForbiddenZones.ToArray();
        }
        public void Render() { Engine.Update(); Output.Apply(Engine, Engine.Frame(), 0, true, true, true); }
        public SafeZoneSolution Solve() => new SafeZoneSolver().Solve(Snapshot,
            new(Player.Position.ToVec3(), 0, 6, false, false, 0, true, Snapshot.Version), new(6, 6, 0, false, 6, 6), new(), World.CurrentTime);
        public void Dispose() { Output.Clear(); SafeZone.Dispose(); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
