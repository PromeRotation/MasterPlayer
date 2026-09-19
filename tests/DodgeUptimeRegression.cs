using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Mechanics.Crucible;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;
using PromeRotation.GreenMoveSystem;
using PromeRotation.Spatial.Geometry;
using PromeRotation.Spatial.SafeZone;
using PromeRotation.Spatial.SafeZoneReborn.Solver;

internal static class DodgeUptimeRegression
{
    private static readonly DateTime Epoch = new(2026,9,19,12,53,42,DateTimeKind.Utc);
    private const BindingFlags Fields = BindingFlags.Instance|BindingFlags.NonPublic;
    public static void Run(Action<string,Action> run)
    {
        foreach (var delay in new[] { .0,.08,.2,.3 })
            run($"Dodge uptime recorded B01 Ancient Tomb without follow or target, {delay:F2}s worker", () => RecordedEscape(delay));

        run("Dodge uptime activation works with only auto-dodge and expires on disable or unload", () =>
        {
            var setting = new SafeZoneSetting();
            var now = Epoch;
            var activation = new SafeZoneActivation(setting,()=>now);
            activation.Update(false,false,true,true);
            Check(setting.EnableAutoMovement && setting.AttackUptimeExpiresAt==now.AddSeconds(.5), "standalone dodge did not request timing");
            now=now.AddSeconds(1);
            Check(setting.AttackUptimeExpiresAt<now,"stale plugin request stayed active");
            activation.Update(false,false,true,true);
            activation.Update(false,false,true,false);
            Check(setting.AttackUptimeExpiresAt==default && setting.EnableAutoMovement,"timing switch changed automatic dodge");
            activation.Update(false,false,true,true);
            activation.Update(false,false,false,true);
            Check(setting.AttackUptimeExpiresAt==default,"disabling dodge left timing active");
            activation.Update(false,false,true,true);
            activation.Dispose(); activation.Dispose();
            Check(setting.AttackUptimeExpiresAt==default,"unload leaked request");
        });

        run("Dodge uptime opt-out and expired requests retain immediate escape", () =>
        {
            var snapshot=Scene(new CircleGeometry(Vector3.Zero,3),5);
            var route=Solve(snapshot,Vector3.Zero,Epoch);
            foreach(var expiry in new[] {default(DateTime),Epoch.AddSeconds(-1)})
            {
                var (movement,move)=Movement(expiry);
                Dispatch(movement,route,snapshot,Vector3.Zero,Epoch);
                Check(move.IsRunning,"expired/disabled timing request delayed movement");
            }
        });

        run("Dodge uptime does not stop an escape after committing to movement", () =>
        {
            var snapshot=Scene(new CircleGeometry(Vector3.Zero,1),2);
            var now=Epoch.AddSeconds(1.2);
            var route=Solve(snapshot,Vector3.Zero,now);
            var (movement,move)=Movement(Epoch.AddSeconds(10));
            Dispatch(movement,route,snapshot,Vector3.Zero,now);
            Check(move.IsRunning,$"late escape waited: {route.DescribeOutcome()}");
            var next=Scene(new CircleGeometry(Vector3.Zero,1),5);
            Dispatch(movement,Solve(next,Vector3.Zero,now),next,Vector3.Zero,now);
            Check(move.IsRunning,"new candidate stopped a committed escape to greed again");
        });

        run("Dodge uptime holds a safe cast using actual exit time instead of final destination", () =>
        {
            var snapshot=Scene(new CircleGeometry(Vector3.Zero,1),2);
            var route=Route(snapshot,Vector3.Zero,new(0,0,6),new(2,0,6),new(2,0,0));
            var (movement,_)=Movement();
            Check(CastWait(movement,route,snapshot,1),"safe cast was interrupted because the harmless tail was counted as escape time");
        });

        run("Dodge uptime rejects a delayed crossing of a second hazard", () =>
        {
            var first=Scene(new CircleGeometry(Vector3.Zero,1),5);
            var snapshot=first.WithDanger(new CircleGeometry(new(4,0,0),.5f),Epoch.AddSeconds(2));
            var route=Route(snapshot,Vector3.Zero,new(8,0,0));
            var (movement,_)=Movement();
            Check(CanWait(movement,route,first,.3),"baseline escape should have slack");
            Check(!CanWait(movement,route,snapshot,1),"first safe exit concealed a later collision");
        });

        run("Dodge uptime rejects waiting through a temporary safe gap", () =>
        {
            var snapshot=Scene(new CircleGeometry(Vector3.Zero,1),5).WithDanger(new CircleGeometry(new(4,0,0),.5f),Epoch.AddSeconds(1.2),Epoch.AddSeconds(1.4));
            var route=Route(snapshot,Vector3.Zero,new(8,0,0));
            var (movement,_)=Movement();
            Check(!CanWait(movement,route,snapshot,.5),"waiting shifted the route into an intermediate hit");
        });

        run("Dodge uptime does not greed staggered waves even with a currently open escape", () =>
        {
            var snapshot=Scene(new CircleGeometry(Vector3.Zero,2),5)
                .WithDanger(new CircleGeometry(new(8,0,0),2),Epoch.AddSeconds(6));
            var route=Route(snapshot,Vector3.Zero,new(-4,0,0));
            var (movement,_)=Movement();
            Check(!CanWait(movement,route,snapshot,.15),"a still-empty lane postponed escape during an unfolding sequence");
        });

        foreach(var reason in new[] {"active damage","obstacle","boundary","mechanic goal","opaque constraint","no threat","invalid solution"})
            run($"Dodge uptime never waits for {reason}", () =>
            {
                var snapshot=Scene(new CircleGeometry(Vector3.Zero,3),5);
                var route=Route(snapshot,Vector3.Zero,new(8,0,0));
                snapshot=reason switch
                {
                    "active damage"=>Scene(new CircleGeometry(Vector3.Zero,3),-1),
                    "obstacle"=>new(snapshot.Version,Epoch,snapshot.ArenaBoundary,[new(default,new CircleGeometry(Vector3.Zero,1))],snapshot.DangerAreas,[]),
                    "boundary"=>new(snapshot.Version,Epoch,new CircleGeometry(new(10,0,0),2),[],snapshot.DangerAreas,[]),
                    "mechanic goal"=>new(snapshot.Version,Epoch,snapshot.ArenaBoundary,[],snapshot.DangerAreas,[new(default,new CircleGeometry(new(8,0,0),1),[new(Epoch,null,10,PositionPreference.Center)])]),
                    "opaque constraint"=>snapshot.WithDanger(new OpaqueCircle(new(15,0,0),1),Epoch.AddSeconds(2)),
                    "no threat"=>new(snapshot.Version,Epoch,snapshot.ArenaBoundary,[],[],[]),
                    _=>snapshot,
                };
                if(reason=="invalid solution")route=SafeZoneSolution.Failed(SolutionStatus.NoReachableSafeCell,snapshot.Version,Epoch,Epoch);
                var (movement,_)=Movement();
                Check(!CanWait(movement,route,snapshot,.15),"unsafe or mandatory positioning was postponed");
            });

        run("Dodge uptime preserves temporal transitions and emergency margins", () =>
        {
            var snapshot=Scene(new CircleGeometry(Vector3.Zero,3),5);
            var route=Solve(snapshot,Vector3.Zero,Epoch);
            var (movement,_)=Movement();
            typeof(SafeZoneSolution).GetProperty("UsesTemporalPlanning",Fields)!.SetValue(route,true);
            Check(!CanWait(movement,route,snapshot,.15),"temporal departure was shifted");
            typeof(SafeZoneSolution).GetProperty("UsesTemporalPlanning",Fields)!.SetValue(route,false);
            typeof(SafeZoneSolution).GetProperty("ReducedDangerStartSafetyTime",Fields)!.SetValue(route,.25f);
            Check(!CanWait(movement,route,snapshot,.15),"emergency escape was postponed");
        });

        run("Dodge uptime uses current movement speed when deciding departure", () =>
        {
            var snapshot=Scene(new CircleGeometry(Vector3.Zero,3),2);
            var route=Route(snapshot,Vector3.Zero,new(5,0,0));
            var (movement,_)=Movement();
            Check(CanWait(movement,route,snapshot,.8,6),"normal speed lost valid slack");
            Check(!CanWait(movement,route,snapshot,.8,3),"slow movement inherited the fast escape deadline");
        });

        run("Dodge uptime rechecks moving danger geometry instead of caching a countdown", () =>
        {
            var first=Scene(new CircleGeometry(Vector3.Zero,1),2);
            var expanded=Scene(new CircleGeometry(Vector3.Zero,4),2);
            var route=Route(first,Vector3.Zero,new(6,0,0));
            var (movement,_)=Movement();
            Check(CanWait(movement,route,first,1),"baseline escape lost slack");
            Check(!CanWait(movement,route,expanded,1),"changed geometry retained the old departure time");
        });

        run("Dodge uptime permits optional attack goals while preserving mechanic priority", () =>
        {
            var first=Scene(new CircleGeometry(Vector3.Zero,2),5);
            var snapshot=new SafeZoneSnapshot(first.Version,Epoch,first.ArenaBoundary,[],first.DangerAreas,
                [new(default,new CircleGeometry(new(6,0,0),2),[new(Epoch,null,-1,PositionPreference.Nearest)])]);
            var (movement,_)=Movement();
            Check(CanWait(movement,Route(snapshot,Vector3.Zero,new(5,0,0)),snapshot,.15),"optional attack preference disabled independent dodge timing");
        });
    }

    private static void RecordedEscape(double delay)
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","Crucible","uptime-low1-205342.json")));
        var data=fixture.RootElement;
        Vector3 V(string key) {var a=data.GetProperty(key);return new(a[0].GetSingle(),0,a[2].GetSingle());}
        var origin=V("origin"); var position=V("player");
        var hit=data.GetProperty("actualHit").GetDouble();
        var world=new WorldState {CurrentTime=Epoch};
        var boss=new Actor {InstanceID=1,OID=19334,NameID=14531,Position=WPos.FromVec3(origin),HPMP=(100,100),Type=ActorType.Enemy,IsTargetable=true,InCombat=true};
        var actor=new Actor {InstanceID=2,Position=WPos.FromVec3(position),HPMP=(100,100),Type=ActorType.Player,IsTargetable=true};
        world.Actors.Add(boss);world.Actors.Add(actor);world.Player=actor;
        var engine=new CrucibleEngine(world,1,45);engine.SelectGroup();
        using var safeZone=new ScriptSafeZoneApi {DangerMarginForTest=.5f};
        var output=new CrucibleOutput(safeZone,new ScriptDrawApi(),safeZone.Manager,_=>{},()=>.5f);
        engine.Cast(boss,new() {Action=new(46866),FinishAt=Epoch.AddSeconds(data.GetProperty("predictedHit").GetDouble()),LocXZ=boss.Position});
        var (movement,move)=Movement();
        var latest=movement.GetType().GetField("_latestSolution",Fields)!;
        var setting=(SafeZoneSetting)movement.GetType().GetField("_setting",Fields)!.GetValue(movement)!;
        var advance=move.GetType().GetMethod("Advance")!;
        var now=Epoch;
        using var activation=new SafeZoneActivation(setting,()=>now);
        SafeZoneSolution? pending=null;
        double ready=0,firstMove=-1,escaped=-1;
        var resolved=false;
        for(var tick=0;tick<=530;++tick)
        {
            var t=tick*.01; now=Epoch.AddSeconds(t);
            world.CurrentTime=now;actor.Position=WPos.FromVec3(position);
            if(t>=hit && !resolved) {engine.Effect(boss,new() {Action=new(46866),GlobalSequence=1});resolved=true;}
            engine.Update();output.Apply(engine,engine.Frame(),0,true,true,true);
            var snapshot=safeZone.Manager.BuildSnapshot(now);
            activation.Update(false,false,true,true);
            if(pending!=null && t+1e-6>=ready) {latest.SetValue(movement,pending);pending=null;}
            var accepted=(SafeZoneSolution?)latest.GetValue(movement);
            if(pending==null) {pending=Solve(snapshot,position,now,accepted);ready=t+Math.Max(.01,delay);}
            Dispatch(movement,accepted,snapshot,position,now);
            if(move.IsRunning && firstMove<0)firstMove=t;
            position=(Vector3)advance.Invoke(move,[position,.01f])!;
            if(Vector3.Distance(position,origin)>6.5f && escaped<0)escaped=t;
            if(t>=hit)Check(Vector3.Distance(position,origin)>6,$"recorded hit at {t:F2}, position={position}, first move={firstMove:F2}");
        }
        Check(firstMove>3 && firstMove<4.3,$"did not keep available uptime: departure={firstMove:F2}");
        Check(escaped>3 && escaped<hit-.15,$"late escape missed reserve: exit={escaped:F2}, hit={hit:F3}");
        Console.WriteLine($"  B01: depart={firstMove:F2}s, exit={escaped:F2}s, hit={hit:F3}s (recorded exit={data.GetProperty("recordedExit").GetDouble():F3}s)");
        output.Clear();
    }

    private static SafeZoneSnapshot Scene(IGeometryShape danger,double hit)=>new(1,Epoch,new CircleGeometry(Vector3.Zero,20),[],[new(default,danger,[new(Epoch.AddSeconds(hit),Epoch.AddSeconds(8))])],[]);
    private static SafeZoneSnapshot WithDanger(this SafeZoneSnapshot scene,IGeometryShape shape,DateTime start,DateTime? end=null)
        =>new(scene.Version,Epoch,scene.ArenaBoundary,scene.HardObstacles,scene.DangerAreas.Append(new(default,shape,[new(start,end)])).ToArray(),scene.GoalAreas);
    private static SafeZoneSolution Solve(SafeZoneSnapshot snapshot,Vector3 position,DateTime now,SafeZoneSolution? previous=null)
        =>new SafeZoneSolver().Solve(snapshot,new(position,0,6,false,false,0,true,snapshot.Version),new(6,6,0,false,6,6),new(),now,previous);
    private static SafeZoneSolution Route(SafeZoneSnapshot snapshot,params Vector3[] points)
        =>SafeZoneSolution.Succeeded(snapshot.Version,Epoch,Epoch,points[^1],points.Select(p=>TimedWaypoint.Pass(p,Epoch)).ToArray());
    private static (object,IGreenMoveSystem) Movement(DateTime? expiry=null)
    {
        var result=((object,IGreenMoveSystem))typeof(LateDodgeRegression).GetMethod("CreateMovement",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!;
        result.Item1.GetType().GetField("_setting",Fields)!.SetValue(result.Item1,new SafeZoneSetting{EnableSafeZone=true,EnableAutoMovement=true,AttackUptimeExpiresAt=expiry??Epoch.AddMinutes(1)});
        return result;
    }
    private static void Dispatch(object movement,SafeZoneSolution? route,SafeZoneSnapshot snapshot,Vector3 position,DateTime now)
        =>movement.GetType().GetMethod("Dispatch",Fields)!.Invoke(movement,[route,new PlayerState(position,0,6,false,false,0,true,snapshot.Version),snapshot,now]);
    private static bool CanWait(object movement,SafeZoneSolution route,SafeZoneSnapshot snapshot,double delay,float speed=6)
        =>(bool)movement.GetType().GetMethod("CanDelayEscape",Fields)!.Invoke(movement,[route,new PlayerState(Vector3.Zero,0,speed,false,false,0,true,snapshot.Version),snapshot,Epoch,delay])!;
    private static bool CastWait(object movement,SafeZoneSolution route,SafeZoneSnapshot snapshot,float cast)
        =>(bool)movement.GetType().GetMethod("ShouldWaitForCast",Fields)!.Invoke(movement,[route,new PlayerState(Vector3.Zero,0,6,false,true,cast,true,snapshot.Version),snapshot,Epoch])!;
    private sealed class OpaqueCircle(Vector3 position,float radius):IGeometryShape
    {
        private readonly CircleGeometry _shape=new(position,radius);
        public GeometryKind Kind=>GeometryKind.Circle;
        public Vector3 Position=>position;
        public float Rotation=>0;
        public bool Contains(Vector3 p,float epsilon=.0001f)=>_shape.Contains(p,epsilon);
        public bool IntersectsSegment(Vector3 a,Vector3 b,float epsilon=.0001f)=>_shape.IntersectsSegment(a,b,epsilon);
        public GeometryBounds GetBounds()=>_shape.GetBounds();
        public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments=64)=>_shape.GetContours(segments);
    }
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
