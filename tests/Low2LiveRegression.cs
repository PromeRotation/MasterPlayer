using MasterPlayer.Dodge;
using MasterPlayer.Mechanics.Crucible;
using MasterPlayer.Mechanics.Crucible.Components;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class Low2LiveRegression
{
    public static void Run(Action<string, Action> run)
    {
        foreach (var (name, oid, radius) in new[] { (14549u,19545u,2f), (14549u,19546u,2f) })
            run($"low-two moving contact {oid} follows displacement and drops stationary forecasts", () =>
            {
                using var f = new Fight(name);
                var a = f.Spawn(oid, f.Center + new WDir(5,-5));
                f.Render();
                var c = f.Engine.Module!.Components.OfType<CrucibleMovingContact>().Single();
                Check(c.ActiveAOEs(0,f.Player).Length == 1,"stationary actor invented a forward danger");
                f.World.CurrentTime = f.World.FutureTime(0.1);
                a.Position += new WDir(0,0.2f); a.Rotation = 180.Degrees();
                f.Render();
                var aoes = c.ActiveAOEs(0,f.Player).ToArray();
                Check(aoes.Length == 3 && aoes.Any(x => x.Shape is AOEShapeRect && x.Rotation.Deg == 0),"forecast followed attack facing instead of measured movement");
                var endpoint = aoes[^1].Origin;
                Check(endpoint.Z > a.Position.Z && aoes[^1].Shape is AOEShapeCircle circle && circle.Radius == radius,"wrong contact radius/direction");
                var hints = new AIHints(); c.AddAIHints(0,f.Player,PartyRolesConfig.Assignment.Unassigned,hints);
                Check(hints.ForbiddenZones.All(z => z.Activation == default && z.Shape.Geometry(0) != null),"contact forecast postpones escape or resamples the entire arena");
                f.World.CurrentTime = f.World.FutureTime(0.11); f.Render();
                Check(c.ActiveAOEs(0,f.Player).Length == 1,"stationary forecast retained stale movement");
                f.World.CurrentTime = f.World.FutureTime(0.1); a.Position += new WDir(10,0); f.Render();
                Check(c.ActiveAOEs(0,f.Player).Length == 1,"teleport was extrapolated as movement");
                f.Engine.Destroy(a,true); f.Render();
                Check(c.ActiveAOEs(0,f.Player).Length == 0,"destroyed actor kept contact geometry");
            });

        run("B10 kill-first zombies never create body or movement hazards", () =>
        {
            using var f = new Fight(14552);
            var baseline = f.Snapshot.DangerAreas.Count;
            var zombie = f.Spawn(19549,f.Player.Position);
            f.Render();
            Check(f.Snapshot.DangerAreas.Count == baseline && f.Snapshot.HardObstacles.Count == 0,"zombie body still causes kiting");
            f.World.CurrentTime = f.World.FutureTime(0.1); zombie.Position += new WDir(0,0.2f); f.Render();
            Check(f.Snapshot.DangerAreas.Count == baseline,"moving zombie published a predicted avoidance area");
            Check(f.Engine.Frame().Hints.PotentialTargets.Single(t=>t.Actor==zombie).Priority == 1,"missing kill-first target hint");
            f.Spawn(19550,f.Player.Position); f.Render();
            Check(f.Snapshot.DangerAreas.Count > baseline,"disabling zombie avoidance also disabled miasma");
        });

        run("B09 ARR tornado forecast warns before the recorded 248.441 contact", () =>
        {
            using var f = new Fight(14549);
            var a = f.Spawn(19545,new(537.9873f,-0.412f));
            f.Render();
            f.World.CurrentTime = f.World.FutureTime(0.3); a.Position = new(537.9873f,0.2289f);
            f.Player.Position = new(538.6587f,2.3957f); f.Render();
            var c = f.Engine.Module!.FindComponent<CrucibleTornadoes>()!;
            Check(c.ActiveAOEs(0,f.Player).ToArray().Any(x => x.Check(f.Player.Position)),"recorded collision is outside the forward warning");
            var solution = f.Solve();
            Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2,"forward contact does not trigger an early escape");
            Check(!c.ActiveAOEs(0,f.Player).ToArray().Any(x => x.Check(WPos.FromVec3(solution.TargetPosition!.Value))),"escape ends inside the tornado forecast");
        });

        foreach (var hazard in new[] { "tornado", "fire", "cast" })
            run($"B09 typhoon validates {hazard} at the landing point and keeps the constraint until impact", () =>
            {
                using var f = new Fight(14549);
                var landing = f.Center + new WDir(0,11);
                f.Player.Position = f.Center + new WDir(0,1);
                if (hazard == "tornado") f.Spawn(19545,landing);
                else if (hazard == "fire") f.Spawn(2008685,landing);
                else f.Cast(f.Spawn(9020,landing),48172,landing,3);
                f.Cast(f.Boss,48168,f.Center,3); f.Render();
                var kb = f.Engine.Module!.FindComponent<CrucibleTyphoon>()!;
                var hints = new AIHints(); kb.AddAIHints(0,f.Player,PartyRolesConfig.Assignment.Unassigned,hints);
                Check(hints.ForbiddenZones.Any(z => z.Shape.Distance(f.Player.Position) < 0),"landing collision was ignored");
                Check(hints.ForbiddenZones.Any(z => z.Shape.Distance(f.Center) < 0),"exact knockback origin treated as a safe direction");
                var solution = f.Solve();
                Check(solution.IsSuccess && solution.TargetPosition != null,"no safe knockback setup");
                Check(hints.ForbiddenZones.All(z => z.Shape.Distance(WPos.FromVec3(solution.TargetPosition!.Value)) > 0),"solver setup still lands in danger");
                f.World.CurrentTime = f.World.FutureTime(2.7); f.Engine.FinishCast(f.Boss,48168);
                Check(kb.ActiveKnockbacks(0,f.Player).Length == 1,"cast bar end removed pending knockback");
                f.World.CurrentTime = f.World.FutureTime(0.3); f.Engine.Effect(f.Boss,new(){Action=new(48168),GlobalSequence=1});
                Check(kb.ActiveKnockbacks(0,f.Player).Length == 0,"impact left stale knockback");
            });

        run("B10 death-drive icon previews R10 then hands off to the locked ARR ground location", () =>
        {
            using var f = new Fight(14552);
            var before = f.Snapshot.DangerAreas.Count;
            f.Engine.Dispatch(c => c.OnEventIcon(f.Player,707,f.Player.InstanceID)); f.Render();
            var preview = f.Engine.Module!.FindComponent<CrucibleDeathDrive>()!;
            var deadline = preview.ActiveAOEs(0,f.Player)[0].Activation;
            Check(f.Snapshot.DangerAreas.Count == before && f.Draw.Previews.Values.Any(p => p.OutlineOnly && p.Geometry is CircleGeometry { Radius:10 }),"own bait became a chasing danger or was not drawn");
            f.World.CurrentTime = f.World.FutureTime(1); f.Player.Position = new(122.9725f,-9.6895f);
            f.Engine.Dispatch(c => c.OnEventIcon(f.Player,707,f.Player.InstanceID)); f.Render();
            Check(preview.ActiveAOEs(0,f.Player)[0].Activation == deadline,"duplicate icon extended deadline");
            var helper = f.Spawn(9020,new(120,-10)); f.Cast(helper,48182,f.Player.Position,4); f.Render();
            Check(preview.ActiveAOEs(0,f.Player).Length == 0,"tracking bait overlaps the now-locked circle");
            var locked = f.Engine.Module.FindComponent<XBMB10AOE>()!.ActiveAOEs(0,f.Player)[0];
            f.Player.Position = new(130,5); helper.Position = new(100,10);
            Check(locked.Origin == new WPos(122.9725f,-9.6895f),"ground circle followed player/helper");
            f.World.CurrentTime = f.World.FutureTime(3.7); f.Engine.FinishCast(helper,48182); f.Render();
            Check(f.Engine.Module.FindComponent<XBMB10AOE>()!.ActiveAOEs(0,f.Player).Length == 1,"cast end removed unresolved impact");
            f.World.CurrentTime = f.World.FutureTime(0.3); f.Engine.Effect(helper,new(){Action=new(48182),GlobalSequence=1});
            Check(f.Engine.Module.FindComponent<XBMB10AOE>()!.ActiveAOEs(0,f.Player).Length == 0,"effect did not remove locked circle");
        });

        foreach (var cleanup in new[] { "cancel", "timeout", "death", "group" })
            run($"B10 death-drive {cleanup} clears preview", () =>
            {
                using var f = new Fight(14552);
                f.Engine.Dispatch(c => c.OnEventIcon(f.Player,707,f.Player.InstanceID)); f.Cast(f.Boss,48181,f.Center,7); f.Render();
                var c = f.Engine.Module!.FindComponent<CrucibleDeathDrive>()!;
                if (cleanup == "cancel") f.Engine.FinishCast(f.Boss,48181,true);
                else if (cleanup == "timeout") f.World.CurrentTime = f.World.FutureTime(8.3);
                else f.Engine.Destroy(cleanup == "death" ? f.Player : f.Boss,true);
                f.Render();
                Check(c.ActiveAOEs(0,f.Player).Length == 0,"stale target marker");
            });

        run("B10 spirit control draws the observed 18y march and checks hazards along the entire route", () =>
        {
            using var f = new Fight(14552);
            f.Player.Position = new(120.6531f,-15.6405f); f.Player.Rotation = (-1.1289f).Degrees();
            f.Player.Statuses = [new(){ID=2161,ExpireAt=f.World.FutureTime(15)}];
            f.Spawn(19550,new(125,-5)); f.Render();
            var march = f.Engine.Module!.FindComponent<CrucibleNecromancerMarch>()!;
            var path = march.Projection(f.Player)!.Value;
            Check(Math.Abs((path.End - f.Player.Position).Length() - 18) < 0.01f,"wrong ARR march length");
            Check(!path.End.InCircle(new(125,-5),8) && path.Unsafe,"safe endpoint hid crossing miasma");
            Check(f.Draw.Lines.Count >= 3,"march arrow did not reach the shared renderer");
            f.Player.Statuses = [new(){ID=1257,ExpireAt=f.World.FutureTime(1)}];
            Check(Math.Abs((march.Projection(f.Player)!.Value.End - f.Player.Position).Length()-6)<0.01f,"active march failed to shrink by remaining status duration");
            f.Player.Statuses = []; f.Render();
            Check(march.Projection(f.Player)==null && f.Draw.Lines.Count == 0,"status removal left march arrow");
            f.Player.Statuses = [new(){ID=2161,ExpireAt=DateTime.MaxValue}];
            Check(march.Projection(f.Player)==null,"unknown duration invented a march deadline");
        });

        run("B10 stationary miasma owns 48184 and retains an exact generic fallback", () =>
        {
            Check(CrucibleActionCatalog.IsClaimed(48184),"generic cast warning duplicates the persistent mist");
            Check(AoeShapeDatabase.TryGetShape(48184,out var shape) && shape!.Param1 == 8 && shape.ExtraDelaySeconds == 0.3f,"missing exact mist cast fallback");
        });
    }

    private sealed class Fight : IDisposable
    {
        public readonly WorldState World = new() { CurrentTime = DateTime.UtcNow.AddMinutes(1) };
        public readonly ScriptSafeZoneApi SafeZone = new() { DangerMarginForTest=0.5f };
        public readonly ScriptDrawApi Draw = new();
        public readonly CrucibleEngine Engine;
        public readonly CrucibleOutput Output;
        public readonly Actor Boss, Player;
        public readonly WPos Center;
        private ulong _id=100;
        public SafeZoneSnapshot Snapshot => SafeZone.Manager.BuildSnapshot(World.CurrentTime);
        public Fight(uint name)
        {
            Center = name==14549 ? new(520,0) : new(120,0);
            Boss=new(){InstanceID=1,OID=name==14549?19544u:19548u,NameID=name,Position=Center,Type=ActorType.Enemy,HPMP=(100,100),IsTargetable=true,InCombat=true};
            Player=new(){InstanceID=2,Position=Center,Type=ActorType.Player,HPMP=(100,100),IsTargetable=true};
            World.Actors.Add(Boss); World.Actors.Add(Player); World.Player=Player;
            Engine=new(World,7,13); Engine.SelectGroup();
            Output=new(SafeZone,Draw,SafeZone.Manager,_=>{},()=>0.5f); Render();
        }
        public Actor Spawn(uint oid,WPos position)
        {
            var a=new Actor{InstanceID=_id++,OID=oid,Position=position,Type=ActorType.Enemy,HPMP=(100,100)};
            World.Actors.Add(a); Engine.Dispatch(c=>c.OnActorCreated(a)); return a;
        }
        public void Cast(Actor source,uint action,WPos location,double delay) => Engine.Cast(source,new(){Action=new(action),LocXZ=location,FinishAt=World.FutureTime(delay)});
        public void Render(){Engine.Update();Output.Apply(Engine,Engine.Frame(),0,true,true,true);}
        public SafeZoneSolution Solve()=>new SafeZoneSolver().Solve(Snapshot,new(Player.Position.ToVec3(),0,6,false,false,0,true,Snapshot.Version),new(6,6,0,false,6,6),new(),World.CurrentTime);
        public void Dispose(){Output.Clear();SafeZone.Dispose();}
    }
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
