using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;

internal static class BlundervilleBumperPlannerRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    public static void Run(Action<string, Action> run)
    {
        run("Blunderville bumper planner does not add a scan for sub-microsecond departure rounding", () =>
        {
            var player = new Vector3(-210.25f, 17.47f, -158);
            var planner = new BlundervilleBumperPlanner();
            ((List<BlundervilleBumperPlanner.Step>)Get(planner, "_path")!).AddRange([new(player, 1), new(player - Vector3.UnitZ * 2, 0)]);
            Set(planner, "_origin", player); Set(planner, "_waitUntil", Epoch.AddSeconds(1).AddTicks(1));
            var decision = planner.Apply(new(player, false, false, null, "test"), State(), new(_ => { }), player, Epoch.AddSeconds(1));
            Check(!decision.Hold && decision.ContinuousPath?.Last() == player - Vector3.UnitZ * 2,
                "One rounded clock tick delayed the complete departure by another policy scan.");
        });
        foreach (var completed in new[] { false, true })
            run($"Blunderville bumper planner repairs an unsafe prefix before extending its tail completed={completed}", () =>
            {
                var now = Epoch.AddSeconds(.2); var origin = new Vector3(-207.5f, 17.47f, -183);
                var player = origin - Vector3.UnitZ * .3f; var requests = 0;
                var planner = new BlundervilleBumperPlanner(_ =>
                { ++requests; return new TaskCompletionSource<BlundervilleBumperPlanner.Step[]>().Task; });
                var route = Seed(planner, origin, [new(origin - Vector3.UnitZ * 5, 0, 3, true),
                    new(origin - Vector3.UnitZ * 10, 0, 3, true), new(origin - Vector3.UnitZ * 15, 0, 3, true)]);
                var state = State(); state.SynchronizeStatuses([new(3693, 50, Epoch.AddSeconds(30))], now);
                var encounter = WarmIceEncounter(now, -214.5f);
                var charges = (Dictionary<ulong, BlundervilleHazard>)typeof(BlundervilleEncounter).GetField("_charges", Private)!.GetValue(encounter)!;
                charges[999] = new("rhythm/future/34717/0", new CircleGeometry(origin - Vector3.UnitZ * 8, .2f),
                    now.AddSeconds(2.3), now.AddSeconds(3.3), now);
                var old = new TaskCompletionSource<BlundervilleBumperPlanner.Step[]>();
                BlundervilleBumperPlanner.Step[] tail = [new(origin - Vector3.UnitZ * 20, 0, 3, true),
                    new(origin - Vector3.UnitZ * 25, 0, 3, true)];
                Set(planner, "_pending", old.Task); Set(planner, "_searchInFlight", old.Task);
                Set(planner, "_requestedAt", Epoch); Set(planner, "_suffixAnchor", 2); Set(planner, "_requestedRun", route);
                if (completed) old.SetResult(tail);
                var normal = new BlundervilleRaceDecision(player, false, false, null, "test");
                var execution = new DirectTraversalExecution(route, 0, true, false, true);
                var decision = planner.Apply(normal, state, encounter, player, now, execution);
                Check(!decision.Hold && decision.ContinuousPath!.SequenceEqual(route) && Get(planner, "_proposal") == null,
                    "An extension was published through an unsafe retained prefix.");
                if (!completed)
                {
                    Check(requests == 0 && Get(planner, "_pending") == null,
                        "An irrelevant tail worker blocked prefix repair or spawned another simultaneous worker.");
                    old.SetResult(tail);
                    planner.Apply(normal, state, encounter, player, now.AddSeconds(.1), execution);
                }
                Check(requests == 1 && (int)Get(planner, "_suffixAnchor")! == 0,
                    $"Repair did not move back to the acquired slide: requests={requests}, anchor={Get(planner, "_suffixAnchor")}, pending={Get(planner, "_pending") != null}.");
            });
        run("Blunderville bumper planner keeps a suffix worker through its publication deadline", () =>
        {
            var player = new Vector3(-210.25f, 17.47f, -158);
            var planner = new BlundervilleBumperPlanner(); var state = State();
            var route = Seed(planner, player, [new(player - Vector3.UnitZ * 2, 0),
                new(player - Vector3.UnitZ * 4, 0), new(player - Vector3.UnitZ * 6, 0)]);
            var completion = new TaskCompletionSource<BlundervilleBumperPlanner.Step[]>();
            Set(planner, "_pending", completion.Task); Set(planner, "_requestedAt", Epoch);
            Set(planner, "_suffixAnchor", 0); Set(planner, "_requestedRun", route); Set(planner, "_suffixRequired", true);
            var normal = new BlundervilleRaceDecision(player, false, false, null, "test");
            var execution = new DirectTraversalExecution(route, 0, true, false, false);
            planner.Apply(normal, state, new(_ => { }), player, Epoch.AddSeconds(.6), execution);
            Check(ReferenceEquals(Get(planner, "_pending"), completion.Task),
                "An unfinished suffix expired before its documented one-second publication window.");
            completion.SetResult([new(player - Vector3.UnitZ * 4, 0), new(player - Vector3.UnitZ * 8, 0)]);
            var decision = planner.Apply(normal, state, new(_ => { }), player, Epoch.AddSeconds(.8), execution);
            Check(decision.ContinuousPath?.Last() == player - Vector3.UnitZ * 8,
                "A safe completed suffix could not publish inside its deadline.");
        });
        run("Blunderville bumper planner extends a safe queue without rewriting its committed approach", () =>
        {
            var now = Epoch.AddSeconds(.2); var origin = new Vector3(-207.5f, 17.47f, -183);
            var requested = 0;
            var planner = new BlundervilleBumperPlanner(_ =>
            { ++requested; return new TaskCompletionSource<BlundervilleBumperPlanner.Step[]>().Task; });
            var state = State(); state.SynchronizeStatuses([new(3693, 50, Epoch.AddSeconds(30))], now);
            var route = Seed(planner, origin, [new(origin - Vector3.UnitZ * 5, 0, 3, true),
                new(origin - Vector3.UnitZ * 10, 0, 3, true), new(origin - Vector3.UnitZ * 15, 0, 3, true)]);
            var encounter = WarmIceEncounter(now, -214.5f);
            var player = origin - Vector3.UnitZ * .3f;
            planner.Apply(new(player, false, false, null, "test"), state, encounter, player, now,
                new(route, 0, true, false, true));
            Check(requested == 0, "A long safe queue kept starting unnecessary background searches.");
            player = origin - Vector3.UnitZ * 4.5f;
            var decision = planner.Apply(new(player, false, false, null, "test"), state, WarmIceEncounter(now.AddSeconds(1.4), -214.5f), player, now.AddSeconds(1.4),
                new(route, 0, true, false, true));
            Check(!decision.Hold && decision.ContinuousPath!.SequenceEqual(route) && requested == 1
                && (int)Get(planner, "_suffixAnchor")! == route.Length - 1,
                $"A safe continuation rewrote existing future turns instead of extending the tail: hold={decision.Hold}, requested={requested}, anchor={Get(planner, "_suffixAnchor")}, path={decision.ContinuousPath?.Count}.");
        });
        run("Blunderville bumper planner rejects a safe pocket without a continuous exit", () =>
        {
            var origin = new Vector3(-207, 17, -190);
            var floor = new RectangleGeometry(new(-207, 17, -195), 0, 1, 16);
            var result = Search(floor, [], origin, new(origin, 0, 3, true, origin, origin, LandingPosition: origin));
            Check(result.Length == 0, "A short-lived safe endpoint was published without a route to the dry platform.");
        });
        run("Blunderville bumper planner repairs the approach when its safe queue has no continuation", () =>
        {
            var now = Epoch.AddSeconds(.2); var origin = new Vector3(-207.5f, 17.47f, -183);
            var player = origin - Vector3.UnitZ * 3.3f; var requested = 0;
            var planner = new BlundervilleBumperPlanner(_ =>
            { ++requested; return new TaskCompletionSource<BlundervilleBumperPlanner.Step[]>().Task; });
            var state = State(); state.SynchronizeStatuses([new(3693, 50, Epoch.AddSeconds(30))], now);
            var route = Seed(planner, origin, [new(origin - Vector3.UnitZ * 5, 0, 3, true),
                new(origin - Vector3.UnitZ * 10, 0, 3, true), new(origin - Vector3.UnitZ * 15, 0, 3, true)]);
            Set(planner, "_pending", Task.FromResult(Array.Empty<BlundervilleBumperPlanner.Step>()));
            Set(planner, "_requestedAt", Epoch); Set(planner, "_suffixAnchor", route.Length - 1); Set(planner, "_requestedRun", route);
            var decision = planner.Apply(new(player, false, false, null, "test"), state, WarmIceEncounter(now, -214.5f), player, now,
                new(route, 0, true, false, true));
            Check(!decision.Hold && decision.ContinuousPath!.SequenceEqual(route) && requested == 1
                && (int)Get(planner, "_suffixAnchor")! == 0 && (bool)Get(planner, "_suffixRequired")!,
                "A blocked tail kept consuming the old approach instead of repairing its unacquired future rays.");
        });
        run("Blunderville bumper planner validates the remaining dry target without restarting a slide", () =>
        {
            var planner=new BlundervilleBumperPlanner(); var encounter=new BlundervilleEncounter(_=>{});
            var origin=new Vector3(-204,17.47f,-201.7f); var target=new Vector3(-204,17.47f,-206.2f);
            var landing=new Vector3(-204,17.47f,-206.7f);
            var route=Seed(planner,origin,[new(target,0,3,true,landing+Vector3.UnitZ*.12f,landing-Vector3.UnitZ*.1f,
                -.1,.15,LandingRadius:.1f,LandingPosition:landing),new(new(-204,17.47f,-209.2f),0)]);
            var charges=(Dictionary<ulong,BlundervilleHazard>)typeof(BlundervilleEncounter).GetField("_charges",Private)!.GetValue(encounter)!;
            charges[999]=new("dry-test",new CircleGeometry(new(-204,17.47f,-210.3f),.2f),Epoch,Epoch.AddSeconds(5),Epoch);
            var player=new Vector3(-204,17.47f,-205.99f);
            var decision=planner.Apply(new(player,false,false,null,"test"),State(),encounter,player,Epoch.AddSeconds(.2),
                new(route,0,true,false,false));
            Check(!decision.Hold && decision.ContinuousPath!.SequenceEqual(route),
                "The dry remainder was treated as a new five-metre slide into an irrelevant hazard.");
        });
        run("Blunderville bumper planner reserves the whole range of short and long slide landings",()=>
        {
            var start=new Vector3(-207,17,-190); var target=start-Vector3.UnitZ*4.5f;
            var pulse=new BlundervilleHazard("rhythm/settle/34717/0",new CircleGeometry(start-Vector3.UnitZ*4.88f,.015f),
                Epoch.AddSeconds(2.05),Epoch.AddSeconds(2.15),Epoch);
            var indexType=typeof(BlundervilleBumperPlanner).GetNestedType("ObstacleIndex",BindingFlags.NonPublic)!;
            var index=Activator.CreateInstance(indexType,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,
                null,new object[]{new[]{pulse},Epoch,0.0},null)!;
            var method=typeof(BlundervilleBumperPlanner).GetMethod("SafeSlide",BindingFlags.Static|BindingFlags.NonPublic)!;
            var floor=new RectangleGeometry(start,0,30,80);
            bool Safe(double settle)=>(bool)method.Invoke(null,[floor,index,start,start,start,target,0.0,0.0,settle,0.0,0f,0f,true])!;
            Check(Safe(0)&&!Safe(.5),"The shortest landing escaped a late pulse because only the far endpoint was checked during settle.");
        });
        run("Blunderville bumper planner connects the ice exit to an ordinary walking segment",()=>
        {
            var origin=new Vector3(-207,17,-201);
            var result=Search(new RectangleGeometry(origin,0,30,80),[],origin,
                new(origin,0,3,true,origin,origin,LandingPosition:origin));
            Check(result.Length>=2&&result[0].Sliding&&!result[^1].Sliding&&result[^1].Speed==6&&result[^1].Position.Z<=-209.19f,
                "The exit retained locked ice movement or omitted its ordinary walking continuation.");
            var slide=result[^2];
            Check(Vector3.Dot(Vector3.Normalize(slide.LandingPosition!.Value-origin),
                Vector3.Normalize(result[^1].Position-slide.LandingPosition.Value))>.9999f,
                "Walking turned before the final uncertain physical slide endpoint.");
        });
        foreach (var onFloor in new[] { true, false })
            run($"Blunderville bumper planner corrects a recorded walking overrun while preserving the floor onFloor={onFloor}", () =>
            {
                var origin = new Vector3(-210.5f, 17.47f, -172);
                var player = new Vector3(onFloor ? -207.36604f : -207.2f, 17.47f, -174.48999f);
                var planner = new BlundervilleBumperPlanner();
                var route = Seed(planner, origin, [new(new(-207.5f, 17.47f, -174), 0),
                    new(new(-207.5f, 17.47f, -180), 0), new(new(-208, 17.47f, -180.75f), 0)]);
                var decision = planner.Apply(new(player, false, false, null, "test"), State(), new(_ => { }), player,
                    Epoch.AddSeconds(.2), new(route, 1, true, false, false));
                Check(onFloor ? !decision.Hold && decision.ContinuousPath!.SequenceEqual(route) && Get(planner, "_pending") == null
                    : decision.Hold && Get(planner, "_run") == null,
                    "A valid native corner overrun stopped input, or an actual floor exit retained the route.");
            });
        run("Blunderville bumper planner rechecks a late hazard in a walking overrun before publication", () =>
        {
            var player = new Vector3(-210.25f, 17.47f, -158); var now = Epoch.AddSeconds(.2);
            var planner = new BlundervilleBumperPlanner(); var encounter = new BlundervilleEncounter(_ => { });
            BlundervilleBumperPlanner.Step[] candidate = [new(player - Vector3.UnitZ * 2, 0),
                new(player + new Vector3(2, 0, -2), 0), new(player + new Vector3(2, 0, -4), 0)];
            Set(planner, "_pending", Task.FromResult(candidate)); Set(planner, "_requestedAt", Epoch);
            Set(planner, "_requestedPosition", player);
            var charges = (Dictionary<ulong, BlundervilleHazard>)typeof(BlundervilleEncounter).GetField("_charges", Private)!.GetValue(encounter)!;
            charges[999] = new("rhythm/late/34717/0", new CircleGeometry(player - Vector3.UnitZ * 2.18f, .03f),
                now.AddSeconds(2.0 / 6 + .02), now.AddSeconds(.39), now);
            var decision = planner.Apply(new(player, false, false, null, "test"), State(), encounter, player, now);
            Check(decision.Hold && Get(planner, "_run") == null,
                "A new animation on the physical corner overrun was ignored by publication validation.");
        });
        foreach (var accepted in new[] { false, true })
            run($"Blunderville bumper planner adopts a continuation only after executor acknowledgement accepted={accepted}", () =>
            {
                var player = new Vector3(-210.25f, 17.47f, -158);
                var planner = new BlundervilleBumperPlanner(); var state = State(); var encounter = new BlundervilleEncounter(_ => { });
                BlundervilleBumperPlanner.Step[] steps = [new(player - Vector3.UnitZ * 2, 0), new(player - Vector3.UnitZ * 4, 0), new(player - Vector3.UnitZ * 6, 0)];
                var route = Seed(planner, player, steps);
                BlundervilleBumperPlanner.Step[] continuation = [new(player + new Vector3(.5f, 0, -4), 0), new(player + new Vector3(.5f, 0, -6), 0)];
                Set(planner, "_pending", Task.FromResult(continuation)); Set(planner, "_requestedAt", Epoch);
                Set(planner, "_suffixAnchor", 0); Set(planner, "_requestedRun", route);
                Set(planner, "_suffixRequired", true);
                var normal = new BlundervilleRaceDecision(player, false, false, null, "test");
                var candidate = planner.Apply(normal, state, encounter, player, Epoch.AddSeconds(.2), new(route, 0, true, false, false));
                Check(candidate.ContinuousPath != null && !candidate.ContinuousPath.SequenceEqual(route), "A safe continuation was not proposed.");
                Check(ReferenceEquals(Get(planner, "_run"), route), "The unacknowledged proposal replaced the active route.");
                var acknowledgedPath = accepted ? candidate.ContinuousPath! : route;
                var decision = planner.Apply(normal, state, encounter, player, Epoch.AddSeconds(.3), new(acknowledgedPath, 0, true, false, false));
                Check(decision.ContinuousPath!.SequenceEqual(acknowledgedPath), "Rejected continuation replaced the accepted route or accepted continuation was lost.");
            });
        run("Blunderville bumper planner accepts a necessary safe suffix without faster or deeper progress", () =>
        {
            var now = Epoch.AddSeconds(.2); var origin = new Vector3(-207.5f, 17.47f, -183);
            var player = origin - Vector3.UnitZ * .3f;
            var planner = new BlundervilleBumperPlanner(); var state = State();
            state.SynchronizeStatuses([new(3693, 50, Epoch.AddSeconds(30))], now);
            var encounter = WarmIceEncounter(now);
            var route = Seed(planner, origin, [new(origin - Vector3.UnitZ * 5, 0, 3, true),
                new(origin - Vector3.UnitZ * 10, 0, 3, true), new(origin - Vector3.UnitZ * 15, 0, 3, true)]);
            var charges = (Dictionary<ulong, BlundervilleHazard>)typeof(BlundervilleEncounter).GetField("_charges", Private)!.GetValue(encounter)!;
            charges[999] = new("rhythm/unsafe-future/34717/0", new CircleGeometry(origin - Vector3.UnitZ * 8, .2f),
                now.AddSeconds(2.3), now.AddSeconds(3.3), now);
            // This detour reaches the same depth more slowly. Its purpose is
            // to avoid the next animation while retaining the acquired ray.
            var side = route[0] + Vector3.UnitX * 5;
            BlundervilleBumperPlanner.Step[] continuation = [new(side, 0, 3, true),
                new(side - Vector3.UnitZ * 5, 0, 3, true), new(side - Vector3.UnitZ * 10, 0, 3, true)];
            Set(planner, "_pending", Task.FromResult(continuation)); Set(planner, "_requestedAt", Epoch);
            Set(planner, "_suffixAnchor", 0); Set(planner, "_requestedRun", route);
            var decision = planner.Apply(new(player, false, false, null, "test"), state, encounter, player, now,
                new(route, 0, true, false, true));
            Check(!decision.Hold && decision.ContinuousPath != null && decision.ContinuousPath[0] == route[0]
                && decision.ContinuousPath.Skip(1).SequenceEqual(continuation.Select(s => s.Position)),
                "A safe animation detour was rejected solely because it was slower and reached the same depth.");
            Check(ReferenceEquals(Get(planner, "_run"), route),
                "A necessary suffix replaced the accepted route before native acknowledgement.");
        });
        run("Blunderville bumper planner retains a safe complete route when a candidate only moves its exit farther", () =>
        {
            var now = Epoch.AddSeconds(.2); var origin = new Vector3(-207.5f, 17.47f, -183);
            var player = origin - Vector3.UnitZ * .3f; var requested = 0;
            var planner = new BlundervilleBumperPlanner(_ =>
            { ++requested; return new TaskCompletionSource<BlundervilleBumperPlanner.Step[]>().Task; });
            var state = State(); state.SynchronizeStatuses([new(3693, 50, Epoch.AddSeconds(30))], now);
            var route = Seed(planner, origin, [new(origin - Vector3.UnitZ * 5, 0, 3, true),
                new(origin - Vector3.UnitZ * 10, 0, 3, true), new(origin - Vector3.UnitZ * 15, 0, 3, true),
                new(origin - Vector3.UnitZ * 20, 0, 3, true), new(origin with { Z = -209.1f }, 0, 3, true)]);
            BlundervilleBumperPlanner.Step[] continuation = [new(route[1], 0, 3, true), new(route[2], 0, 3, true),
                new(route[3], 0, 3, true), new(origin with { Z = -209.75f }, 0, 3, true)];
            Set(planner, "_pending", Task.FromResult(continuation)); Set(planner, "_requestedAt", Epoch);
            Set(planner, "_suffixAnchor", 0); Set(planner, "_requestedRun", route);
            var decision = planner.Apply(new(player, false, false, null, "test"), state, WarmIceEncounter(now), player, now,
                new(route, 0, true, false, true));
            Check(!decision.Hold && decision.ContinuousPath!.SequenceEqual(route) && Get(planner, "_proposalRun") == null,
                "Extra depth beyond the wide exit caused an unnecessary replacement of a safe complete route.");
            Check(requested == 0 && Get(planner, "_pending") == null,
                "A complete accepted exit route kept launching needless continuation searches.");
        });
        run("Blunderville bumper planner clearing an off-route path invalidates its completed worker", () =>
        {
            var player = new Vector3(-210.25f, 17.47f, -158); var planner = new BlundervilleBumperPlanner();
            BlundervilleBumperPlanner.Step[] steps = [new(player - Vector3.UnitZ * 2, 0), new(player - Vector3.UnitZ * 4, 0), new(player - Vector3.UnitZ * 6, 0), new(player - Vector3.UnitZ * 8, 0)];
            var route = Seed(planner, player, steps); Set(planner, "_cursor", 2);
            Set(planner, "_pending", Task.FromResult(new[] { new BlundervilleBumperPlanner.Step(steps[^1].Position, 0) }));
            Set(planner, "_requestedAt", Epoch); Set(planner, "_suffixAnchor", 2); Set(planner, "_requestedRun", route);
            var decision = planner.Apply(new(player, false, false, null, "test"), State(), new(_ => { }), player,
                Epoch.AddSeconds(.2), new(route, 2, true, false, false));
            Check(decision.Hold && Get(planner, "_run") == null && Get(planner, "_pending") == null,
                "A consumed historic segment or stale worker revived an invalid route.");
        });
        run("Blunderville bumper planner clears a rejected generation while a physical lock survives", () =>
        {
            var player = new Vector3(-210.25f, 17.47f, -158); var planner = new BlundervilleBumperPlanner();
            var route = Seed(planner, player, [new(player - Vector3.UnitZ * 2, 0), new(player - Vector3.UnitZ * 4, 0)]);
            Set(planner, "_pending", new TaskCompletionSource<BlundervilleBumperPlanner.Step[]>().Task);
            var decision = planner.Apply(new(player, false, false, null, "test"), State(), new(_ => { }), player,
                Epoch.AddSeconds(.2), new(route, 0, false, true, true));
            Check(decision.Hold && Get(planner, "_run") == null && Get(planner, "_pending") == null,
                "The rejected generation was refreshed or its obsolete worker remained eligible.");
        });
        run("Blunderville bumper planner continuation retains physical landing and arrival bounds", () =>
        {
            // This is an already completed seam inside the ice, beyond both
            // possible entry thresholds; its inherited clock must survive.
            var origin = new Vector3(-210, 17.47f, -183);
            var anchor = new BlundervilleBumperPlanner.Step(origin, 0, 3, true,
                origin + new Vector3(-.15f, 0, .1f), origin + new Vector3(.15f, 0, -.1f), -.1, .15);
            var result = Search(new RectangleGeometry(new(-210, 17.47f, -195), 0, 30, 80), [], origin, anchor);
            Check(result.Length > 0 && result.All(s => s.ShortLanding != null && s.LongLanding != null), "Search reconstruction dropped physical landing uncertainty.");
            var first = result[0];
            // The output endpoints are a capsule axis, not two particular
            // short/long histories. Every mixed history must remain covered.
            foreach (var fraction in new[] {0f,.25f,.5f,.75f,1f})
            foreach (var length in new[] {4.88f,4.99f,5.1f})
            {
                var from = Vector3.Lerp(anchor.ShortLanding!.Value,anchor.LongLanding!.Value,fraction);
                var landing = from+Vector3.Normalize(first.Position-from)*length;
                Check(GeometryHelper.DistanceSquaredPointToSegmentXZ(landing,first.ShortLanding!.Value,first.LongLanding!.Value)
                    <= MathF.Pow(first.LandingRadius+.001f,2), "A mixed physical landing escaped the continuation's capsule.");
                Check(first.Duration+first.ShortTimeOffset <= anchor.ShortTimeOffset+(double)length/3
                    && first.Duration+first.LongTimeOffset >= anchor.LongTimeOffset+(double)length/2.88,
                    "A continuation reset its inherited early/late arrival clocks.");
            }
            Check(result.All(s => s.Wait == 0), "A queued continuation introduced an unsupported intermediate wait.");
        });
        run("Blunderville bumper planner checks physical overshoot hazards beyond the nominal endpoint", () =>
        {
            var origin = new Vector3(-210, 17.47f, -181);
            var floor = new RectangleGeometry(new(-210, 17.47f, -195), 0, .8f, 80);
            var baseline = Search(floor, [], origin);
            Check(baseline.Length > 0, "The unobstructed straight ice corridor could not be planned.");
            var pulse = new BlundervilleHazard("rhythm/test/34717/0", new CircleGeometry(origin - Vector3.UnitZ * 5.7f, .08f),
                Epoch.AddSeconds(1.8), Epoch.AddSeconds(2), Epoch);
            Check(Search(floor, [pulse], origin).Length == 0,
                "A pulse on the physical 5.7m landing was ignored because the nominal 5m ray stopped short.");
        });
        run("Blunderville bumper planner protects only the native current slide and replans the next ray", () =>
        {
            var now = Epoch.AddSeconds(.2); var origin = new Vector3(-210.25f, 17.47f, -181);
            var player = origin - Vector3.UnitZ * .3f; var requested = 0;
            var planner = new BlundervilleBumperPlanner(_ =>
            { ++requested; return new TaskCompletionSource<BlundervilleBumperPlanner.Step[]>().Task; });
            var state = State(); state.SynchronizeStatuses([new(3693, 50, Epoch.AddSeconds(30))], now);
            var encounter = WarmIceEncounter(now);
            var charges = (Dictionary<ulong, BlundervilleHazard>)typeof(BlundervilleEncounter).GetField("_charges", Private)!.GetValue(encounter)!;
            charges[999] = new("rhythm/future/34717/0", new CircleGeometry(origin - Vector3.UnitZ * 8, .2f),
                now.AddSeconds(2.3), now.AddSeconds(3.3), now);
            var route = Seed(planner, origin, [new(origin - Vector3.UnitZ * 5, 0, 3, true),
                new(origin - Vector3.UnitZ * 10, 0, 3, true), new(origin - Vector3.UnitZ * 15, 0, 3, true)]);
            var decision = planner.Apply(new(player, false, false, null, "test"), state, encounter, player, now,
                new(route, 0, true, false, true));
            Check(!decision.Hold && requested == 1 && (int)Get(planner, "_suffixAnchor")! == 0,
                "An unstarted second slide was protected from a safe suffix amendment.");
        });
        run("Blunderville bumper planner uses the native cursor before an arrival tolerance is consumed", () =>
        {
            var origin = new Vector3(-210.25f, 17.47f, -181); var player = origin - Vector3.UnitZ * 4.6f;
            var planner = new BlundervilleBumperPlanner(); var state = State();
            state.SynchronizeStatuses([new(3693, 50, Epoch.AddSeconds(30))], Epoch.AddSeconds(.2));
            var route = Seed(planner, origin, [new(origin - Vector3.UnitZ * 5, 0, 3, true), new(origin - Vector3.UnitZ * 10, 0, 3, true)]);
            planner.Apply(new(player, false, false, null, "test"), state, new(_ => { }), player, Epoch.AddSeconds(.2),
                new(route, 0, true, false, true));
            Check((int)Get(planner, "_cursor")! == 0, "The planner advanced beyond the executor's accepted current waypoint.");
        });
        run("Blunderville bumper planner requests a continuation at a safe future anchor near a slide seam", () =>
        {
            var now = Epoch.AddSeconds(.2); var origin = new Vector3(-210.25f, 17.47f, -181);
            var player = origin - Vector3.UnitZ * 4.5f; var requested = 0;
            var planner = new BlundervilleBumperPlanner(_ =>
            { ++requested; return new TaskCompletionSource<BlundervilleBumperPlanner.Step[]>().Task; });
            var state = State(); state.SynchronizeStatuses([new(3693, 50, Epoch.AddSeconds(30))], now);
            var route = Seed(planner, origin, [new(origin - Vector3.UnitZ * 5, 0, 3, true),
                new(origin - Vector3.UnitZ * 10, 0, 3, true), new(origin - Vector3.UnitZ * 15, 0, 3, true)]);
            var decision = planner.Apply(new(player, false, false, null, "test"), state, WarmIceEncounter(now), player, now,
                new(route, 0, true, false, true));
            Check(!decision.Hold && decision.ContinuousPath!.SequenceEqual(route),
                "Approaching a physical seam interrupted the accepted continuous route.");
            var anchor = (int)Get(planner, "_suffixAnchor")!;
            Check(requested == 1 && Get(planner, "_pending") != null && anchor > 0 && anchor < route.Length,
                "A short current segment suppressed continuation despite a validated future anchor with sufficient publication time.");
        });
        run("Blunderville bumper planner refuses an acknowledgement after the native route ended", () =>
        {
            var player = new Vector3(-210.25f, 17.47f, -158);
            var planner = new BlundervilleBumperPlanner(); var state = State(); var encounter = new BlundervilleEncounter(_ => { });
            var route = Seed(planner, player, [new(player - Vector3.UnitZ * 2, 0),
                new(player - Vector3.UnitZ * 4, 0), new(player - Vector3.UnitZ * 6, 0)]);
            Set(planner, "_pending", Task.FromResult(new[] { new BlundervilleBumperPlanner.Step(player + new Vector3(.5f, 0, -4), 0),
                new BlundervilleBumperPlanner.Step(player + new Vector3(.5f, 0, -6), 0) }));
            Set(planner, "_requestedAt", Epoch); Set(planner, "_suffixAnchor", 0); Set(planner, "_requestedRun", route);
            Set(planner, "_suffixRequired", true);
            var normal = new BlundervilleRaceDecision(player, false, false, null, "test");
            var proposal = planner.Apply(normal, state, encounter, player, Epoch.AddSeconds(.2), new(route, 0, true, false, false));
            Check(proposal.ContinuousPath != null && !proposal.ContinuousPath.SequenceEqual(route),
                "The fixture never proposed a safe continuation before native completion.");
            var decision = planner.Apply(normal, state, encounter, player, Epoch.AddSeconds(.3),
                new(proposal.ContinuousPath!, proposal.ContinuousPath!.Count, false, false, false));
            Check(decision.Hold && Get(planner, "_run") == null && Get(planner, "_proposalRun") == null
                && !(bool)Get(planner, "_acknowledged")! && Get(planner, "_pending") == null,
                "Matching historic path metadata was treated as an executor acknowledgement after control ended.");
        });
        run("Blunderville bumper planner rechecks a partial endpoint safety window in the latest snapshot", () =>
        {
            var player = new Vector3(-205.5f, 17.47f, -183); var now = Epoch.AddSeconds(.2);
            var endpoint = player - Vector3.UnitZ * 5;
            foreach (var endpointSafety in new[] { .4, .6 })
            {
                var planner = new BlundervilleBumperPlanner(); var encounter = new BlundervilleEncounter(_ => { });
                BlundervilleBumperPlanner.Step[] candidate = [new(endpoint, 0, 3, true, EndpointSafety: endpointSafety)];
                Set(planner, "_pending", Task.FromResult(candidate)); Set(planner, "_requestedAt", Epoch);
                Set(planner, "_requestedPosition", player);
                var charges = (Dictionary<ulong, BlundervilleHazard>)typeof(BlundervilleEncounter).GetField("_charges", Private)!.GetValue(encounter)!;
                // The recorded five-metre slides finish in about 1.70s. Even
                // with acquisition and occupancy uncertainty, a new animation
                // at 2.35s is beyond the .4s reserve and inside the .6s reserve.
                charges[999] = new("rhythm/late-endpoint/34717/0", new CircleGeometry(endpoint, .12f),
                    now.AddSeconds(2.35), now.AddSeconds(2.45), now);
                var decision = planner.Apply(new(player, false, false, null, "test"), State(), encounter, player, now,
                    new([], 0, false, false, false));
                Check(endpointSafety == .6 ? decision.Hold && Get(planner, "_run") == null
                    : !decision.Hold && Get(planner, "_run") != null,
                    "Publication ignored the partial endpoint reserve, or the late animation affected the earlier control window.");
            }
        });
    }
    private static BlundervilleRaceState State()
    {
        var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20013, 0, 0, 0, Epoch);
        return state;
    }
    private static BlundervilleEncounter WarmIceEncounter(DateTime now, float actorX = -217)
    {
        var encounter = new BlundervilleEncounter(_ => { }); var actor = new BlundervilleActor(1, 16339, new(actorX, 17.47f, -190));
        encounter.Synchronize([actor], now);
        var bumpers = (BlundervilleBumperTracker)typeof(BlundervilleEncounter).GetField("_bumpers", Private)!.GetValue(encounter)!;
        foreach (var seconds in new[] { -18, -12, -6, 0 }) bumpers.Effect(actor.Id, actor.Position, now.AddSeconds(seconds));
        var rhythms = (BlundervilleRhythmTracker)typeof(BlundervilleEncounter).GetField("_rhythms", Private)!.GetValue(encounter)!;
        foreach (var seconds in new[] { -12, -9, -6, -3, 0 }) rhythms.Effect(actor, 34717, actor.Position, 0, now.AddSeconds(seconds));
        Check(encounter.BumpersReady(new(-210.25f, 17.47f, -181.3f)), "The focused ice fixture never confirmed its clocks.");
        return encounter;
    }
    private static Vector3[] Seed(BlundervilleBumperPlanner planner, Vector3 origin, BlundervilleBumperPlanner.Step[] steps)
    {
        ((List<BlundervilleBumperPlanner.Step>)Get(planner, "_path")!).AddRange(steps);
        var route = steps.Select(s => s.Position).ToArray();
        Set(planner, "_origin", origin); Set(planner, "_run", route);
        Set(planner, "_motion", new DirectTraversalMotion(steps.Select(s => s.Speed).ToArray(), steps.Select(s => s.Sliding).ToArray()));
        Set(planner, "_publishedAt", Epoch);
        return route;
    }
    private static BlundervilleBumperPlanner.Step[] Search(IGeometryShape floor, BlundervilleHazard[] hazards, Vector3 origin,
        BlundervilleBumperPlanner.Step? anchor = null)
        => (BlundervilleBumperPlanner.Step[])typeof(BlundervilleBumperPlanner).GetMethod("Search", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [floor, hazards, origin, 6f, Epoch, 20.5, anchor, false])!;
    private static object? Get(BlundervilleBumperPlanner planner, string field) => typeof(BlundervilleBumperPlanner).GetField(field, Private)!.GetValue(planner);
    private static void Set(BlundervilleBumperPlanner planner, string field, object? value) => typeof(BlundervilleBumperPlanner).GetField(field, Private)!.SetValue(planner, value);
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
