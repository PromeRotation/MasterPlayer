using System.Numerics;
using MasterPlayer.Settings;
using System.Reflection;
using System.Text.Json;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;
using MasterPlayer.Spatial.SafeZoneReborn;

internal static class Orthos30Regression
{
    private static Lumina.GameData? data;
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);
    internal sealed record RecordedEvent(double T, string Kind, ulong Source, uint Action, float[] Pos, float Rotation, float Seconds, double Hit, float[] Loc, uint Oid, int State);
    private static RecordedEvent[] Recording(string file = "eo30-134415.json") => JsonSerializer.Deserialize<RecordedEvent[]>(
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/EurekaOrthos", file))).RootElement.GetProperty("events").GetRawText(),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    public static void Run(Action<string, Action> run)
    {
        run("EO30 native rectangles retain caster origin rotation and cast-end gap", () =>
        {
            using var f = new Fight();
            f.Cast(2, 31846, new(-312,0,-320), 5.7f, 0);
            var a = f.Aoes().Single();
            Check(a.Origin.AlmostEqual(new(-312,-320),.05f), "Self-cast used native empty target location.");
            f.At(5.7); f.Session.Execute(new ActorState.OpCastInfo(2,null));
            Check(f.Aoes().Length==1, "Natural cast end removed danger before effect.");
            f.Hit(2,31846); Check(f.Aoes().Length==0,"Effect did not clear source.");
        });
        run("EO30 ground circles preserve target location cancellation and expiry", () =>
        {
            using var f = new Fight();
            f.CastAt(2,31850,new(-280,0,-280),new(-306,0,-296),2.7f,0);
            Check(f.Aoes().Single().Origin.AlmostEqual(new(-306,-296),.01f),"Ground AOE used caster instead of target.");
            f.Session.Execute(new ActorState.OpCastInfo(2,null));
            Check(f.Aoes().Length==0,"Cancelled ground cast retained danger.");
            f.CastAt(2,31850,new(-280,0,-280),new(-306,0,-296),2.7f,0);
            f.At(6); Check(f.Aoes().Length==0,"Missing effect leaked stale danger.");
        });
        run("EO30 chase uses locked ground position stable heading and five hits", () =>
        {
            using var f = new Fight();
            f.CastAt(1,31847,new(-300,0,-320),new(-310,0,-300),4.7f,0);
            Check(f.Aoes().Length==1,"Missing icon suppressed first ground chase.");
            Check(f.Aoes()[0].Origin.AlmostEqual(new(-310,-300),.01f),"First chase not at locked cast location.");
            f.At(4.7);f.Session.Execute(new ActorState.OpCastInfo(1,null));
            Check(f.Aoes().Length==1,"First chase cleared at native cast end.");
            f.Effect(1,31847,new(-310,0,-300));
            var h = new AIHints(); foreach(var c in f.Module.Components)c.AddAIHints(0,f.Player,default,h);
            var signature=string.Join(";",h.TimedForbiddenZones.Select(z=>MechanicHintsConvert.SignatureOf(z.Shape)));
            f.Session.Execute(new ActorState.OpMove(100,new(-300,0,-300,1.5f)));
            h.Clear();foreach(var c in f.Module.Components)c.AddAIHints(0,f.Player,default,h);
            Check(signature==string.Join(";",h.TimedForbiddenZones.Select(z=>MechanicHintsConvert.SignatureOf(z.Shape))),"Turning retargeted a fixed avoidance area.");
            Check(h.TimedForbiddenZones.Single().Outward,"Chase allowed a route deeper into the following hit.");
            for(var i=0;i<4;++i){f.At(7+2*i);f.Effect(1,31848,new(-310+6*i,0,-300));}
            Check(f.Aoes().Length==0,"Five-hit chase retained danger.");
        });
        run("EO30 head navigation geometry contains actual collision disks",()=>
        {
            foreach(var rotation in new[]{0f,MathF.PI/2,-MathF.PI/2,.9504594f})
            {
                ShapeDistance sd=new SDCapsule(new(-300,-300),rotation.Radians(),1.5f,2.5f);
                var primitives=MechanicHintsConvert.ConvertForbiddenZone(sd,0,new(-300,0,-300),28,out _);
                for(var x=-320f;x<=-280;x+=.5f)for(var z=-320f;z<=-280;z+=.5f)
                    if(sd.Distance(new(x,z))<-.2f)
                        Check(primitives.Any(p=>p.Template.Resolve(p.Position,p.Rotation,null).Contains(new(x,0,z))),$"Capsule conversion omits {x},{z}, facing {rotation}.");
            }
        });
        run("EO30 ARR all 221 ground and rectangular hit footprints", VerifyRecording);
        run("EO30 timed navigation preserves expiry outward escape and cleared hints",()=>
        {
            var hints=new AIHints();
            hints.TimedForbiddenZones.Add((new SDCircle(new(-300,-300),6),Start.AddSeconds(1),Start.AddSeconds(3),9,true));
            using var manager=new SafeZoneManager(new(){EnableSafeZone=true});
            var now=Start;var registry=new SafeZoneRegistry(manager,()=>now);var differ=new MechanicNavAreaDiffer();
            var requests=MechanicNavAreaDiffer.TimedRequests(hints,0,new(-300,0,-300),28).ToArray();
            differ.Sync(registry,manager,requests,[],.5f,null);
            var window=manager.BuildSnapshot(now).DangerAreas.Single().DangerWindows.Single();
            Check(window.DangerFrom==Start.AddSeconds(1)&&window.DangerUntil==Start.AddSeconds(3)&&window.RequireOutwardExit,"Explicit occupancy was reduced to a one-shot hit.");
            now=Start.AddSeconds(2);differ.Sync(registry,manager,requests,[],.5f,null);
            Check(manager.BuildSnapshot(now).DangerAreas.Single().DangerWindows.Single()==window,"Identical sync moved the clock.");
            var changed=requests[0] with {ExplicitWindow=window with {DangerUntil=Start.AddSeconds(5)}};
            differ.Sync(registry,manager,[changed],[],.5f,null);
            Check(manager.BuildSnapshot(now).DangerAreas.Single().DangerWindows.Single().DangerUntil==Start.AddSeconds(5),"Expiry change reused obsolete timing.");
            hints.Clear();Check(hints.TimedForbiddenZones.Count==0,"Frame hints leaked.");
            differ.Sync(registry,manager,[],[],.5f,null);Check(manager.BuildSnapshot(now).DangerAreas.Count==0,"Removed timed zone retained danger.");
        });
        run("EO30 refuge dispatch rejects a terminal point hit just after arrival", () =>
        {
            var pair = ((object, IGreenMoveSystem))typeof(LateDodgeRegression).GetMethod("CreateMovement", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
            using var movement = (SafeZoneRebornMovementSystem)pair.Item1;
            movement.GetType().GetField("_setting", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(movement,
                new SafeZoneSetting { RuntimePreferSafeRefuge = true });
            var target = new Vector3(3, 0, 0);
            var danger = new DangerAreaSnapshot(default, new CircleTemplate(1).Resolve(target, 0, null),
                [new DangerWindow(Start.AddSeconds(1.2), Start.AddSeconds(2)) { HasExplicitTiming = true, HasMotionEnvelope = true }]);
            var snapshot = new SafeZoneSnapshot(1, Start, new CircleTemplate(20).Resolve(Vector3.Zero, 0, null), [], [danger], []);
            var solution = new SafeZoneSolution(SolutionStatus.Success, 1, Start, Start, target,
                [TimedWaypoint.Pass(Vector3.Zero, Start), TimedWaypoint.Pass(target, Start.AddSeconds(.5))]);
            var validate = movement.GetType().GetMethod("IsTemporalPhaseSafe", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var player = new PlayerState(Vector3.Zero, 0, 6, false, false, 0, true, 1);
            Check(!(bool)validate.Invoke(movement, [solution, player, snapshot, Start, null, 0d])!,
                "Arrival alone must not authorize a refuge inside the next hit.");
            var cleared = new SafeZoneSnapshot(2, Start, snapshot.ArenaBoundary, [], [], []);
            Check((bool)validate.Invoke(movement, [solution, player, cleared, Start, null, 0d])!,
                "A safe terminal point must still be executable.");
        });
        run("EO30 moving envelope permits only initial exit and keeps the next hit deadline", () =>
        {
            var origin = new Vector3(-300, 0, -300);
            var current = new DangerAreaSnapshot(default, new CircleTemplate(2.5f).Resolve(origin, 0, null),
                [new DangerWindow(Start.AddSeconds(-.2), Start.AddSeconds(1)) { HasExplicitTiming = true, HasMotionEnvelope = true }]);
            var snapshot = new SafeZoneSnapshot(1, Start, new CircleTemplate(10).Resolve(origin, 0, null), [], [current], []);
            var options = new SolveOptions { PreferSafeRefuge = true };
            var map = RasterizedMap.Build(snapshot, origin, options, Start, 6);
            var (x, z) = map.WorldToCell(origin);
            Check(map.RequiresEscapeAt(z * map.Columns + x), "An occupied motion envelope needs an immediate exit.");
            var future = current with { DangerWindows = [new DangerWindow(Start.AddSeconds(1), Start.AddSeconds(2)) { HasExplicitTiming = true, HasMotionEnvelope = true }] };
            var next = RasterizedMap.Build(new(2, Start, snapshot.ArenaBoundary, [], [future], []), origin, options, Start, 6);
            (x,z) = next.WorldToCell(origin);
            Check(!next.RequiresEscapeAt(z * next.Columns + x) && next.DangerIntervalsAt(z * next.Columns + x).Count > 0,
                "A future envelope retains its deadline; it cannot become an escape exemption.");
        });
        run("EO30 ARR BLM closed loop 100ms worker",()=>Simulate(.1));
        run("EO30 ARR BLM closed loop 300ms worker",()=>Simulate(.3));
        run("EO30 ARR BLM closed loop west start 300ms",()=>Simulate(.3,new(-316,0,-300)));
        run("EO30 ARR BLM closed loop east start 300ms",()=>Simulate(.3,new(-284,0,-300)));
        run("EO30 ARR BLM closed loop centre start 300ms",()=>Simulate(.3,new(-300,0,-300)));
        run("EO30 new ARR BLM movement stability 300ms",()=>Simulate(.3, file:"eo30-203928.json"));
        run("EO30 new ARR BLM movement stability 100ms",()=>Simulate(.1, file:"eo30-203928.json"));
        run("EO30 new ARR BLM centre start 300ms",()=>Simulate(.3,new(-300,0,-300),"eo30-203928.json"));
        run("EO30 new ARR BLM west start 300ms",()=>Simulate(.3,new(-316,0,-300),"eo30-203928.json"));
        run("EO30 new ARR BLM east start 300ms",()=>Simulate(.3,new(-284,0,-300),"eo30-203928.json"));
    }
    private static void Apply(Fight f, RecordedEvent e)
    {
        var p = e.Pos is {Length:3}?new Vector3(e.Pos[0],0,e.Pos[2]):Vector3.Zero;
        switch(e.Kind)
        {
            case "spawn": if(e.Source!=1 && f.Session.World.Actors.Find(e.Source)==null)f.Spawn(e.Source,e.Oid,p);goto case "move";
            case "move": if(f.Session.World.Actors.Find(e.Source)!=null)f.Session.Execute(new ActorState.OpMove(e.Source,new(p,e.Rotation)));break;
            case "state":f.Session.Execute(new ActorState.OpModelState(e.Source,new(0,(byte)e.State,0)));break;
            case "cast": f.CastAt(e.Source,e.Action,p,new(e.Loc[0],0,e.Loc[2]),e.Seconds,e.Rotation);break;
            case "effect":f.Effect(e.Source,e.Action,p);break;
        }
    }
    private static void VerifyRecording()
    {
        using var f=new Fight();var events=Recording();var ended=new HashSet<RecordedEvent>();var covered=0;
        foreach(var e in events)
        {
            f.At(e.T);
            foreach(var c in events.Where(c=>c.Kind=="cast"&&c.T+c.Seconds<=e.T))
                if(ended.Add(c)&&f.Session.World.Actors.Find(c.Source)!=null)f.Session.Execute(new ActorState.OpCastInfo(c.Source,null));
            if(e.Kind=="effect")
            {
                var a=f.Aoes().Where(a=>a.ActorID==e.Source).ToArray();
                Check(a.Length==1,$"Uncovered action {e.Action} at {e.T:F3}s.");
                if(e.Action!=31848)Check(a[0].Origin.AlmostEqual(new(e.Pos[0],e.Pos[2]),.08f),$"Wrong footprint {e.Action} at {e.T:F3}s.");
                ++covered;
            }
            Apply(f,e);
        }
        Check(covered==221,"Missing recorded hit footprints.");
        Console.WriteLine($"  ARR coverage: {covered}/221 real spatial effects; recorded head movement/state events replayed");
    }
    private static void Simulate(double workerDelay, Vector3? initial = null, string file = "eo30-134415.json")
    {
        var events = Recording(file);
        var casts = events.Where(e => e.Kind == "cast" && e.Action is 31844 or 31846 or 31850 or 31847).ToArray();
        using var f = new Fight();
        using var manager = new SafeZoneManager(new() { EnableSafeZone = true, EnableAutoMovement = true });
        var now = Start;
        var registry = new SafeZoneRegistry(manager, () => now);
        var differ = new MechanicNavAreaDiffer();
        registry.SetArenaBoundary(new WorldPolygonGeometry([new(-319.5f,0,-319.5f),new(-280.5f,0,-319.5f),new(-280.5f,0,-280.5f),new(-319.5f,0,-280.5f)]));
        var pair = ((object, IGreenMoveSystem))typeof(LateDodgeRegression).GetMethod("CreateMovement", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
        using var movement = (SafeZoneRebornMovementSystem)pair.Item1;
        var advance = pair.Item2.GetType().GetMethod("Advance")!;
        var dispatch = movement.GetType().GetMethod("Dispatch", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var latestField = movement.GetType().GetField("_latestSolution", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var usable = movement.GetType().GetMethod("IsSolutionUsableForDispatch", BindingFlags.Static | BindingFlags.NonPublic)!;
        var setting = new SafeZoneSetting();
        using var activation = new SafeZoneActivation(setting,()=>now);
        movement.GetType().GetField("_setting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(movement,setting);
        movement.GetType().GetField("_manager",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(movement,manager);
        var stationary=0d; var headHits=0; var headChecks=0;
        var bursts = new List<(double Start, double Until, float Distance)>();
        var wasMoving = false; var burstStart = 0d; var burstDistance = 0f;
        using var trace = Environment.GetEnvironmentVariable("EO30_TRACE") is {Length:>0} traceFile ? new StreamWriter(traceFile) : null;
        trace?.WriteLine("t,x,z,moving,phase,targetX,targetZ,cast,heads");
        var castRemaining=0f;var completed=0;var interrupted=0;var longestStand=0d;var stand=0d;
        var chaseRemaining=0;var chaseOrigin=Vector3.Zero;
        var position = initial ?? new Vector3(-300, 0, -284);
        var ended = new HashSet<RecordedEvent>();
        var hints = new AIHints();
        SafeZoneSolution? latest = null, pending = null;
        var cursor = 0; var hits = 0; var collisions = 0; var ready = 0d;
        for (var tick = 0; tick * .05 < events[^1].T + 2; ++tick)
        {
            var t = tick * .05;
            now = Start.AddSeconds(t); f.At(t);
            foreach (var c in casts)
                if (t >= c.T + c.Seconds && ended.Add(c)) f.Session.Execute(new ActorState.OpCastInfo(c.Source, null));
            while (cursor < events.Length && events[cursor].T <= t)
            {
                var e = events[cursor++];
                // Re-bait the chasing cast on this simulated player; subsequent hits
                // advance six yalms toward this player, not the recording's path.
                if(e.Kind=="cast" && e.Action==31847)
                {
                    e=e with {Loc=[position.X,0,position.Z]};
                    chaseOrigin=position;chaseRemaining=5;
                }
                if(e.Kind=="effect" && e.Action is 31847 or 31848)
                {
                    if(chaseRemaining<5)
                    {
                        var d=position-chaseOrigin;var length=d.Length();
                        chaseOrigin=length>6?chaseOrigin+6*d/length:position;
                    }
                    e=e with {Pos=[chaseOrigin.X,0,chaseOrigin.Z]};--chaseRemaining;
                }
                if (e.Kind == "effect")
                {
                    AOEShape shape = e.Action is 31844 or 31846 ? new AOEShapeRect(40,8) : new AOEShapeCircle(6);
                    // Use the raw BMR point-in-AOE predicate, independent of inflated navigation hints.
                    if (shape.Check(new(position.X, position.Z), new(e.Pos[0], e.Pos[2]), e.Rotation.Radians()))
                    {
                        ++collisions;
                        Console.WriteLine($"  collision source={e.Source} origin={string.Join(",",e.Pos)} rot={e.Rotation} {e.Action} at {e.T:F3}s pos={position} distance={shape.Distance(new(e.Pos[0],e.Pos[2]),e.Rotation.Radians()).Distance(new(position.X,position.Z)):F4} solution={latest?.Status} target={latest?.TargetPosition}");
                    }
                    ++hits;
                }
                Apply(f, e);
            }
            foreach(var head in f.Module.Enemies(0x3D9B))
            {
                if(head.ModelState.AnimState1!=1)continue;
                ++headChecks;
                if(Vector2.Distance(new(position.X,position.Z),new(head.Position.X,head.Position.Z))<2.15f)
                {
                    if(headHits++<1)Console.WriteLine($"  head collision t={t:F2} pos={position} head={head.Position} solution={latest?.Status} running={pair.Item2.IsRunning} phase={movement.GetType().GetField("_phase",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(movement)} unsafe={manager.BuildSnapshot(now).DangerAreas.Count(a=>a.Geometry.Contains(position)&&a.DangerWindows.Any(w=>w.IsActiveAt(now)))} path={string.Join(";",latest?.ExecutablePath.Select(p=>$"{p.Position}/{p.PlannedArrivalTime:HH:mm:ss.fff}")??[])}");
                }
            }
            f.Session.Execute(new ActorState.OpMove(100, new(position, 0)));
            activation.Update(false,false,true,true);
            hints.Clear();
            foreach (var c in f.Module.Components) c.AddAIHints(0, f.Player, default, hints);
            setting.RuntimePreferSafeRefuge=hints.PreferSafeRefuge;
            var requests = hints.ForbiddenZones.Select((z,i)=>
            {
                var prims=MechanicHintsConvert.ConvertForbiddenZone(z.shapeDistance,0,new(-300,0,-300),28,out var kind);
                var goal=kind==MechanicNavZoneKind.ComplementSkipped&&MechanicHintsConvert.TryConvertComplement(z.shapeDistance,0,out var g);
                if(goal && MechanicHintsConvert.TryConvertComplement(z.shapeDistance,0,out var converted))prims=converted;
                return new MechanicNavZoneRequest($"{z.Source}/{z.activation.Ticks}/{i}",prims,(float)Math.Max(0,(z.activation-now).TotalSeconds),z.activation<=now,MechanicHintsConvert.SignatureOf(z.shapeDistance)+"|"+z.activation.Ticks){IsGoal=goal};
            }).Concat(MechanicNavAreaDiffer.TimedRequests(hints,0,new(-300,0,-300),28)).ToArray();
            var obstacles=hints.TemporaryObstacles.SelectMany((sd,i)=>MechanicHintsConvert.ConvertForbiddenZone(sd,0,new(-300,0,-300),28,out _).Select((p,j)=>($"head/{i}/{j}",p))).ToArray();
            differ.Sync(registry, manager, requests, obstacles, .5f, null);
            var snapshot = manager.BuildSnapshot(now);
            if (pending != null && t >= ready)
            {
                if ((bool)usable.Invoke(null, [pending, snapshot])!) latest = pending;
                pending = null;
            }
            // Start a new 2.8s BLM cast only while stopped. Movement before
            // its final 200ms interrupts it; slidecasts still complete.
            if(castRemaining<=0 && !pair.Item2.IsRunning)castRemaining=2.8f;
            var player = new PlayerState(position, 0, 6, pair.Item2.IsRunning, castRemaining>0, castRemaining, true, snapshot.Version);
            if (pending == null && snapshot.DangerAreas.Count > 0)
            {
                var options = (SolveOptions)movement.GetType().GetMethod("CaptureSolveOptions", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(movement, null)!;
                pending = new SafeZoneSolver().Solve(snapshot, player, new(6, 6, 0, false, 6, 6), options, now, latest);
                ready = t + workerDelay;
            }
            latestField.SetValue(movement, latest);
            dispatch.Invoke(movement, [(bool)usable.Invoke(null, [latest, snapshot])! ? latest : null, player, snapshot, now]);
            latest = movement.LatestSolution;
            var moving=pair.Item2.IsRunning;
            if(moving&&!wasMoving){burstStart=t;burstDistance=0;}
            if(!moving&&wasMoving)bursts.Add((burstStart,t,burstDistance));
            wasMoving=moving;
            trace?.WriteLine(FormattableString.Invariant($"{t:F2},{position.X:F3},{position.Z:F3},{moving},{movement.PhaseText},{latest?.TargetPosition?.X:F3},{latest?.TargetPosition?.Z:F3},{castRemaining:F2},{f.Module.Enemies(0x3D9B).Count(h=>h.ModelState.AnimState1==1)}"));
            if(!pair.Item2.IsRunning){stationary+=.05;stand+=.05;longestStand=Math.Max(longestStand,stand);}
            else{stand=0;if(castRemaining>.2f){++interrupted;castRemaining=0;}}
            if(castRemaining>0){castRemaining-=.05f;if(castRemaining<=0)++completed;}
            var nextPosition = (Vector3)advance.Invoke(pair.Item2, [position, .05f])!;
            if(moving)burstDistance+=Vector3.Distance(position,nextPosition);
            position=nextPosition;
            Check(snapshot.ArenaBoundary!.Contains(position), $"Left arena at {t:F2}s.");
        }
        Console.WriteLine($"  stationary={stationary:F1}s; ARR closed loop: {hits} effects; {collisions} avoidable hits; {workerDelay * 1000:F0}ms worker delay");
        Console.WriteLine($"  heads: {headChecks} active samples, {headHits} overlaps");
        Console.WriteLine($"  BLM 2.8s cast model: {completed} completed; {interrupted} interrupted; longest stationary window {longestStand:F1}s");
        trace?.Dispose();
        Console.WriteLine($"  movement: {bursts.Count} bursts; {bursts.Count(b=>b.Distance<1.5f)} short steps (<1.5y); distance={bursts.Sum(b=>b.Distance):F1}y");
        foreach(var cast in casts.Where(c=>c.Action==31847))
            Console.WriteLine($"  chase {cast.T:F2}: {bursts.Count(b=>b.Start>=cast.T&&b.Start<cast.T+14)} starts; {bursts.Where(b=>b.Start>=cast.T&&b.Start<cast.T+14).Sum(b=>b.Distance):F1}y");
        if(file=="eo30-203928.json" && initial==null && workerDelay==.3)
            Check(completed>=78 && interrupted<=30 && bursts.Count(b=>b.Distance<1.5f)<=10,
                "New ARR regressed the measured casting/microstep improvement.");
        Check(completed>= (file=="eo30-134415.json"?70:65) && longestStand>=8,"Safe windows did not permit enough full BLM casts.");
        Check(hits == events.Count(e=>e.Kind=="effect") && collisions == 0 && headHits==0, "Recording still takes avoidable hits.");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class Fight : IDisposable
    {
        public readonly MechanicSession Session;
        public readonly BossModule Module;
        public Actor Player => Session.World.Party[0]!;
        public Fight()
        {
            if (data == null && !MechanicRuntimeEnvironment.IsInitialized)
            {
                data = new(Environment.GetEnvironmentVariable("FFXIV_SQPACK") ?? throw new Exception("Set FFXIV_SQPACK."),
                    new Lumina.LuminaOptions { DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified });
                MechanicRuntimeEnvironment.Initialize(data, DispatchProxy.Create<Dalamud.Plugin.Services.IPluginLog, Orthos30Log>());
            }
            Session = new("EO30 ARR regression");
            Session.BeginFrame(Start);
            Session.Execute(new ActorState.OpCreate(100, 0, 0, 0, "Player", 0, ActorType.Player, Class.BLM, 90,
                new(-300, 0, -300, 0), .5f, new(100000, 100000, 0, 10000, 10000), true, true, 0, 0, 0));
            Session.Execute(new PartyState.OpModify(0, new(100, 100, false, "Player")));
            Spawn(1, 0x3D9A, new(-300, 0, -300));
            Session.Execute(new ActorState.OpCombat(1, true));
            Session.Update();
            Module = Session.BossModules.ActiveModule ?? throw new Exception("EO30 module not active.");
        }
        public void Spawn(ulong id, uint oid, Vector3 pos) => Session.Execute(new ActorState.OpCreate(id, oid, (int)id * 2, 0,
            "Enemy", 0, ActorType.Enemy, Class.None, 90, new(pos, 0), 1.5f,
            new(100000, 100000, 0, 10000, 10000), true, false, 0, 0, 0));
        public void Cast(ulong id,uint action,Vector3 pos,float duration,float rotation=0)=>CastAt(id,action,pos,Vector3.Zero,duration,rotation);
        public void CastAt(ulong id,uint action,Vector3 pos,Vector3 location,float duration,float rotation)
        {
            if(Session.World.Actors.Find(id)==null)Spawn(id,0x233C,pos);
            else Session.Execute(new ActorState.OpMove(id,new(pos,0)));
            Session.Execute(new ActorState.OpCastInfo(id,new(){Action=new(ActionType.Spell,action),TargetID=action is 31844 or 31846?id:0xE0000000,
                Location=location,Rotation=rotation.Radians(),TotalTime=duration}));Session.Update();
        }
        public void Effect(ulong id,uint action,Vector3 pos)=>Session.Execute(new ActorState.OpCastEvent(id,new(new(ActionType.Spell,action),0xE0000000,0,0,pos,1,0,default)));
        public void At(double time) { Session.BeginFrame(Start.AddSeconds(time)); Session.Update(); }
        public void Hit(ulong id, uint action) => Session.Execute(new ActorState.OpCastEvent(id, new(new(ActionType.Spell, action), id, 0, 0, default, 1, 0, default)));
        public GenericAOEs.AOEInstance[] Aoes() => Module.Components.OfType<GenericAOEs>().SelectMany(c => c.ActiveAOEs(0, Player).ToArray()).ToArray();
        public void Dispose() => Session.Dispose();
    }
}
public class Orthos30Log : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => null;
}
