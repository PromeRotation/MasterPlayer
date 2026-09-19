using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Mechanics.Crucible;
using MasterPlayer.Scripting;
using PromeRotation.GreenMoveSystem;
using PromeRotation.Spatial.SafeZone;
using PromeRotation.Spatial.SafeZoneReborn.Solver;

internal static class RecordedLaudaRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run(Action<string, Action> run)
    {
        foreach(var key in new[]{"backward-torrent","forward-cleave"})
            foreach(var delay in new[]{.08,.3})
                run($"Recorded Lauda {key}, {delay:F2}s worker, 100ms script tick",()=>Replay(key,delay));
    }
    private static void Replay(string key, double delay)
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","Crucible","high2-234310.json")));
        var fight=fixture.RootElement.GetProperty("fights").EnumerateArray().Single(f=>f.GetProperty("key").GetString()==key);
        WPos Pos(JsonElement e)=>new(e[0].GetSingle(),e[2].GetSingle());
        double Number(string name)=>fight.GetProperty(name).GetDouble();
        var epoch=new DateTime(2026,9,19,15,43,10,DateTimeKind.Utc);
        var world=new WorldState{CurrentTime=epoch};
        var boss=new Actor{InstanceID=1,OID=19742,NameID=14693,Position=Pos(fight.GetProperty("player")),HPMP=(100,100),Type=ActorType.Enemy,IsTargetable=true,InCombat=true};
        var player=new Actor{InstanceID=2,Position=boss.Position,Rotation=((float)Number("rotation")).Degrees(),HPMP=(100,100),Type=ActorType.Player,IsTargetable=true};
        world.Actors.Add(boss);world.Actors.Add(player);world.Player=player;
        var engine=new CrucibleEngine(world,32,45);engine.SelectGroup();
        using var safeZone=new ScriptSafeZoneApi{DangerMarginForTest=.5f};
        var output=new CrucibleOutput(safeZone,new ScriptDrawApi(),safeZone.Manager,_=>{},()=>.5f);
        var casts=fight.GetProperty("casts").EnumerateArray().ToArray();
        var statuses=fight.GetProperty("statuses").EnumerateArray().ToArray();
        var helpers=casts.Select(e=>new Actor{InstanceID=e.GetProperty("actor").GetUInt64(),OID=9020,NameID=14693,Position=Pos(e.GetProperty("origin")),HPMP=(100,100),Type=ActorType.Enemy}).ToArray();
        foreach(var helper in helpers)world.Actors.Add(helper);
        var (movement,move)=((object,IGreenMoveSystem))typeof(LateDodgeRegression).GetMethod("CreateMovement",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!;
        var type=movement.GetType();
        type.GetField("_manager",Private)!.SetValue(movement,safeZone.Manager);
        type.GetField("_setting",Private)!.SetValue(movement,new SafeZoneSetting{EnableSafeZone=true,EnableAutoMovement=true,AttackUptimeExpiresAt=epoch.AddMinutes(1)});
        var latest=type.GetField("_latestSolution",Private)!;
        var dispatch=type.GetMethod("Dispatch",Private)!;
        var valid=type.GetMethod("IsSolutionValid",Private)!;
        var advance=move.GetType().GetMethod("Advance")!;
        var started=new HashSet<int>();var resolved=new HashSet<int>();var seenStatus=new HashSet<int>();
        bool turned=false,jumped=false,wasMoving=false;double ready=0,nextSolve=0,movementReady=0,lockUntil=0;
        SafeZoneSolution? pending=null;Angle? heading=null;
        var end=casts.Max(c=>c.GetProperty("hit").GetDouble())+.4;
        var release=statuses.Single(s=>s.GetProperty("id").GetUInt32()==5555&&!s.GetProperty("gain").GetBoolean()).GetProperty("time").GetDouble();
        for(var tick=0;tick*.02<=end;tick++)
        {
            var t=tick*.02;var now=epoch.AddSeconds(t);world.CurrentTime=now;
            for(var i=0;i<statuses.Length;i++)
            {
                var s=statuses[i];if(t<s.GetProperty("time").GetDouble()||!seenStatus.Add(i))continue;
                var id=s.GetProperty("id").GetUInt32();
                var status=new ActorStatus{ID=id,ExpireAt=epoch.AddSeconds(id==5555?release:Number("hit")-.09)};
                if(s.GetProperty("gain").GetBoolean())engine.Dispatch(c=>c.OnStatusGain(player,ref status));
                else engine.Dispatch(c=>c.OnStatusLose(player,ref status));
            }
            if(!turned && t>=Number("directionTime"))
            {
                turned=true;engine.Effect(boss,new(){Action=new(49485),Rotation=((float)Number("direction")).Degrees(),GlobalSequence=1});
            }
            for(var i=0;i<casts.Length;i++)
            {
                var c=casts[i];var action=c.GetProperty("action").GetUInt32();
                if(t>=c.GetProperty("start").GetDouble()&&started.Add(i))
                    engine.Cast(helpers[i],new(){Action=new(action),LocXZ=Pos(c.GetProperty("location")),Rotation=c.GetProperty("rotation").GetSingle().Degrees(),FinishAt=epoch.AddSeconds(c.GetProperty("finish").GetDouble())});
                if(t>=c.GetProperty("hit").GetDouble()&&resolved.Add(i))
                {
                    if(action==49453)
                    {
                        var kb=engine.Module!.FindComponent<CrucibleLaudaTorrent>()!.ActiveKnockbacks(0,player)[0];
                        player.Position+=kb.Direction.ToDirection()*20;lockUntil=t+.5;
                        Check(CrucibleLaudaFloor.Contains(player.Position),$"torrent landing outside: {player.Position}");
                    }
                    else
                    {
                        var shape=engine.Module!.FindComponent<XBMB45AOE>()!.Describe(action)!.Value.Shape;
                        Check(!shape.Check(player.Position,Pos(c.GetProperty("location")),c.GetProperty("rotation").GetSingle().Degrees()),$"{action} hit at {t:F2}, {player.Position}");
                    }
                    engine.Effect(helpers[i],new(){Action=new(action),GlobalSequence=(uint)i+10});engine.FinishCast(helpers[i],action);
                }
            }
            if(!jumped && t>=Number("hit"))
            {
                jumped=true;
                Check(heading!=null,"no heading at the knockback snapshot");
                var kb=engine.Module!.FindComponent<CrucibleLaudaShockwave>()!.ActiveKnockbacks(0,player)[0];
                var start=player.Position;player.Position+=kb.Direction.ToDirection()*40;lockUntil=t+1;
                Check(CrucibleLaudaFloor.Contains(player.Position),$"shockwave left floor: {start} => {player.Position}, heading={player.Rotation.Deg}");
                Console.WriteLine($"  {key}: {start} => {player.Position}, heading={player.Rotation.Deg:F2}");
                engine.Effect(boss,new(){Action=new(49489),GlobalSequence=2,Targets=[new(player.InstanceID)]});
            }
            if(tick%5==0)
            {
                engine.Update();var frame=engine.Frame();heading=CrucibleControlPlan.Build(frame.Hints,player,now).Heading;
                output.Apply(engine,frame,0,true,true,true);
            }
            var snapshot=safeZone.Manager.BuildSnapshot(now);var position=player.Position.ToVec3();
            if(pending!=null&&t>=ready){latest.SetValue(movement,pending);pending=null;}
            var accepted=(SafeZoneSolution?)latest.GetValue(movement);
            var state=new PlayerState(position,player.Rotation.Rad,6,false,false,0,true,snapshot.Version);
            if(pending==null&&t>=nextSolve&&!(bool)valid.Invoke(movement,[accepted,snapshot,state,now])!)
            {
                pending=new SafeZoneSolver().Solve(snapshot,state,new(6,6,0,false,6,6),new(),now,accepted);
                ready=t+delay;nextSolve=t+.1;
            }
            dispatch.Invoke(movement,[accepted,state,snapshot,now]);
            if(move.IsRunning&&!wasMoving)movementReady=t+.08;
            if(t>=release&&t>=lockUntil&&t>=movementReady)
            {
                var next=WPos.FromVec3((Vector3)advance.Invoke(move,[position,.02f])!);
                if((next-player.Position).LengthSq()>.000001f)player.Rotation=Angle.FromDirection(next-player.Position);
                player.Position=next;
                Check(CrucibleLaudaFloor.Contains(next),$"walk left floor at {t:F2}: {next}");
            }
            if(heading is {} facing)player.Rotation=facing;
            wasMoving=move.IsRunning;
        }
        Check(jumped&&resolved.Count==casts.Length,"fixture not exhausted");output.Clear();engine.Reset();
    }
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
