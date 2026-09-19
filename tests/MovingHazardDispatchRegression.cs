using System.Numerics;
using System.Reflection;
using MasterPlayer.Scripting;
using PromeRotation.GreenMoveSystem;
using PromeRotation.Spatial.SafeZone;
using PromeRotation.Spatial.SafeZoneReborn.Solver;

internal static class MovingHazardDispatchRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run(Action<string, Action> run)
    {
        run("moving hazards do not cancel every in-flight solve when snapshots change", () =>
        {
            var (movement, _) = Create();
            using var zone = new ScriptSafeZoneApi();
            var snapshot = zone.Manager.BuildSnapshot(DateTime.UtcNow);
            using var cts = new CancellationTokenSource();
            var resultType = movement.GetType().GetNestedType("AsyncSolveResult", BindingFlags.NonPublic)!;
            var promise = Activator.CreateInstance(typeof(TaskCompletionSource<>).MakeGenericType(resultType))!;
            Set(movement, "_pendingSolveTask", promise.GetType().GetProperty("Task")!.GetValue(promise)!);
            Set(movement, "_pendingSolveCts", cts);
            Set(movement, "_pendingSolveSnapshotVersion", snapshot.Version - 1);
            Call(movement, "StartPendingSolve", snapshot, Player(Vector3.Zero, snapshot), new MovementCapability(6,6,0,false,6,6), DateTime.UtcNow, null);
            Check(!cts.IsCancellationRequested, "each moving object update cancels the only route being calculated");
        });

        foreach (var changed in new[] { "distant", "blocking", "boundary", "expired" })
            run($"completed background path validates {changed} changes before first movement", () =>
            {
                var now = DateTime.UtcNow.AddMinutes(1);
                using var zone = new ScriptSafeZoneApi();
                zone.Manager.SetArenaBoundary(new RectangleTemplate(40,30).Resolve(Vector3.Zero,0,null));
                zone.Manager.AddDangerArea(new CircleTemplate(5), new StaticTransform(Vector3.Zero,0), [new DangerWindow(now.AddSeconds(3), now.AddSeconds(3.6))]);
                var snapshot = zone.Manager.BuildSnapshot(now);
                var position = Vector3.Zero;
                var solution = new SafeZoneSolver().Solve(snapshot, Player(position, snapshot), new(6,6,0,false,6,6), new(), now);
                Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2, "fixture has no escape");
                var (movement, move) = Create();
                Set(movement, "_setting", new SafeZoneSetting { EnableSafeZone = true });
                if (changed == "boundary") zone.Manager.SetArenaBoundary(new CircleTemplate(1).Resolve(position,0,null));
                else zone.Manager.AddDangerArea(new CircleTemplate(changed == "blocking" ? 30 : 1), new StaticTransform(changed == "blocking" ? solution.TargetPosition!.Value : new(18,0,12),0), [new DangerWindow(now,null)]);
                var current = zone.Manager.BuildSnapshot(now);
                Publish(movement, solution);
                Call(movement, "CompletePendingSolve", current);
                var accepted = (SafeZoneSolution?)movement.GetType().GetField("_latestSolution", Private)!.GetValue(movement);
                if (changed == "distant") Check(accepted != null, "usable route discarded just because the moving-hazard snapshot changed");
                Call(movement, "Dispatch", accepted, Player(position, current), current, changed == "expired" ? now.AddSeconds(2.9) : now.AddSeconds(0.1));
                Check(move.IsRunning == (changed == "distant"), "changed geometry/deadline was not validated before dispatch");
            });

        run("changed snapshots still permit a validated exit from a persistent contact hazard", () =>
        {
            var now = DateTime.UtcNow.AddMinutes(1);
            using var zone = new ScriptSafeZoneApi();
            zone.Manager.SetArenaBoundary(new CircleTemplate(20).Resolve(Vector3.Zero,0,null));
            zone.Manager.AddDangerArea(new CircleTemplate(3),new StaticTransform(Vector3.Zero,0),[new DangerWindow(now,null)]);
            var old = zone.Manager.BuildSnapshot(now);
            var solution = new SafeZoneSolver().Solve(old,Player(Vector3.Zero,old),new(6,6,0,false,6,6),new(),now);
            Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2, "fixture did not solve escape");
            zone.Manager.AddDangerArea(new CircleTemplate(1),new StaticTransform(new(15,0,0),0),[new DangerWindow(now,null)]);
            var current = zone.Manager.BuildSnapshot(now);
            var (movement, move) = Create();
            Call(movement,"Dispatch",solution,Player(Vector3.Zero,current),current,now.AddSeconds(0.1));
            Check(move.IsRunning, "validation forbids leaving the hazard already under the player");
            zone.Manager.AddDangerArea(new CircleTemplate(20),new StaticTransform(Vector3.Zero,0),[new DangerWindow(now,null)]);
            current = zone.Manager.BuildSnapshot(now);
            Call(movement,"Dispatch",solution,Player(Vector3.Zero,current),current,now.AddSeconds(0.2));
            Check(!move.IsRunning,"escape exemption allowed a destination covered by a new danger");
        });

        foreach (var latency in new[] { 0.15, 0.3 })
            run($"continuous 100ms moving-hazard snapshots still dispatch with {latency*1000:F0}ms background latency", () =>
            {
                var epoch = DateTime.UtcNow.AddMinutes(1);
                using var zone = new ScriptSafeZoneApi();
                zone.Manager.SetArenaBoundary(new RectangleTemplate(40,30).Resolve(Vector3.Zero,0,null));
                zone.Manager.AddDangerArea(new CircleTemplate(6),new StaticTransform(Vector3.Zero,0),[new DangerWindow(epoch.AddSeconds(4),epoch.AddSeconds(4.6))]);
                var handle = default(SafeZoneAreaHandle);
                var (movement,move) = Create();
                Set(movement,"_setting",new SafeZoneSetting{EnableSafeZone=true});
                var latest = movement.GetType().GetField("_latestSolution",Private)!;
                var position = Vector3.Zero;
                SafeZoneSolution? pending=null;
                double ready=0,firstMove=double.PositiveInfinity;
                for(var tick=0;tick<400;tick++)
                {
                    var now=epoch.AddSeconds(tick*0.01);
                    if(tick%10==0)
                    {
                        zone.Manager.RemoveArea(handle);
                        handle=zone.Manager.AddDangerArea(new CircleTemplate(2),new StaticTransform(new(15,0,-10+tick*0.01f),0),[new DangerWindow(now,null)]);
                    }
                    var snapshot=zone.Manager.BuildSnapshot(now);
                    if(pending!=null && tick*0.01+1e-6>=ready)
                    {
                        Publish(movement,pending); Call(movement,"CompletePendingSolve",snapshot); pending=null;
                    }
                    var solution=(SafeZoneSolution?)latest.GetValue(movement);
                    if(pending==null)
                    {
                        pending=new SafeZoneSolver().Solve(snapshot,Player(position,snapshot),new(6,6,0,false,6,6),new(),now,solution);
                        ready=tick*0.01+latency;
                    }
                    Call(movement,"Dispatch",solution,Player(position,snapshot),snapshot,now);
                    if(move.IsRunning) firstMove=Math.Min(firstMove,tick*0.01);
                    position=(Vector3)move.GetType().GetMethod("Advance")!.Invoke(move,[position,0.01f])!;
                }
                Check(firstMove<=latency+0.02,"continuous snapshot changes starved the first escape");
                Check(position.Length()>6,"player remained inside the four-second blast");
            });
    }

    private static PlayerState Player(Vector3 position, SafeZoneSnapshot snapshot) => new(position,0,6,false,false,0,true,snapshot.Version);
    private static (object, IGreenMoveSystem) Create() => ((object, IGreenMoveSystem))typeof(LateDodgeRegression)
        .GetMethod("CreateMovement",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!;
    private static void Set(object target,string field,object value) => target.GetType().GetField(field,Private)!.SetValue(target,value);
    private static object? Call(object target,string method,params object?[] args) => target.GetType().GetMethod(method,Private)!.Invoke(target,args);
    private static void Publish(object movement, SafeZoneSolution solution)
    {
        var type = movement.GetType().GetNestedType("AsyncSolveResult",BindingFlags.NonPublic)!;
        var result = type.GetMethod("Completed")!.Invoke(null,[solution.SourceSnapshotVersion,solution,false]);
        var task = typeof(Task).GetMethod("FromResult")!.MakeGenericMethod(type).Invoke(null,[result])!;
        Set(movement,"_pendingSolveTask",task);
    }
    private static void Check(bool condition,string message) { if (!condition) throw new InvalidOperationException(message); }
}
