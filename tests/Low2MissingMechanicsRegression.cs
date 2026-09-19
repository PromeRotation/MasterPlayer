using MasterPlayer.Dodge;
using MasterPlayer.Mechanics.Crucible;
using MasterPlayer.Mechanics.Crucible.Components;
using MasterPlayer.Scripting;

internal static class Low2MissingMechanicsRegression
{
    public static void Run(Action<string, Action> run)
    {
        foreach (var action in new uint[] { 48198, 48201, 48231 })
            run($"Low-two missing {action}: exact packet geometry and complete lifecycle", () =>
            {
                using var f = new Fight(action == 48231);
                var origin = f.Center + new WDir(4, -3);
                var helper = f.Spawn(f.Center - new WDir(10, 10));
                f.Cast(helper, action, origin, 6);
                var aoe = f.Aoes.Single(a => a.ActorID == helper.InstanceID);
                Check(aoe.Origin == origin, "used helper's stale position instead of cast location");
                if (action == 48231)
                    Check(!aoe.Check(origin) && !aoe.Check(origin + new WDir(3.9f,0)) && aoe.Check(origin + new WDir(4.1f,0)) && !aoe.Check(origin + new WDir(40.1f,0)), "goblin ring has the circle branch's geometry");
                else
                    Check(aoe.Check(origin + new WDir(4.9f,0)) && !aoe.Check(origin + new WDir(5.1f,0)), "stomp is not a 5y circle");
                helper.Position += new WDir(20, 0);
                Check(f.Aoes.Single(a => a.ActorID == helper.InstanceID).Origin == origin, "fixed telegraph follows helper movement");
                f.Advance(5.7); f.Engine.FinishCast(helper, action);
                Check(f.Aoes.Any(a => a.ActorID == helper.InstanceID), "normal cast bar end cleared pending hit");
                f.Hit(helper, action);
                Check(f.Aoes.All(a => a.ActorID != helper.InstanceID), "effect failed to clear hit");
                f.Cast(helper, action, origin, 6); f.Engine.FinishCast(helper, action, cancelled:true);
                Check(f.Aoes.All(a => a.ActorID != helper.InstanceID), "interruption left a phantom hit");
                f.Cast(helper, action, origin, 6); f.Advance(10);
                Check(f.Aoes.All(a => a.ActorID != helper.InstanceID), "expired hit remains");
                f.Cast(helper, action, origin, 6); f.Engine.Destroy(helper,true);
                Check(f.Aoes.All(a => a.ActorID != helper.InstanceID), "destroyed helper remains");
                Check(CrucibleActionCatalog.IsClaimed(action) && AoeShapeDatabase.TryGetShape(action,out var generic) && generic!.UseCastRotation && !generic.FollowCaster, "generic dodge disagrees with the script");
            });

        run("Low-two stomp orders reversed packets, stages pushes and rejects the recorded wall landing", () =>
        {
            using var f = new Fight(false);
            var early = f.Spawn(f.Center); var late = f.Spawn(f.Center);
            f.Cast(late,48202,new(135,15),10.5);
            f.Cast(early,48199,new(135,-15),7);
            f.Player.Position = new(119.1882f,2.2736f);
            var component = f.Stomp;
            Check(component.ActiveKnockbacks(0,f.Player) is [{ ActorID: var id }] && id == early.InstanceID, "later packet preempted first knockback");
            var hints = new AIHints(); component.AddAIHints(0,f.Player,PartyRolesConfig.Assignment.Unassigned,hints);
            Check(hints.ForbiddenZones.Count == 1 && hints.ForbiddenZones[0].Shape.Distance(f.Player.Position)<0, "first landing ignored the next push into the wall");
            Check(hints.ForbiddenZones[0].Shape.Distance(new(130,-5))>0, "safe chained landings rejected as two simultaneous pushes");
            f.Advance(6.7); f.Engine.FinishCast(early,48199);
            Check(component.ActiveKnockbacks(0,f.Player)[0].ActorID == early.InstanceID, "normal bar end prematurely promoted second wave");
            f.Advance(.3); f.Hit(early,48199); f.Hit(early,48199);
            Check(component.NumCasts == 1 && component.ActiveKnockbacks(0,f.Player)[0].ActorID == late.InstanceID, "duplicate first hit consumed the second wave");
            f.Player.Position = new(105.6687f,17.0138f);
            hints = new(); component.AddAIHints(0,f.Player,PartyRolesConfig.Assignment.Unassigned,hints);
            Check(hints.ForbiddenZones.Single().Shape.Distance(f.Player.Position)<0, "recorded second-wave wall landing accepted");
            Check(hints.ForbiddenZones.Single().Shape.Distance(new(126,0))>0, "in-bounds second-wave setup rejected");
            Check(!AoeShapeDatabase.TryGetShape(48199,out _) && !AoeShapeDatabase.TryGetShape(48202,out _), "knockback reverted to a guessed full-arena donut");
        });

        foreach (var cleanup in new[] { "cancel", "death", "timeout", "disable" })
            run($"Low-two stomp {cleanup} clears pending displacement", () =>
            {
                using var f = new Fight(false); var helper = f.Spawn(f.Center);
                f.Cast(helper,48199,new(135,-15),7);
                if (cleanup == "cancel") f.Engine.FinishCast(helper,48199,true);
                if (cleanup == "death") f.Engine.Destroy(helper,false);
                if (cleanup == "timeout") f.Advance(10);
                if (cleanup == "disable")
                {
                    f.Engine.Enabled = name => name != nameof(CrucibleMinotaurStomp); f.Engine.Update();
                    Check(!f.Engine.Frame().Hints.ForbiddenZones.Any(z=>z.ExpiresAt==null && z.Activation!=default),"disabled component still constrains movement");
                    return;
                }
                Check(f.Stomp.ActiveKnockbacks(0,f.Player).Length==0,"stale knockback remains");
            });

        run("Low-two stomp immunity only removes the push, and expiring immunity is not enough", () =>
        {
            using var f = new Fight(false); var source=f.Spawn(f.Center); var circle=f.Spawn(f.Center);
            f.Cast(source,48199,new(135,-15),7); f.Cast(circle,48198,new(135,-15),7);
            f.Player.Statuses=[new(){ID=160,ExpireAt=f.World.FutureTime(8)}];
            var hints=new AIHints(); f.Stomp.AddAIHints(0,f.Player,PartyRolesConfig.Assignment.Unassigned,hints);
            Check(hints.ForbiddenZones.Count==0 && f.Aoes.Any(a=>a.ActorID==circle.InstanceID),"immunity erased stomp damage or left knockback constraint");
            f.Player.Statuses=[new(){ID=160,ExpireAt=f.World.FutureTime(6.9)}];
            f.Stomp.AddAIHints(0,f.Player,PartyRolesConfig.Assignment.Unassigned,hints);
            Check(hints.ForbiddenZones.Count==1,"immunity expires before impact but was still used");
        });
    }

    private sealed class Fight : IDisposable
    {
        public readonly WorldState World = new() { CurrentTime=DateTime.UtcNow.AddMinutes(1) };
        public readonly CrucibleEngine Engine;
        public readonly Actor Player, Boss;
        public readonly WPos Center;
        private ulong _nextId=100;
        public Fight(bool goblin)
        {
            Center=goblin ? new(520,-420) : new(120,0);
            Boss=new(){InstanceID=1,OID=goblin?19557u:19551u,NameID=goblin?14561u:14555u,Position=Center,Type=ActorType.Enemy,HPMP=(100,100),IsTargetable=true,InCombat=true};
            Player=new(){InstanceID=2,Position=Center,Type=ActorType.Player,HPMP=(100,100)};
            World.Actors.Add(Boss); World.Actors.Add(Player); World.Player=Player;
            Engine=new(World,7,13); Engine.SelectGroup();
        }
        public CrucibleMinotaurStomp Stomp=>Engine.Module!.FindComponent<CrucibleMinotaurStomp>()!;
        public GenericAOEs.AOEInstance[] Aoes=>Engine.Frame().Aoes.Select(a=>a.Aoe).ToArray();
        public Actor Spawn(WPos p) { var a=new Actor{InstanceID=_nextId++,OID=9020,Position=p,HPMP=(100,100),Type=ActorType.Enemy};World.Actors.Add(a);return a; }
        public void Cast(Actor a,uint action,WPos p,double delay)=>Engine.Cast(a,new(){Action=new(action),LocXZ=p,FinishAt=World.FutureTime(delay)});
        public void Hit(Actor a,uint action)=>Engine.Effect(a,new(){Action=new(action),GlobalSequence=(uint)a.InstanceID});
        public void Advance(double seconds) {World.CurrentTime=World.FutureTime(seconds);Engine.Update();}
        public void Dispose()=>Engine.Reset();
    }
    private static void Check(bool ok,string message) {if(!ok)throw new InvalidOperationException(message);}
}
