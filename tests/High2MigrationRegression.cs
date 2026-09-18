using System.Numerics;
using System.Reflection;
using MasterPlayer.Radar;
using MasterPlayer.Settings;
using MasterPlayer.Mechanics.Crucible;
using MasterPlayer.Mechanics.Crucible.Components;
using MasterPlayer.Mechanics.Crucible.CriticalEngagement;
using MasterPlayer.Scripting;
using PromeRotation.LogSystem;
using PromeRotation.Spatial.SafeZone;
using PromeRotation.Spatial.SafeZoneReborn.Solver;

internal static class High2MigrationRegression
{
    public static void Run(Action<string, Action> run)
    {
        uint[] names = [14631,14633,14641,14642,14651,14657,14660,14663,14665,14670,14675,14684,14688,14693];
        for (var index = 0; index < names.Length; ++index)
        {
            var name=names[index]; var group=index+32;
            run(group == 44 ? "High2 B44 gaze and proximity effects do not become arena-wide ground hazards"
                : $"High2 B{group} every configured cast updates facing, survives completion and cleans cancellation/death/timeout", () =>
            {
                using var f=new Fight(name,name==14693 ? new(520,-420) : new(120,0));
                var component=f.Engine.Module!.Components.OfType<ReplayValidatedCastAOEs>().Single();
                var actions=Enum.GetValues<AID>().Select(a=>(uint)a).Concat(Enumerable.Range(49000,2001).Select(a=>(uint)a))
                    .Where(a=>component.Describe(a)!=null).Distinct().ToArray();
                if(group==44) { Check(actions.Length==0,"gaze/proximity has an invented ground danger"); return; }
                Check(actions.Length>0,"group has no mapped casts");
                double clock=0;
                foreach(var aid in actions)
                {
                    Check(CrucibleActionCatalog.ClaimedActions.Contains(aid),$"{aid} leaks into generic dodge");
                    var helper=f.Spawn(9020,f.Center);
                    bool Pending()=>component.ActiveAOEs(0,f.Player).ToArray().Any(a=>a.ActorID==helper.InstanceID);
                    f.Time(clock); f.Cast(aid,4,f.Center,0,helper); f.Cast(aid,4,f.Center,73.5f,helper);
                    var aoe=component.ActiveAOEs(0,f.Player).ToArray().Single(a=>a.ActorID==helper.InstanceID);
                    Check(Math.Abs(aoe.Rotation.Deg-73.5)<0.01,$"{aid} kept old facing");
                    f.Time(clock+3.8); f.Engine.FinishCast(helper); Check(Pending(),$"{aid} ended before hit");
                    f.Effect(aid,helper); Check(!Pending(),$"{aid} survived damage");
                    f.Time(clock+6); f.Cast(aid,4,f.Center,0,helper); f.Engine.FinishCast(helper,cancelled:true); Check(!Pending(),$"{aid} survived cancel");
                    f.Time(clock+12); f.Cast(aid,4,f.Center,0,helper); f.Engine.Destroy(helper,true); Check(!Pending(),$"{aid} survived death");
                    helper=f.Spawn(9020,f.Center);
                    f.Time(clock+18); f.Cast(aid,4,f.Center,0,helper); f.Time(clock+25); Check(!Pending(),$"{aid} lacks expiry");
                    clock+=30;
                }
            });
        }
        run("High2 cast correction preserves a sub-metre ground origin correction", () =>
        {
            using var f = new Fight(14633, new(120, 0));
            f.Cast(49207, 4, f.Center, 0);
            f.Cast(49207, 4, f.Center + new WDir(0.6f, 0), 0);
            Check(f.Frame.Aoes.Single(a => a.Aoe.ActorID == f.Boss.InstanceID).Aoe.Origin.AlmostEqual(f.Center + new WDir(0.6f, 0), 0.01f), "packet origin correction was discarded");
        });
        foreach (var (name, action) in new (uint, uint)[] { (14631, 49194), (14633, 49207), (14642, 49240), (14657, 50688), (14660, 49296), (14663, 49315), (14665, 49335), (14670, 49361), (14693, 49451) })
            run($"High2 {action} survives normal cast completion until damage, then clears", () =>
            {
                using var f = new Fight(name, name == 14693 ? new(520, -420) : new(120, 0));
                f.Cast(action, 4, f.Center, 0);
                f.Time(3.75);
                f.Engine.FinishCast(f.Boss, action);
                Check(f.Frame.Aoes.Any(a => a.Aoe.ActorID == f.Boss.InstanceID), "normal cast completion removed an unresolved hit");
                f.Effect(action);
                Check(!f.Frame.Aoes.Any(a => a.Aoe.ActorID == f.Boss.InstanceID), "resolved cast was retained");
            });
        run("Lauda blade final timeline repeats cannot postpone the four-wave warning", () =>
        {
            using var f = new Fight();
            var blade = f.Spawn(19745, new(535, -427.5f));
            f.Engine.Dispatch(c => c.OnActorPlayActionTimelineEvent(blade, 4576));
            f.Time(2);
            f.Engine.Dispatch(c => c.OnActorPlayActionTimelineEvent(blade, 4576));
            Check(f.Frame.Aoes.Single().Aoe.Activation == f.Epoch.AddSeconds(8), "duplicate final cue delayed damage by two seconds");
        });
        run("Lauda blade normal finish keeps danger until the effect", () =>
        {
            using var f = new Fight();
            var blade = f.Spawn(19745, new(535, -427.5f));
            f.Cast(49483, 1, blade.Position, -90, blade);
            f.Time(0.75); f.Engine.FinishCast(blade);
            Check(f.Frame.Aoes.Any(a => a.Aoe.ActorID == blade.InstanceID), "blade vanished before its hit");
            f.Effect(49483, blade);
            Check(f.Frame.Aoes.Count == 0, "blade did not clear on hit");
        });
        run("Lauda torrent cancellation clears its landing constraint", () =>
        {
            using var f = new Fight();
            f.Cast(49453, 5, f.Center, 90);
            Check(f.Frame.Hints.ForbiddenZones.Count > 0, "missing torrent constraint");
            f.Engine.FinishCast(f.Boss, cancelled: true);
            Check(f.Frame.Hints.ForbiddenZones.Count == 0, "canceled torrent still requires a knockback landing");
        });
        run("Lauda fixed helpers release bait staging even with stale source positions", () =>
        {
            using var f = new Fight();
            var sword = f.Spawn(19747, new(512.9448f, -414.4982f));
            f.Engine.Dispatch(c => c.OnTethered(sword, new(1, f.Player.InstanceID)));
            f.Time(3.5);
            Check(f.Frame.Hints.GoalPoints.Count > 0, "bait never staged");
            var helper = f.Spawn(9020, new(514, -420));
            f.Cast(49464, 2, new(513.9696f, -420.0046f), 164.9155f, helper);
            Check(f.Frame.Hints.GoalPoints.Count == 0, "old aiming source pins player into locked damage line");
            Check(f.Frame.Hints.ForbiddenZones.Any(z => z.ExpiresAt != null), "locked line did not replace bait");
        });
        run("Lauda aiming cancellation releases its preparation goal", () =>
        {
            using var f = new Fight();
            f.Cast(49462, 3, f.Center, 0);
            f.Engine.FinishCast(f.Boss, cancelled: true);
            f.Time(12);
            Check(f.Frame.Hints.GoalPoints.Count == 0, "canceled summon started a late bait run");
        });
        run("Lauda fire knife cancellation removes the predicted proximity objective", () =>
        {
            using var f = new Fight();
            f.Cast(49468, 3, new(507, -422.5f), 0);
            f.Engine.FinishCast(f.Boss, cancelled: true);
            Check(f.Frame.Hints.GoalZones.Count == 0, "canceled knife left a future explosion goal");
        });
        run("Lauda fire knife source death removes its predicted proximity objective", () =>
        {
            using var f = new Fight();
            var helper = f.Spawn(9020, new(507, -422.5f));
            f.Cast(49468, 3, helper.Position, 0, helper);
            f.Engine.Destroy(helper, true);
            Check(f.Frame.Hints.GoalZones.Count == 0, "dead knife left an explosion goal");
        });
        run("Lauda shockwave preserves expiry when bind arrives after the mark", () =>
        {
            using var f = new Fight();
            f.Status(5341, 12); f.Status(5555, 1);
            f.Effect(49485, rotation: 180);
            f.Time(2);
            Check(f.Frame.Hints.GoalPoints.Count == 1, "late bind erased shockwave activation");
        });
        run("Lauda bind without a duration survives native scans without overflowing the script", () =>
        {
            using var f=new Fight();
            var bind=new ActorStatus { ID=5555, ExpireAt=DateTime.MaxValue };
            f.Engine.Dispatch(c=>c.OnStatusGain(f.Player,ref bind));
            f.Time(1); Check(f.Frame.Hints.ForcedMarchImminent,"indefinite bind vanished");
            f.Effect(49485); f.Status(5341,12);
            f.Engine.Dispatch(c=>c.OnStatusLose(f.Player,ref bind));
            f.Time(2); Check(f.Frame.Hints.GoalPoints.Count==1,"duration-bearing mark failed to recover after bind");
        });
        run("Lauda shockwave direction uses effect-time facing instead of an old scan", () =>
        {
            using var f = new Fight();
            f.Player.Rotation = default; f.Boss.Rotation = default;
            f.Status(5555, 1); f.Status(5341, 12);
            f.Effect(49485, rotation: 180); f.Time(2);
            var kb = f.Engine.Module!.FindComponent<CrucibleLaudaShockwave>()!.ActiveKnockbacks(0, f.Player)[0];
            Check(Math.Abs(kb.Direction.Normalized().Abs().Deg - 180) < 0.01, "shockwave guessed the wrong forward/backward direction");
        });
        run("Lauda final proximity cast recovers edge guidance after late script activation", () =>
        {
            using var f = new Fight();
            f.Cast(49480, 2, f.Center, 0);
            Check(f.Frame.Hints.GoalPoints.Count == 1, "missing blade-spawn history disabled final explosion guidance");
            Check(!f.Frame.Hints.ForbiddenZones.Any(z => z.Shape is SDCircle), "proximity damage was made an arena-wide forbidden circle");
        });
        run("Lauda empowered-blade death leads to a reachable outer tip before the explosion", () =>
        {
            using var f = new Fight();
            f.Cast(49472, 3, f.Center, 0);
            var first = f.Spawn(20152, new(520, -403)); var second = f.Spawn(20152, new(520, -437));
            f.Engine.Destroy(first, true); f.Engine.Destroy(second, true);
            f.Cast(49477, 15, f.Center, 0);
            f.Render(); var solve = f.Solve();
            Check(solve.IsSuccess && solve.TargetPosition is { } p && Math.Abs(p.Z + 420) > 21 && CrucibleLaudaFloor.Contains(WPos.FromVec3(p)), $"edge escape failed: {solve.Status}");
            f.Effect(49480); f.Render();
            Check(f.Snapshot.GoalAreas.Count == 0, "finished explosion retained edge hold");
        });
        run("Lauda late tethers cannot revive a locked wave; the next summon can", () =>
        {
            using var f = new Fight();
            var sword = f.Spawn(19747, new(514, -420));
            f.Engine.Dispatch(c => c.OnTethered(sword, new(1, f.Player.InstanceID)));
            f.Cast(50848, 2, f.Center, 180);
            f.Engine.Dispatch(c => c.OnTethered(sword, new(1, f.Player.InstanceID)));
            Check(!f.Engine.Module!.FindComponent<CrucibleLaudaBaits>()!.Preparing, "late tether reopened the locked wave");
            Check(!f.Frame.Aoes.Any(a => a.Component == nameof(CrucibleLaudaBaits)), "old moving bait overlays the locked line");
            f.Time(5); f.Cast(49462, 3, f.Center, 0);
            f.Engine.Dispatch(c => c.OnTethered(sword, new(1, f.Player.InstanceID)));
            Check(f.Engine.Module.FindComponent<CrucibleLaudaBaits>()!.Preparing, "next summon failed to reset wave");
        });
        run("Lauda landed knife survives placement-helper despawn and clears on explosion", () =>
        {
            using var f = new Fight();
            var helper = f.Spawn(9020, new(532.5083f, -417.1306f));
            f.Cast(49468, 3, helper.Position, 116.3003f, helper);
            f.Time(2.998); f.Effect(49468, helper); f.Engine.Destroy(helper, true);
            Check(f.Frame.Hints.GoalZones.Count == 1, "landed knife vanished with placement helper");
            var knife = f.Spawn(19744, helper.Position);
            f.Time(3.586); f.Cast(49469, 11, helper.Position, -44.7343f, knife);
            f.Time(14.537); f.Effect(49469, knife);
            Check(f.Frame.Hints.GoalZones.Count == 0, "knife goal survived resolved explosion");
        });
        run("Lauda ARR three locked lines permit continuous escape before the actual hit", () =>
        {
            // high2-220147: 1375005/1375095 casts -> 1376974/1377065 effects.
            using var f = new Fight();
            f.Player.Position = new(519.8901f, -440.7874f);
            var sword = f.Spawn(19747, new(512.9448f, -414.4982f));
            f.Engine.Dispatch(c => c.OnTethered(sword, new(1, f.Player.InstanceID)));
            var bossLine = f.Spawn(9020, f.Center);
            f.Cast(50848, 2, new(519.9817f, -420.0046f), -179.7363f, bossLine);
            f.Time(0.09);
            var left = f.Spawn(9020, new(513.9696f, -420.0046f));
            var right = f.Spawn(9020, new(525.9937f, -420.0046f));
            f.Cast(49464, 2, left.Position, 164.9155f, left);
            f.Cast(49464, 2, right.Position, -164.4102f, right);
            f.FollowUntil(1.96);
            Check(!f.Snapshot.DangerAreas.Any(a => a.Geometry.Contains(f.Player.Position.ToVec3())), "three-line escape missed the recorded first hit: " + string.Join("; ", f.Trace));
            Check(CrucibleLaudaFloor.Contains(f.Player.Position), "escape left the joined platform");
        });
        run("Lauda ARR 40y shockwave releases staging for the following 20y torrent", () =>
        {
            using var f = new Fight();
            f.Status(5555, 1); f.Status(5341, 12); f.Effect(49485);
            f.Time(11.762); f.Player.Position = new(520.2258f, -437.5219f);
            var helper = f.Spawn(9020, f.Center);
            f.Cast(49453, 7, new(559.9908f, -420.0046f), -90.0041f, helper);
            f.Time(12); f.Effect(49489);
            f.Player.Position = new(520.2258f, -397.5433f); // actual post-jump landing
            Check(f.Frame.Hints.GoalPoints.Count == 0, "old shockwave point survived the jump");
            f.FollowUntil(18.6);
            var kb = f.Engine.Module!.FindComponent<CrucibleLaudaTorrent>()!.ActiveKnockbacks(0, f.Player)[0];
            var landing = f.Player.Position + kb.Direction.ToDirection() * kb.Distance;
            Check(CrucibleLaudaFloor.Contains(landing), "did not return to a valid torrent start before the hit: " + string.Join("; ",f.Trace));
            f.Effect(49453, helper); f.Render();
            Check(f.Snapshot.DangerAreas.Count == 0, "torrent constraint survived its hit");
        });
        run("Lauda four waves preserve early warnings and clear each wave independently", () =>
        {
            using var f = new Fight();
            var blades = new List<Actor>();
            for (var i = 0; i < 4; ++i)
            {
                f.Time(i * 2);
                var blade = f.Spawn(19745, new(535, -432.5f + i * 5)); blades.Add(blade);
                f.Engine.Dispatch(c => c.OnActorPlayActionTimelineEvent(blade, 4562));
            }
            f.Time(7);
            f.Engine.Dispatch(c => c.OnActorPlayActionTimelineEvent(blades[0], 4576));
            f.Render();
            Check(f.Frame.Aoes.Count == 4 && f.Snapshot.DangerAreas.Count == 4, "a future wave was omitted from navigation");
            Check(f.Draw.Previews.Values.Any(p => p.OutlineOnly) && f.Draw.Previews.Values.Any(p => !p.OutlineOnly), "all four waves have the same emphasis");
            Check(f.SafeZone.NavigationOnly.Count == 4, "navigation duplicates obscure time-graded previews");
            for (var i = 0; i < 4; ++i)
            {
                f.Time(15 + i * 2); f.Effect(49483, blades[i]);
                Check(f.Frame.Aoes.Count == 3 - i, "one wave cleared or prolonged another");
            }
        });
        run("Lauda shockwave landing and facing arrows reach both view previews then clear", () =>
        {
            using var f = new Fight();
            f.Status(5555, 1); f.Status(5341, 12); f.Effect(49485);
            f.Time(10); f.Player.Position = new(520, -438); f.Player.Rotation = default;
            f.Render();
            Check(f.Draw.Lines.ContainsKey("facing") && f.Draw.Lines.Values.Any(l => Vector3.Distance(l.Start, l.End) > 39), "missing facing or 40y landing trajectory");
            Check(f.Draw.Previews.Values.Any(p => p.OutlineOnly && p.Safe && p.Geometry.Position.Z > -400), "safe opposite-tip landing missing");
            f.Player.Rotation = 90f.Degrees(); f.Render();
            Check(f.Draw.Previews.Values.Any(p => p.OutlineOnly && !p.Safe), "off-platform landing marked safe");
            f.Effect(49489); f.Render();
            Check(f.Draw.Lines.Count == 0 && f.Draw.Previews.Count == 0, "resolved direction or landing marker remained");
        });
        run("Lauda landing color checks the post-jump cleave", () =>
        {
            using var f = new Fight();
            f.Status(5555, 1); f.Status(5341, 12); f.Effect(49485);
            f.Time(10); f.Player.Position = new(520,-438); f.Player.Rotation = default;
            var helper = f.Spawn(9020,new(520,-402));
            f.Cast(49449, 4, helper.Position, 0, helper); f.Render();
            Check(f.Draw.Previews.Values.Any(p => p.OutlineOnly && !p.Safe && p.Geometry.Position.Z > -400), "unsafe next-mechanic landing was green");
        });
        run("Preview outlines honor opacity, remove on owner cleanup, and leave solver hazards", () =>
        {
            var registry = new AoeVisibilityRegistry([]);
            var geometry = new PromeRotation.Spatial.Geometry.CircleGeometry(Vector3.Zero, 6);
            registry.AddPreview("owner/circle",geometry,false,0,"source","source",true);
            registry.AddLine("owner/line",Vector3.Zero,Vector3.UnitZ,false,"source","source");
            var appearance = new AoeViewSettings { DangerFill = new(1,0,0,0.2f), DangerOutline = new(1,0,0,0.3f) };
            var style = registry.GetPreviews(DateTime.UtcNow).Single().Style(appearance);
            Check(style.FillColor!.Value.W == 0 && style.StrokeColor!.Value.W == 0.3f, "outline ignored configured transparency");
            registry.RemovePreviews("owner/");
            Check(!registry.GetLines().Any() && !registry.GetPreviews(DateTime.UtcNow).Any(), "owner cleanup left previews");
        });
        run("B35 knockback at the exact source never claims a safe zero-length landing", () =>
        {
            using var f=new Fight(14642,new(120,0));
            f.Cast(49238,4,f.Player.Position,0); f.Render();
            Check(f.Draw.Lines.Count==0 && f.Draw.Previews.Values.Any(p=>p.OutlineOnly&&!p.Safe),"unknown radial direction was shown as a safe landing");
        });
        run("B44 brand holds through normal finish and clears on its own effect only", () =>
        {
            using var f = new Fight(14688,new(120,0));
            f.Player.Statuses = [new() { ID=5536, ExpireAt=f.Epoch.AddSeconds(20) }];
            var helper = f.Spawn(9020,f.Center);
            f.Cast(49426,4,f.Center,0,helper); f.Time(3.8); f.Engine.FinishCast(helper);
            Check(f.Frame.Hints.GoalZones.Count>0,"brand released before real hit");
            f.Effect(49426); Check(f.Frame.Hints.GoalZones.Count>0,"unrelated helper cleared brand");
            f.Effect(49426,helper); Check(f.Frame.Hints.GoalZones.Count==0,"brand survived own effect");
        });
        run("B44 brand cancellation and source death leave no positioning constraint", () =>
        {
            using var f = new Fight(14688,new(120,0));
            f.Player.Statuses = [new() { ID=5537, ExpireAt=f.Epoch.AddSeconds(20) }];
            var helper=f.Spawn(9020,f.Center);
            f.Cast(49425,4,f.Center,180,helper); f.Engine.FinishCast(helper,cancelled:true);
            Check(f.Frame.Hints.GoalZones.Count==0,"cancelled brand remained");
            f.Cast(49425,4,f.Center,180,helper); f.Engine.Destroy(helper,true);
            Check(f.Frame.Hints.GoalZones.Count==0,"destroyed brand remained");
        });
        run("B44 gaze survives normal completion and ends on damage", () =>
        {
            using var f = new Fight(14688,new(120,0));
            f.Cast(49424,4,f.Center,0); f.Time(3.8); f.Engine.FinishCast(f.Boss);
            Check(f.Engine.Module!.FindComponent<CrucibleEyeGazes>()!.ActiveEyes(0,f.Player).Length==1,"gaze ended before damage");
            f.Effect(49424);
            Check(f.Engine.Module.FindComponent<CrucibleEyeGazes>()!.ActiveEyes(0,f.Player).Length==0,"resolved gaze survived");
        });
        run("B44 canceled attraction clears its landing preview", () =>
        {
            using var f = new Fight(14688,new(120,0));
            f.Spawn(19737,new(120,15)); f.Cast(49418,4,f.Center,0); f.Render();
            Check(f.Draw.Lines.Count>0,"attraction preview absent");
            f.Engine.FinishCast(f.Boss,cancelled:true); f.Render();
            Check(f.Draw.Lines.Count==0,"canceled attraction preview survived");
        });
        run("B39 dead placement helper clears unhit ice but retains the resolved ground footprint", () =>
        {
            using var f=new Fight(14663,new(120,0));
            var helper=f.Spawn(9020,f.Center);
            f.Cast(49320,3,f.Center,0,helper); f.Engine.Destroy(helper,true);
            Check(!f.Frame.Aoes.Any(a=>a.Aoe.ActorID==helper.InstanceID),"unhit ice survived dead caster");
            var next=f.Spawn(9020,f.Center);
            f.Cast(49320,3,f.Center,0,next); f.Time(3); f.Effect(49320,next); f.Engine.Destroy(next,true);
            Check(f.Frame.Aoes.Any(a=>a.Aoe.ActorID==next.InstanceID),"resolved ice lost its bridge to ground-object spawn");
            var pool=f.Spawn(2015490,f.Center); f.Time(4);
            Check(f.Frame.Aoes.Any(a=>a.Aoe.ActorID==pool.InstanceID),"ground object failed to take over");
        });
    }

    internal sealed class Fight : IDisposable
    {
        public readonly DateTime Epoch = DateTime.UtcNow.AddMinutes(1);
        public readonly WorldState World = new();
        public readonly ScriptSafeZoneApi SafeZone = new() { DangerMarginForTest = 0.5f };
        public readonly CrucibleEngine Engine;
        public readonly CrucibleOutput Output;
        public readonly ScriptDrawApi Draw = new();
        public readonly List<string> Trace = [];
        public readonly Actor Player, Boss;
        public readonly WPos Center;
        private ulong _next = 100;
        private uint _sequence;
        public Fight(uint name = 14693, WPos? center = null)
        {
            Center = center ?? new WPos(520, -420); World.CurrentTime = Epoch;
            Boss = new() { InstanceID = 1, OID = 19742, NameID = name, Position = Center, HPMP = (100,100), Type = ActorType.Enemy, IsTargetable = true, InCombat = true };
            Player = new() { InstanceID = 2, Position = Center, HPMP = (100,100), Type = ActorType.Player, IsTargetable = true };
            World.Actors.Add(Boss); World.Actors.Add(Player); World.Player = Player;
            Engine = new(World,32,45); Engine.SelectGroup();
            Output = new(SafeZone,Draw,SafeZone.Manager,_=>{},()=>0.5f);
        }
        public CrucibleFrame Frame => Engine.Frame();
        public SafeZoneSnapshot Snapshot => SafeZone.Manager.BuildSnapshot(World.CurrentTime);
        public Actor Spawn(uint oid, WPos position)
        {
            var actor = new Actor { InstanceID = _next++, OID = oid, Position = position, HPMP = (100,100), Type = ActorType.Enemy, IsTargetable = true };
            World.Actors.Add(actor); Engine.Dispatch(c=>c.OnActorCreated(actor)); return actor;
        }
        public void Cast(uint aid, double finish, WPos origin, float rotation, Actor? source = null)
            => Engine.Cast(source ?? Boss,new() {Action=new(aid),FinishAt=World.FutureTime(finish),LocXZ=origin,Rotation=rotation.Degrees(),TargetID=Player.InstanceID});
        public void Effect(uint aid, Actor? source = null, float rotation = 0)
            => Engine.Effect(source ?? Boss,new() {Action=new(aid),GlobalSequence=++_sequence,Rotation=rotation.Degrees(),Targets=[new(Player.InstanceID)]});
        public void Status(uint id, double duration)
        {
            var status=new ActorStatus {ID=id,ExpireAt=World.FutureTime(duration)};
            Engine.Dispatch(c=>c.OnStatusGain(Player,ref status));
        }
        public void Time(double seconds) { World.CurrentTime=Epoch.AddSeconds(seconds); Engine.Update(); }
        public void Render() => Output.Apply(Engine,Frame,0,true,true,true);
        public SafeZoneSolution Solve(SafeZoneSolution? previous=null) => new SafeZoneSolver().Solve(Snapshot,
            new(Player.Position.ToVec3(),0,6,false,false,0,true,Snapshot.Version),new(6,6,0,false,6,6),new(),World.CurrentTime,previous);
        public void FollowUntil(double seconds)
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            var (movement,move)=((object,PromeRotation.GreenMoveSystem.IGreenMoveSystem))typeof(LateDodgeRegression).GetMethod("CreateMovement",flags)!.Invoke(null,null)!;
            var dispatch=typeof(LateDodgeRegression).GetMethod("Dispatch",flags)!;
            var advance=move.GetType().GetMethod("Advance")!;
            SafeZoneSolution? solution=null;
            var start=(World.CurrentTime-Epoch).TotalSeconds;
            for(var tick=0;start+tick*0.01<seconds;++tick)
            {
                Time(start+tick*0.01);
                if(tick%10==0) { Render(); solution=Solve(solution); }
                if(tick>=5) dispatch.Invoke(null,[movement,solution,Snapshot,Player.Position.ToVec3(),World.CurrentTime]);
                if(tick%10==0) Trace.Add($"t={start+tick*0.01:F2} pos={Player.Position} solve={solution!.Status} running={move.IsRunning}");
                Player.Position=WPos.FromVec3((Vector3)advance.Invoke(move,[Player.Position.ToVec3(),0.01f])!);
            }
            Render();
        }
        public void Dispose() { Output.Clear(); SafeZone.Dispose(); }
    }
    private static void Check(bool ok,string message) { if(!ok) throw new InvalidOperationException(message); }
}
