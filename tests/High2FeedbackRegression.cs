using System.Diagnostics;
using MasterPlayer.Mechanics.Crucible;
using MasterPlayer.Scripting;

internal static class High2FeedbackRegression
{
    public static void Run(Action<string,Action> run)
    {
        foreach(var scenario in new[]{"numbers","torrent","shockwave with fireknife","fireknife"})
            run($"High2 feedback performance {scenario}",()=>
            {
                using var f=new Fight(scenario=="numbers");
                if(scenario=="numbers")
                {
                    for(uint n=1;n<=9;n++)f.Spawn(2015492+n,f.Center+new WDir(((n-1)%3-1f)*12,((n-1)/3-1f)*12));
                    f.Player.Statuses=[new(){ID=5149,ExpireAt=f.World.FutureTime(12)}];
                }
                else if(scenario=="torrent")f.Cast(f.Boss,49453,f.Center,-90,7);
                else
                {
                    if(scenario!="fireknife"){f.Bind(true,12);f.Advance(2);}
                    f.Cast(f.Spawn(9020,new(532,-417)),49469,new(532,-417),0,15);
                }
                f.Render();
                var watch=Stopwatch.StartNew();
                for(var i=0;i<5;i++){f.Advance(.1);f.Render();}
                Console.WriteLine($"PERF {scenario}: stable full frame {watch.Elapsed.TotalMilliseconds/5:F2}ms");
                Check(watch.Elapsed.TotalMilliseconds<250,"stable mechanism input/drawing generation exceeds 50ms per frame");
            });

        run("High2 feedback numbers cache invalidates on moved or dormant cells",()=>
        {
            using var f=new Fight(true);var tile=f.Spawn(2015494,f.Center);
            f.Player.Statuses=[new(){ID=5148,ExpireAt=f.World.FutureTime(12)}];
            var constraint=f.Frame.Hints.ForbiddenZones.Last().Shape;var geometry=constraint.Geometry(0);
            Check(ReferenceEquals(constraint,f.Frame.Hints.ForbiddenZones.Last().Shape) && ReferenceEquals(geometry,constraint.Geometry(0)),"stable numbers recreated grid");
            Check(geometry!.Contains((f.Center+new WDir(8,0)).ToVec3()) && !geometry.Contains(f.Center.ToVec3()),"number constraint inverted");
            tile.Position+=new WDir(12,0);
            var changed=f.Frame.Hints.ForbiddenZones.Last().Shape;
            Check(!ReferenceEquals(constraint,changed) && changed.Distance(tile.Position)>0 && changed.Distance(f.Center)<0,"changed grid retained old correct tile");
            tile.EventState=7;
            Check(f.Frame.Hints.GoalZones.Count==0 && !f.Frame.Aoes.Any(a=>a.Component==nameof(CrucibleSphinxNumbers)),"dormant number still drawn");
        });

        run("High2 feedback animal follows original identity and consumes only the correct answer",()=>
        {
            using var f=new Fight(true);
            var animal=f.Spawn(19714,f.Center+new WDir(15,15));var other=f.Spawn(19711,f.Center+new WDir(-15,15));
            f.Talk(2);f.Engine.Update();f.Talk(4); // scalekin
            animal.OID=19711;other.OID=19714; // visual morph must not replace remembered identity
            animal.Position=f.Center+new WDir(-15,-15);other.Position=f.Center+new WDir(15,15);
            var correct=f.Spawn(2015459,animal.Position);f.Spawn(2015459,other.Position);
            f.Talk(32);
            Check(f.Frame.Hints.InteractWithTarget==correct,"wrong original animal chosen");
            correct.EventState=7;f.Engine.Update();
            Check(f.Frame.Hints.InteractWithTarget==null && f.Frame.Hints.GoalPoints.Count==0,"consumed answer retried or switched");
        });

        run("High2 feedback interaction waits for rest, throttles and preserves progress",()=>
        {
            using var f=new Fight(true);var target=f.Spawn(2015459,f.Center);
            var control=new CrucibleInteraction();int requests=0;
            bool Call(ulong id){Check(id==target.InstanceID,"wrong interaction ID");requests++;return true;}
            Check(!control.Update(target,f.Player,f.World.CurrentTime,false,false,Call),"interaction sent before stable position");
            f.Advance(.3);
            Check(!control.Update(target,f.Player,f.World.CurrentTime,true,false,Call),"interaction sent during automated movement");
            Check(control.Update(target,f.Player,f.World.CurrentTime,false,false,Call),"ready answer did not interact");
            f.Advance(2);
            Check(!control.Update(target,f.Player,f.World.CurrentTime,false,true,Call) && requests==1,"interaction interrupted progress");
            target.EventState=7;
            Check(!control.Update(target,f.Player,f.World.CurrentTime,false,false,Call) && !control.Attempted,"consumed object retried");
        });

        run("High2 feedback target is selected once and the host lease is released immediately",()=>
        {
            Host.Calls.Clear();var bridge=new CrucibleHostControl(_=>{},typeof(Host));
            var plan=new CrucibleControlPlan(5,0,[6],1.Degrees(),true,true,false);
            for(var i=0;i<10;i++)bridge.Apply(plan);
            Check(Host.Calls.Count(p=>p.Target==5)==1 && Host.Calls.Last().Target==0 && Host.Calls.Last().ForbiddenTargets.Length==0,"target was repeatedly selected or left locked");
            Check(Host.Calls.Last().Heading!=null,"target release removed necessary facing");
            bridge.Apply(plan with{Target=7});Check(Host.Calls.Count(p=>p.Target==7)==1,"new phase did not select once");
            bridge.Clear();bridge.Apply(plan);Check(Host.Calls.Count(p=>p.Target==5)==2,"unload/new encounter did not reset selection");
        });

        foreach(var forward in new[]{false,true})
            run($"High2 feedback actual shockwave facing keeps a 40m landing safe, forward={forward}",()=>
            {
                using var f=new Fight(false);f.Bind(forward,17);f.Advance(3);
                var c=f.Engine.Module!.FindComponent<CrucibleLaudaShockwave>()!;
                var goal=f.Frame.Hints.GoalPoints.Single().Position;
                foreach(var offset in new[]{new WDir(0,0),new WDir(.5f,-.5f),new WDir(-.5f,1.1f)})
                {
                    f.Player.Position=goal+offset;f.Player.Rotation=37.Degrees();
                    var h=f.Frame.Hints;
                    Check(h.DesiredFacing!=null,"safe staging location got no facing control");
                    f.Player.Rotation=h.DesiredFacing!.Value;
                    var source=c.ActiveKnockbacks(0,f.Player)[0];
                    Check(CrucibleLaudaFloor.Contains(f.Player.Position+source.Direction.ToDirection()*40),"requested actual heading lands outside platform");
                }
                f.Engine.Effect(f.Boss,new(){Action=new(49489),GlobalSequence=2,Targets=[new(f.Player.InstanceID)]});
                Check(f.Frame.Hints.DesiredFacing==null && c.ActiveKnockbacks(0,f.Player).Length==0,"effect retained orientation lock");
            });
    }
    public static class Host
    {
        public static readonly List<CrucibleControlPlan> Calls=[];
        public static void Update(string owner,ulong target,ulong clearTarget,ulong[] forbidden,float? heading,bool hold,bool block,bool cancel)
            =>Calls.Add(new(target,clearTarget,forbidden,heading is {} angle?new(angle):null,hold,block,cancel));
        public static void Clear(string owner){}
    }
    private sealed class Fight:IDisposable
    {
        public readonly WorldState World=new(){CurrentTime=DateTime.UtcNow.AddMinutes(1)};
        public readonly CrucibleEngine Engine;public readonly Actor Player,Boss;public readonly WPos Center;
        public readonly ScriptSafeZoneApi SafeZone=new(){DangerMarginForTest=.5f};
        private readonly CrucibleOutput output;private ulong nextId=10;
        public Fight(bool sphinx)
        {
            Center=sphinx?new(120,0):new(520,-420);
            Boss=new(){InstanceID=1,OID=sphinx?19710u:19742u,NameID=sphinx?14665u:14693u,Position=Center,Type=ActorType.Enemy,IsTargetable=true,InCombat=true,HPMP=(100,100)};
            Player=new(){InstanceID=2,Position=Center,Type=ActorType.Player,HPMP=(100,100)};
            World.Actors.Add(Boss);World.Actors.Add(Player);World.Player=Player;
            Engine=new(World,32,45);Engine.SelectGroup();output=new(SafeZone,new ScriptDrawApi(),SafeZone.Manager,_=>{},()=>.5f);
        }
        public CrucibleFrame Frame=>Engine.Frame();
        public void Render()=>output.Apply(Engine,Frame,0,true,true,true);
        public Actor Spawn(uint oid,WPos pos){var a=new Actor{InstanceID=nextId++,OID=oid,Position=pos,Type=oid>2000000?ActorType.EventObj:ActorType.Enemy,IsTargetable=true,HPMP=(100,100)};World.Actors.Add(a);return a;}
        public void Advance(double seconds){World.CurrentTime=World.FutureTime(seconds);Engine.Update();}
        public void Cast(Actor a,uint action,WPos pos,float rotation,double seconds)=>Engine.Cast(a,new(){Action=new(action),LocXZ=pos,Rotation=rotation.Degrees(),FinishAt=World.FutureTime(seconds)});
        public void Talk(uint id)=>Engine.Dispatch(c=>c.OnEventDirectorUpdate(0x80000027,id,0,Boss.NameID,(uint)Boss.InstanceID));
        public void Bind(bool forward,double delay)
        {
            var bound=new ActorStatus{ID=5555,ExpireAt=World.FutureTime(1)};var mark=new ActorStatus{ID=5341,ExpireAt=World.FutureTime(delay)};
            Engine.Dispatch(c=>c.OnStatusGain(Player,ref bound));Engine.Dispatch(c=>c.OnStatusGain(Player,ref mark));
            Engine.Effect(Boss,new(){Action=new(49485),Rotation=(forward?0:180).Degrees(),GlobalSequence=1});
        }
        public void Dispose(){output.Clear();Engine.Reset();SafeZone.Dispose();}
    }
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
