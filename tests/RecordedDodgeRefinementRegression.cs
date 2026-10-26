using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Mechanics.Crucible;
using MasterPlayer.Follow;
using MasterPlayer.Scripting;
using PromeRotation.GreenMoveSystem;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class RecordedDodgeRefinementRegression
{
    private static readonly DateTime Epoch = new(2026,9,19,13,29,27,DateTimeKind.Utc);
    private const BindingFlags Private = BindingFlags.Instance|BindingFlags.NonPublic;
    public static void Run(Action<string,Action> run)
    {
        foreach(var key in new[] { "cleave", "uplift" })
            foreach(var delay in new[] { .08, .3 })
                foreach(var follow in new[] { false, true })
                    run($"Recorded dodge refinement {key}, {delay:F2}s worker, follow={follow}",()=>Replay(key,delay,follow));
        // B02 Dismember sweep: cross the wave front next to the boss instead of outrunning all five strips.
        foreach(var (file, limit) in new[] { ("dodge-low1-212927.json", 6.0), ("dismember-low1-230421.json", 6.0) })
            foreach(var delay in new[] { .08, .3 })
                run($"Recorded B02 dismember sweep {file[^11..^5]} stays short, {delay:F2}s worker",()=>Replay(file.StartsWith("dodge") ? "cleave" : "dismember",delay,false,file,limit));
        foreach(var key in new[] { "dark-orb", "goblin-ring", "stomp" })
            foreach(var delay in new[] { .08, .3 })
                foreach(var follow in new[] { false, true })
                    run($"Recorded low-two {key}, {delay:F2}s worker, follow={follow}",()=>Replay(key,delay,follow,"low2-225025.json"));
    }

    private static Vector3 Vector(JsonElement e) => new(e[0].GetSingle(),0,e[2].GetSingle());
    private static void Replay(string key, double delay, bool follow, string file = "dodge-low1-212927.json", double maxPath = double.PositiveInfinity)
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","Crucible",file)));
        var fight=fixture.RootElement.GetProperty("fights").EnumerateArray().Single(f=>f.GetProperty("key").GetString()==key);
        var events=fight.GetProperty("casts").EnumerateArray().ToArray();
        var world=new WorldState {CurrentTime=Epoch};
        var boss=new Actor {InstanceID=1,OID=fight.GetProperty("oid").GetUInt32(),NameID=fight.GetProperty("nameId").GetUInt32(),Position=WPos.FromVec3(Vector(fight.GetProperty("boss"))),HPMP=(100,100),Type=ActorType.Enemy,IsTargetable=true,InCombat=true};
        var player=new Actor {InstanceID=2,Position=WPos.FromVec3(Vector(fight.GetProperty("player"))),HPMP=(100,100),Type=ActorType.Player,IsTargetable=true};
        world.Actors.Add(boss);world.Actors.Add(player);world.Player=player;
        var engine=new CrucibleEngine(world,1,45);engine.SelectGroup();
        using var safeZone=new ScriptSafeZoneApi {DangerMarginForTest=.5f};
        var output=new CrucibleOutput(safeZone,new ScriptDrawApi(),safeZone.Manager,_=>{},()=>.5f);
        var helpers=events.Select(e=>new Actor {InstanceID=e.GetProperty("actor").GetUInt64(),OID=9020,NameID=boss.NameID,Position=WPos.FromVec3(Vector(e.GetProperty("origin"))),HPMP=(100,100),Type=ActorType.Enemy}).ToArray();
        foreach(var helper in helpers)world.Actors.Add(helper);
        var (movement,move)=((object,IGreenMoveSystem))typeof(LateDodgeRegression).GetMethod("CreateMovement",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!;
        var type=movement.GetType();
        type.GetField("_manager",Private)!.SetValue(movement,safeZone.Manager);
        type.GetField("_setting",Private)!.SetValue(movement,new SafeZoneSetting {EnableSafeZone=true,EnableAutoMovement=true,AttackUptimeExpiresAt=Epoch.AddMinutes(1)});
        var latest=type.GetField("_latestSolution",Private)!;
        var dispatch=type.GetMethod("Dispatch",Private)!;
        var valid=type.GetMethod("IsSolutionValid",Private)!;
        var advance=move.GetType().GetMethod("Advance")!;
        var started=new HashSet<int>();var resolved=new HashSet<int>();
        SafeZoneSolution? pending=null;
        double ready=0, nextSolve=0,firstMove=-1;
        double movementReady=0;
        double knockbackUntil=0;
        var wasMoving=false;
        var followHandle=default(SafeZoneAreaHandle);
        var followActive=false;
        string lastStatus="";
        var position=player.Position.ToVec3();
        var travelled=0d;
        var end=events.Max(e=>e.GetProperty("hit").GetDouble())+1;
        for(var tick=0;tick*.02<=end;tick++)
        {
            var t=tick*.02;var now=Epoch.AddSeconds(t);
            world.CurrentTime=now;player.Position=WPos.FromVec3(position);
            for(var i=0;i<events.Length;i++)
            {
                var e=events[i];var action=e.GetProperty("action").GetUInt32();
                if(t>=e.GetProperty("start").GetDouble() && started.Add(i))
                    engine.Cast(helpers[i],new() {Action=new(action),FinishAt=Epoch.AddSeconds(e.GetProperty("finish").GetDouble()),LocXZ=WPos.FromVec3(Vector(e.GetProperty("location"))),Rotation=e.GetProperty("rotation").GetSingle().Degrees()});
                if(t>=e.GetProperty("hit").GetDouble() && resolved.Add(i))
                {
                    if(action is 48199 or 48202)
                    {
                        position += Vector3.Normalize(position-Vector(e.GetProperty("location")))*20;
                        knockbackUntil=t+.5; // Knockback row 96: 20y at 40y/s, no walking during displacement.
                        if(!WPos.FromVec3(position).InRect(engine.Module!.Center,19.5f,19.5f))
                            throw new InvalidOperationException($"{key} knockback {action} leaves the arena at {t:F2}s: {position}");
                    }
                    else
                    {
                        var shape=engine.Module!.Components.OfType<MasterPlayer.Mechanics.Crucible.CriticalEngagement.ReplayValidatedCastAOEs>()
                            .Select(c=>c.Describe(action)?.Shape).First(s=>s!=null)!;
                        if(shape.Check(player.Position,WPos.FromVec3(Vector(e.GetProperty("location"))),e.GetProperty("rotation").GetSingle().Degrees()))
                            throw new InvalidOperationException($"{key} hit {action} at {t:F2}s, pos={position}, firstMove={firstMove:F2}, last={lastStatus}");
                    }
                    engine.Effect(helpers[i],new() {Action=new(action),GlobalSequence=(uint)i+1});
                    engine.FinishCast(helpers[i],action);
                }
            }
            player.Position=WPos.FromVec3(position);
            engine.Update();output.Apply(engine,engine.Frame(),0,true,true,true);
            var snapshot=safeZone.Manager.BuildSnapshot(now);
            if(follow)
            {
                var goal=new DangerAwareFollowGoalTemplate(6,snapshot.DangerAreas
                    .Where(a=>a.DangerWindows.Any(w=>OptionalGoalSafety.Pending(w.DangerFrom,w.DangerUntil,now)))
                    .Select(a=>a.Geometry),snapshot.ArenaBoundary);
                var active=goal.HasSafeArea(boss.Position.ToVec3());
                GoalWindow[] windows=active?[new(now,null,-1,PositionPreference.Nearest)]:[];
                if(followHandle.IsValid)
                    safeZone.Manager.UpdateArea(followHandle,template:goal,goalWindows:windows,incrementVersion:active!=followActive);
                else
                    followHandle=safeZone.Manager.AddGoalArea(goal,new StaticTransform(boss.Position.ToVec3(),0),windows);
                followActive=active;
                snapshot=safeZone.Manager.BuildSnapshot(now);
            }
            if(pending!=null && t>=ready) {latest.SetValue(movement,pending);pending=null;}
            var accepted=(SafeZoneSolution?)latest.GetValue(movement);
            if(pending==null && t>=nextSolve && !(bool)valid.Invoke(movement,[accepted,snapshot,new PlayerState(position,0,6,false,false,0,true,snapshot.Version),now])!)
            {
                pending=new SafeZoneSolver().Solve(snapshot,new(position,0,6,false,false,0,true,snapshot.Version),new(6,6,0,false,6,6),new(),now,accepted);
                ready=t+delay;nextSolve=t+.1;
                var status=$"{pending.Status}, temporal={pending.UsesTemporalPlanningForTest()}, path={pending.ExecutablePath.Count}";
                if(status!=lastStatus) {Console.WriteLine($"  {key} t={t:F2} {status}");lastStatus=status;}
            }
            dispatch.Invoke(movement,[accepted,new PlayerState(position,0,6,false,false,0,true,snapshot.Version),snapshot,now]);
            if(move.IsRunning && firstMove<0)firstMove=t;
            if(move.IsRunning && !wasMoving)movementReady=t+.08;
            if(t>=movementReady && t>=knockbackUntil)
            {
                var next=(Vector3)advance.Invoke(move,[position,.02f])!;
                travelled+=Vector3.Distance(next,position);position=next;
            }
            wasMoving=move.IsRunning;
        }
        Console.WriteLine($"  {key}: firstMove={firstMove:F2}, travelled={travelled:F1}m, all {events.Length} impacts avoided");
        if(travelled>maxPath)
            throw new InvalidOperationException($"{key} travelled {travelled:F1}m (limit {maxPath:F1}m) instead of crossing the wave front");
        output.Clear();
    }
    private static bool UsesTemporalPlanningForTest(this SafeZoneSolution solution)
        =>(bool)typeof(SafeZoneSolution).GetProperty("UsesTemporalPlanning",Private)!.GetValue(solution)!;
}
