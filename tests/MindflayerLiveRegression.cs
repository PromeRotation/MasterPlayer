using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Crucible;
using MasterPlayer.Scripting;
using PromeRotation.LogSystem;
using PromeRotation.Spatial.SafeZone;
using PromeRotation.Spatial.SafeZoneReborn.Solver;

internal static class MindflayerLiveRegression
{
    private static readonly BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    public static void Run(Action<string, Action> run)
    {
        run("B33 23:58 ARR four glyphs escape immediately with the live 0.5y margin", () =>
        {
            using var f = new Fight(new(121.4466f,3.6469f));
            foreach (var line in new[] { (49204u,114.9767f,-35.0195f), (49203u,134.9966f,-45.0294f),
                (49203u,104.9973f,-55.0088f), (49204u,124.9866f,-65.0187f) })
                f.Cast(line.Item1,new(line.Item2,line.Item3),(-0.0027f).Degrees(),2);
            f.Render();
            var solution = f.Solve();
            Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2,
                $"the first solve waited until the 1.5s emergency gate: {solution.Status}");
            f.FollowUntil(2.036); // 177.259 cast -> 179.295 recorded hit
            Check(!f.Snapshot.DangerAreas.Any(d => d.Geometry.Contains(f.Player.Position.ToVec3())),
                "the full dispatcher/replan loop did not escape before the actual hit: " + string.Join("; ",f.Trace));
        });

        run("B33 same-origin packet corrects native-scan facing without duplicating a cross", () =>
        {
            using var f = new Fight(new(119.7375f,0.2289f));
            var origin = f.Player.Position;
            var helper = f.Cast(49207,origin,0f.Degrees(),4);
            var packetAngle = 70.6954f.Degrees();
            var adapter = new CrucibleEventAdapter(f.Engine,f.World.Actors.Find,_ => packetAngle.Rad,() => {},_ => {});
            var packet = new LogSystemCastStartEvent { Timestamp=f.Epoch, SourceId=helper.InstanceID, TargetId=helper.InstanceID,
                ActionId=49207,CastTime=3.7f,EffectPosition=origin.ToVec3(),SourcePosition=origin.ToVec3() };
            adapter.Handle(packet);
            adapter.Handle(packet);
            f.Render();
            var cross = f.Engine.Frame().Aoes.Where(a => a.Aoe.ActorID == helper.InstanceID).ToArray();
            Check(cross.Length==1 && cross[0].Aoe.Rotation==packetAngle,
                "same-action/same-location dedup discarded the authoritative packet facing");
            var actualHit = new WPos(122.6673f,6.9429f);
            Check(cross[0].Aoe.Shape.Check(actualHit,origin,packetAngle)
                && f.Snapshot.DangerAreas.Any(a=>a.Geometry.Contains(actualHit.ToVec3())), "recorded cross hit was considered safe");
        });

        run("B33 rotated crosses remain actionable from the recorded stop position", () =>
        {
            using var f = new Fight(new(122.6673f,6.9429f));
            foreach(var action in new uint[]{50939,49207})
                f.Cast(action,new(119.7375f,0.2289f),70.6954f.Degrees(),2.532);
            f.Render();
            Check(f.Snapshot.DangerAreas.Any(a=>a.Geometry.Contains(f.Player.Position.ToVec3())),"missing cross at recorded stop");
            f.FollowUntil(2.4);
            Check(!f.Snapshot.DangerAreas.Any(a=>a.Geometry.Contains(f.Player.Position.ToVec3())),
                "arrival tolerance or replan swallowed the remaining cross escape");
        });

        run("B33 cross geometry matches BMR distance at multiple rotations", () =>
        {
            var origin = new WPos(119.7375f,0.2289f);
            var shape = new AOEShapeCross(50,5);
            foreach(var angle in new[]{0f,23.5853f,70.6954f,90f,155f})
            {
                var rotation=angle.Degrees();var distance=shape.Distance(origin,rotation);var geometry=shape.Geometry(origin,rotation,0);
                for(float x=-18;x<18;x+=0.75f) for(float z=-18;z<18;z+=0.75f)
                {
                    var p=origin+new WDir(x,z);
                    if(Math.Abs(distance.Distance(p))<0.001f) continue;
                    Check(geometry.Contains(p.ToVec3())==(distance.Distance(p)<0),$"cross geometry differs from BMR at rotation {angle}");
                }
            }
        });
    }

    private sealed class Fight : IDisposable
    {
        public readonly DateTime Epoch = DateTime.UtcNow.AddMinutes(1);
        public readonly WorldState World = new();
        public readonly ScriptSafeZoneApi SafeZone = new() { DangerMarginForTest=0.5f };
        public readonly CrucibleEngine Engine;
        public readonly CrucibleOutput Output;
        public readonly Actor Player;
        private ulong _next=100;
        public readonly List<string> Trace=[];
        public SafeZoneSnapshot Snapshot => SafeZone.Manager.BuildSnapshot(World.CurrentTime);
        public Fight(WPos position)
        {
            World.CurrentTime=Epoch;
            var boss = new Actor {InstanceID=1,OID=19677,NameID=14633,Position=new(120,0),HitboxRadius=3,
                HPMP=(100,100),Type=ActorType.Enemy,IsTargetable=true,InCombat=true};
            Player=new(){InstanceID=2,Position=position,HPMP=(100,100),Type=ActorType.Player,IsTargetable=true};
            World.Actors.Add(boss);World.Actors.Add(Player);World.Player=Player;
            Engine=new(World,32,45);Engine.SelectGroup();
            Output=new(SafeZone,new ScriptDrawApi(),SafeZone.Manager,_=>{},()=>0.5f);
        }
        public Actor Cast(uint action,WPos origin,Angle rotation,double seconds)
        {
            var actor=new Actor{InstanceID=_next++,OID=9020,Position=origin,Type=ActorType.Enemy,HPMP=(100,100)};
            World.Actors.Add(actor);
            Engine.Cast(actor,new(){Action=new(action),LocXZ=origin,Rotation=rotation,TargetID=actor.InstanceID,FinishAt=Epoch.AddSeconds(seconds)});
            return actor;
        }
        public void Render(){Engine.Update();Output.Apply(Engine,Engine.Frame(),0,true,true,true);}
        public SafeZoneSolution Solve(SafeZoneSolution? previous=null)=>new SafeZoneSolver().Solve(Snapshot,
            new(Player.Position.ToVec3(),0,6,false,false,0,true,Snapshot.Version),new(6,6,0,false,6,6),new(),World.CurrentTime,previous);
        public void FollowUntil(double seconds)
        {
            var (movement, move)=((object,PromeRotation.GreenMoveSystem.IGreenMoveSystem))typeof(LateDodgeRegression)
                .GetMethod("CreateMovement",PrivateStatic)!.Invoke(null,null)!;
            var dispatch=typeof(LateDodgeRegression).GetMethod("Dispatch",PrivateStatic)!;
            var advance=move.GetType().GetMethod("Advance")!;
            SafeZoneSolution? solution=null;
            for(var tick=0;tick*0.01<seconds;++tick)
            {
                World.CurrentTime=Epoch.AddSeconds(tick*0.01);
                if(tick%10==0){Render();solution=Solve(solution);}
                // Include 50ms of publication/computation delay before the first dispatch.
                if(tick>=5)dispatch.Invoke(null,[movement,solution,Snapshot,Player.Position.ToVec3(),World.CurrentTime]);
                if(tick%10==0)Trace.Add($"t={tick*0.01:F1} pos={Player.Position} solve={solution!.Status} target={solution.TargetPosition} running={move.IsRunning} phase={movement.GetType().GetField("_phase",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(movement)}");
                Player.Position=WPos.FromVec3((Vector3)advance.Invoke(move,[Player.Position.ToVec3(),0.01f])!);
            }
        }
        public void Dispose(){Output.Clear();SafeZone.Dispose();}
    }
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
}
