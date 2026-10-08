using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

internal static class BlundervilleRegression
{
    private static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    // Independent expectations from the CN Action sheet, not the production catalog.
    private static readonly (uint Action, string Shape, float Range, float Width)[] Dimensions =
    [
        (29795,"circle",3,0),
        (34716,"centered",3,2), (34717,"circle",3,0), (34718,"centered",4,10),
        (34773,"circle",5,0), (34775,"rectangle",3,3), (34776,"circle",2,0),
        (34796,"circle",5,0), (34797,"fan",8,180), (34798,"fan",8,180),
        (34799,"rectangle",4,4), (34800,"rectangle",31,12), (34801,"circle",6,0),
        (34802,"circle",5,0), (34804,"rectangle",6,6), (34805,"circle",6,0),
        (34808,"rectangle",100,8), (34812,"rectangle",6,6),
    ];

    public static void Run(Action<string, Action> run)
    {
        run("Blunderville animated charge occupies its announced traversal before actor movement", () =>
        {
            var encounter = new BlundervilleEncounter(_ => { });
            var start = new Vector3(-6.874f, 18.15f, -363.8f); var end = new Vector3(-3.525f, 18.14f, -363.821f);
            encounter.Synchronize([new(1, 16350, start)], Epoch);
            encounter.Effect(1, 34774, 1, start, 0, Epoch, end);
            var hazard = encounter.Hazards(Epoch.AddSeconds(.1)).Single();
            Check(hazard.Geometry.Contains((start + end) / 2) && hazard.Until >= Epoch.AddSeconds(1.6), "Charge had only a historical route outline.");
            Check(!encounter.Hazards(Epoch.AddSeconds(2)).Any(), "A completed charge remained an obstacle.");
        });
        run("Blunderville rolling ball forecasts its sweep and rejects teleports", () =>
        {
            var encounter = new BlundervilleEncounter(_ => { });
            var actor = new BlundervilleActor(1, 16351, new(5.5f, 17, -364));
            encounter.Synchronize([actor], Epoch);
            actor = actor with { Position = actor.Position + Vector3.UnitZ * .4f };
            encounter.Synchronize([actor], Epoch.AddSeconds(.1));
            var future = encounter.Hazards(Epoch.AddSeconds(.1)).Where(h => h.Id.Contains("/future/")).ToArray();
            Check(future.Length == 4 && future.Any(h => h.Geometry.Contains(actor.Position + Vector3.UnitZ * 3)), "Ball movement was absent from future danger.");
            encounter.Synchronize([actor], Epoch.AddSeconds(.2));
            Check(encounter.Hazards(Epoch.AddSeconds(.2)).Count(h => h.Id.Contains("/future/")) == 4, "An unchanged network sample flickered the prediction off.");
            encounter.Synchronize([actor with { Position = actor.Position + Vector3.UnitX * 30 }], Epoch.AddSeconds(.3));
            Check(!encounter.Hazards(Epoch.AddSeconds(.3)).Any(h => h.Id.Contains("/future/")), "A teleport became a fast-moving hazard.");
        });
        foreach (var expected in Dimensions)
            run($"Blunderville AID {expected.Action}: client dimensions and forward orientation", () =>
            {
                using var f = new Fight();
                var pos = new Vector3(12, 3, -15);
                if (expected.Action == 34776)
                {
                    f.Actors[1] = new(1, 16351, pos);
                    f.Scan();
                }
                else
                {
                    if (expected.Shape == "fan") f.Start(1,expected.Action,pos,MathF.PI/2);
                    f.Effect(1, expected.Action, pos, MathF.PI / 2);
                }
                var shape = f.Draw.Previews.Single().Value.Geometry;
                AssertShape(shape, expected.Action, pos, MathF.PI / 2);
                Check(!shape.Contains(pos + Vector3.UnitX * (expected.Range + 0.02f)), "Client range was expanded.");
                f.AssertNoNavigation();
            });

        run("Blunderville visual wave and unrelated utility actions invent no geometry", () =>
        {
            using var f = new Fight();
            f.Start(1, 34803, Vector3.Zero, null);
            f.Effect(1, 34803, Vector3.Zero, 0);
            f.Effect(1, 34711, Vector3.Zero, 0);
            Check(f.Draw.Previews.Count == 0, "A visual-only or utility action became spatial danger.");
            Check(!BlundervilleCatalog.TryGetShape(34803, out _), "Generic dodge fabricated a visual shape.");
        });
        run("Blunderville cast locks actor origin rather than ARR target location", () =>
        {
            using var f = new Fight();
            var origin = new Vector3(-6,25.58691f,190.4f);
            var target = new Vector3(-6.02731f,25.55886f,187.39605f);
            f.Start(1,34812,origin,0,0.7,target);
            AssertShape(f.Draw.Previews["cast/1"].Geometry,34812,origin,0);
            Check(f.Draw.Previews.Count(p => p.Key.StartsWith("wave/")) == 4, "Real ARR source origin failed to create the four following rows.");
        });
        run("Blunderville helper teleport during one cast cannot move its locked preview", () =>
        {
            using var f = new Fight();
            var origin = new Vector3(-6,25.58691f,190.4f);
            f.Start(1,34812,origin,0,0.7);
            f.Now = f.Now.AddSeconds(0.2);
            var next = origin + new Vector3(0,-2.21628f,6);
            f.Actors[1] = f.Actors[1] with { Position = next, Cast = f.Actors[1].Cast! with { SourcePosition = next } };
            f.Scan();
            Check(f.Draw.Previews["cast/1"].Geometry.Position == origin, "Native scan made a cast follow a helper teleport.");
            Check(f.Draw.Previews.Where(p=>p.Key.StartsWith("wave/")).All(p=>Math.Abs(p.Value.Geometry.Position.Z-origin.Z)%6 < 0.01f),
                "Following rows shifted off the recorded wave grid.");
        });
        run("Blunderville missing rectangle facing fails once and later valid cast recovers", () =>
        {
            using var f = new Fight();
            f.Start(1,34800,Vector3.Zero,null);
            f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Errors.Count == 1, "Unknown facing drew a made-up rectangle or repeated diagnostics.");
            f.Start(1,34800,Vector3.Zero,MathF.PI/2);
            AssertShape(f.Draw.Previews.Single().Value.Geometry,34800,Vector3.Zero,MathF.PI/2);
        });
        run("Blunderville packet facing correction survives later stale native scans", () =>
        {
            using var f = new Fight();
            f.Start(1,34800,Vector3.Zero,0,3.2);
            var native = f.Actors[1].Cast!;
            f.Engine.StartCast(1,native with {Rotation=MathF.PI/2},f.Now);
            f.Render();
            AssertShape(f.Draw.Previews["cast/1"].Geometry,34800,Vector3.Zero,MathF.PI/2);
            f.Now = f.Now.AddSeconds(0.1); f.Scan();
            AssertShape(f.Draw.Previews["cast/1"].Geometry,34800,Vector3.Zero,MathF.PI/2);
        });
        run("Blunderville cancel is source/action scoped and stale snapshots cannot revive it", () =>
        {
            using var f = new Fight();
            f.Start(1,34800,Vector3.Zero,0);
            f.Start(2,34800,Vector3.UnitX*8,0);
            f.Engine.Cancel(1,34717); f.Render();
            Check(f.Draw.Previews.Count == 2, "Unrelated cancel removed a cast.");
            f.Engine.Cancel(1,34800); f.Scan();
            Check(!f.Draw.Previews.ContainsKey("cast/1") && f.Draw.Previews.ContainsKey("cast/2"), "Cancelled source revived or another source was cleared.");
        });
        run("Blunderville a distinct same-action cast 100ms after cancellation is accepted", () =>
        {
            using var f = new Fight();
            f.Start(1,34805,Vector3.Zero,null);
            f.Engine.Cancel(1,34805);
            f.Now = f.Now.AddSeconds(0.1);
            var origin = Vector3.UnitX*7;
            f.Start(1,34805,origin,null);
            Check(f.Draw.Previews["cast/1"].Geometry.Position == origin, "Cancellation deduplication swallowed a distinct recast.");
        });
        run("Blunderville a delayed cancel from the previous cast cannot kill its replacement", () =>
        {
            using var f = new Fight();
            f.Start(1,34805,Vector3.Zero,null);
            f.Engine.Cancel(1,34805,Epoch.AddSeconds(0.05));
            f.Now = Epoch.AddSeconds(0.1);
            var replacement = Vector3.UnitX*8;
            f.Start(1,34805,replacement,null);
            f.Engine.Cancel(1,34805,Epoch.AddSeconds(0.05)); f.Render();
            Check(f.Draw.Previews["cast/1"].Geometry.Position==replacement,"Old cancellation removed a newer cast.");
        });
        run("Blunderville normal native cast completion waits for its actual effect", () =>
        {
            using var f = new Fight();
            f.Start(1,34800,Vector3.Zero,0,3.2);
            f.Now = Epoch.AddSeconds(3.2); f.Scan();
            Check(f.Draw.Previews.ContainsKey("cast/1"), "Normal cast end discarded the effect window.");
            f.Effect(1,34800,Vector3.Zero,0);
            Check(!f.Draw.Previews.ContainsKey("cast/1") && f.Draw.Previews.Count == 1, "Actual effect did not replace the cast once.");
            f.Now = f.Now.AddSeconds(0.56); f.Scan();
            Check(f.Draw.Previews.Count == 0, "Effect confirmation leaked beyond its observed pulse.");
        });
        run("Blunderville per-target duplicate effect neither duplicates nor extends pulse", () =>
        {
            using var f = new Fight();
            f.Effect(1,34773,Vector3.Zero,0,77);
            f.Now = Epoch.AddSeconds(0.3);
            for (var i=0;i<8;i++) f.Effect(1,34773,Vector3.Zero,0,77);
            Check(f.Draw.Previews.Count == 1, "One action was rendered per target.");
            f.Now = Epoch.AddSeconds(0.56); f.Render();
            Check(f.Draw.Previews.Count == 0, "Duplicate target packet prolonged one pulse.");
        });
        run("Blunderville identical sequence numbers from separate helpers remain independent", () =>
        {
            using var f = new Fight();
            f.Effect(1,34773,Vector3.Zero,0,77);
            f.Effect(2,34773,Vector3.UnitX*12,0,77);
            Check(f.Draw.Previews.Count == 2, "Global sequence alias suppressed a different source.");
        });
        run("Blunderville rolling balls follow current position without effect afterimages", () =>
        {
            using var f = new Fight();
            f.Actors[1] = new(1,16351,Vector3.Zero); f.Scan();
            f.Effect(1,34776,Vector3.Zero,0);
            f.Now = f.Now.AddSeconds(0.1);
            var next = new Vector3(8,0,2);
            f.Actors[1] = f.Actors[1] with { Position = next }; f.Scan();
            Check(f.Draw.Previews.Count == 1 && f.Draw.Previews.Single().Value.Geometry.Position == next,
                "Rolling ball left a stale effect circle or failed to move.");
            f.Actors.Remove(1); f.Scan();
            Check(f.Draw.Previews.Count == 0, "Destroyed rolling ball remained visible.");
        });
        run("Blunderville rotating beam follows its actor between effect packets and expires", () =>
        {
            using var f = new Fight();
            var origin = new Vector3(200,10,-125);
            f.Actors[1] = new(1,9020,origin,Rotation:0); f.Player=origin; f.Scan();
            f.Effect(1,34808,origin,0,17);
            f.Now = Epoch.AddSeconds(0.1);
            f.Actors[1] = f.Actors[1] with {Rotation=MathF.PI/2}; f.Scan();
            Check(f.Draw.Previews.Count==1,"Rotating beam left multiple historical rectangles.");
            AssertShape(f.Draw.Previews["beam/1"].Geometry,34808,origin,MathF.PI/2);
            Check(!f.Draw.Previews["beam/1"].Geometry.Contains(origin+Vector3.UnitZ*20),"Rotating beam retained an old angle between packets.");
            f.Now = Epoch.AddSeconds(1.31); f.Scan();
            Check(f.Draw.Previews.Count==0,"Beam without new observations stayed permanently active.");
        });
        run("Blunderville fan confirmation preserves cast facing when effect facing differs by ninety degrees", () =>
        {
            using var f = new Fight();
            var origin = new Vector3(180,10,-125);
            f.Start(1,34797,origin,MathF.PI/2,1.2);
            f.Now = Epoch.AddSeconds(1.2);
            f.Effect(1,34797,origin,0,100);
            AssertShape(f.Draw.Previews.Single().Value.Geometry,34797,origin,MathF.PI/2);
            f.Effect(2,34798,origin,0,101);
            Check(f.Draw.Previews.Count==1,"A fan with no locked cast trusted its misleading effect angle.");
        });
        run("Blunderville observed return route preserves its width and both endpoints", () =>
        {
            using var f = new Fight();
            var a = new Vector3(-8,6,270); var b = new Vector3(8,6,270);
            f.Effect(1,34774,a,0); f.Effect(1,34774,b,0);
            f.Effect(1,34774,a,0);
            var route = (RayRectangleGeometry)f.Draw.Previews["route/1"].Geometry;
            Check(Math.Abs(route.Width-3)<0.001f && Math.Abs(route.Length-16)<0.001f,
                "Return route used a guessed fixed charge distance or changed client width.");
            Check(route.Contains(a) && route.Contains(b) && !route.Contains(new Vector3(0,6,271.52f)), "Observed route geometry missed an endpoint or became too wide.");
            Check(f.Draw.Previews["route/1"].OutlineOnly, "Observed travel corridor was presented as a timed hit.");
            f.Effect(1,34774,new Vector3(200,10,-125),0);
            Check(!f.Draw.Previews.ContainsKey("route/1"), "Unrelated teleport stretched a corridor between courses.");
        });
        run("Blunderville casted charge uses recorded source and endpoint, ignoring packet placeholder facing", () =>
        {
            using var f = new Fight();
            var source = new Vector3(184.83881f,10,-122.3267f);
            var endpoint = new Vector3(175.52453f,9.99466f,-120.71412f);
            f.Start(1,35409,source,-MathF.PI,1.5,endpoint);
            var rect = (RayRectangleGeometry)f.Draw.Previews["cast/1"].Geometry;
            var expected = Vector2.Distance(new(source.X,source.Z),new(endpoint.X,endpoint.Z));
            Check(Vector3.Distance(rect.Position,source)<0.001f && Math.Abs(rect.Width-3)<0.001f && Math.Abs(rect.Length-expected)<0.001f,
                "Charge discarded ARR source-to-endpoint geometry.");
            Check(rect.Contains(endpoint) && !rect.Contains(source-Vector3.UnitZ*3), "Charge trusted placeholder facing instead of the destination.");
        });
        run("Blunderville casted charge with no source does not invent a path from zero", () =>
        {
            using var f = new Fight();
            f.Engine.StartCast(9,new(35409,new Vector3(175.52453f,10,-120.71412f),-MathF.PI,f.Now,f.Now.AddSeconds(1.8)),f.Now);
            f.Render();
            Check(f.Draw.Previews.Count == 0, "Missing charge source produced a fabricated origin.");
        });
        run("Blunderville self AOE without a source cannot treat packet target coordinates as an actor", () =>
        {
            using var f = new Fight();
            var target = new Vector3(200,10,-125);
            f.Engine.StartCast(9,new(34805,target,null,f.Now,f.Now.AddSeconds(3.3)),f.Now); f.Render();
            Check(f.Draw.Previews.Count==0,"Missing self-AOE source used an unrelated packet target.");
            var origin = target+Vector3.UnitX*10;
            f.Actors[9]=new(9,16338,origin); f.Scan();
            f.Engine.StartCast(9,new(34805,target,null,f.Now,f.Now.AddSeconds(3.3)),f.Now); f.Player=origin; f.Render();
            AssertShape(f.Draw.Previews.Single().Value.Geometry,34805,origin,0);
        });
        run("Blunderville separate wave columns are consumed independently", () =>
        {
            using var f = new Fight();
            var a = new Vector3(-6,25.58691f,190.4f); var b = a + Vector3.UnitX*12;
            f.Start(1,34812,a,0,0.7); f.Start(2,34812,b,0,0.7);
            Check(f.Draw.Previews.Count(p=>p.Key.StartsWith("wave/")) == 8, "Two columns collapsed into one global wave.");
            f.Now = Epoch.AddSeconds(1.2); f.Effect(1,34812,a,0,100);
            f.Now = Epoch.AddSeconds(1.7); f.Effect(1,34804,a+new Vector3(0,-2.21628f,6),0,101);
            Check(!f.Draw.Previews.ContainsKey("wave/1/1") && f.Draw.Previews.ContainsKey("wave/1/2") && f.Draw.Previews.ContainsKey("wave/2/2"),
                "An effect consumed the other column or left its own consumed row active.");
        });
        run("Blunderville actual wave effect corrects the following row's urgency clock", () =>
        {
            using var f = new Fight();
            var origin = new Vector3(-6,25.58691f,190.4f);
            f.Start(1,34812,origin,0,0.7);
            f.Now = Epoch.AddSeconds(1.2); f.Effect(1,34812,origin,0,100);
            f.Now = Epoch.AddSeconds(1.3); f.Render();
            Check(f.Draw.Previews["wave/1/1"].OutlineOnly, "Following row retained an early inferred deadline after a later effect.");
            f.Now = Epoch.AddSeconds(1.48); f.Render();
            Check(!f.Draw.Previews["wave/1/1"].OutlineOnly, "Following row failed to become urgent at the effect-corrected time.");
            f.Now = Epoch.AddSeconds(1.7); f.Effect(1,34804,origin+new Vector3(0,-2.21628f,6),0,101);
            f.Now = Epoch.AddSeconds(1.95); f.Render();
            Check(!f.Draw.Previews["wave/1/2"].OutlineOnly, "First traveling hit failed to correct the second row.");
        });
        foreach (var reason in new[] {"cancel","death","remove","timeout","reset","rewind"})
            run($"Blunderville {reason} clears owned casts and following waves", () =>
            {
                using var f = new Fight();
                f.Start(1,34812,new Vector3(0,25.58691f,190.4f),0,0.7);
                switch(reason)
                {
                    case "cancel": f.Engine.Cancel(1,34812); break;
                    case "death": f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan(); break;
                    case "remove": f.Engine.Remove(1); f.Actors.Remove(1); break;
                    case "timeout": f.Now = Epoch.AddSeconds(4); break;
                    case "reset": f.Engine.Reset(); break;
                    case "rewind": f.Now = Epoch.AddSeconds(-1); break;
                }
                f.Render();
                Check(f.Draw.Previews.Count == 0, $"{reason} left a cast or predicted wave behind.");
                f.AssertNoNavigation();
            });
        run("Blunderville mechanism toggles remove and restore only their own previews", () =>
        {
            using var f = new Fight();
            f.Effect(1,34800,Vector3.Zero,0); f.Effect(2,34773,Vector3.UnitX*8,0);
            f.Disabled.Add(BlundervilleCatalog.Wind); f.Render();
            Check(f.Draw.Previews.Count == 1 && f.Draw.Previews.Single().Value.Geometry is CircleGeometry, "One disabled group cleared another group or stayed visible.");
            f.Disabled.Clear(); f.Render();
            Check(f.Draw.Previews.Count == 2, "Re-enabling a group lost still-valid observed output.");
            f.AssertNoNavigation();
        });
        run("Blunderville absent player, another course and shutdown clear visible output", () =>
        {
            var f = new Fight();
            f.Effect(1,34773,Vector3.Zero,0);
            f.Player = null; f.Render(); Check(f.Draw.Previews.Count == 0,"Missing or dead player left live drawing.");
            f.Player = new Vector3(200,0,0); f.Render(); Check(f.Draw.Previews.Count == 0,"Other course leaked into current view.");
            f.Player = Vector3.Zero; f.Render(); Check(f.Draw.Previews.Count == 1,"Returning to current course failed to restore valid output.");
            f.Dispose(); Check(f.Draw.Previews.Count == 0,"Script stop left owned output.");
        });
        run("Blunderville invalid transforms and durations never publish nonfinite geometry", () =>
        {
            using var f = new Fight();
            f.Engine.StartCast(1,new(34800,new(float.NaN,0,0),0,f.Now,f.Now.AddSeconds(3)),f.Now);
            f.Engine.StartCast(2,new(34800,Vector3.Zero,float.NaN,f.Now,f.Now.AddSeconds(3),Vector3.Zero),f.Now);
            f.Engine.StartCast(3,new(34800,Vector3.Zero,0,f.Now,f.Now.AddSeconds(-1),Vector3.Zero),f.Now);
            f.Engine.Effect(4,34773,1,new(float.PositiveInfinity,0,0),0,f.Now); f.Render();
            Check(f.Draw.Previews.Count == 0,"Malformed data escaped into the geometry renderer.");
        });

        foreach (var stamp in new[] {"193057","193849","195158","203534","204237"})
            run($"Blunderville anonymized ARR {stamp}: three courses and exact observed geometry", () => ReplayFixture(stamp));
        run("Blunderville ARR fixtures contain only anonymous NPC mechanism observations", FixturePrivacy);
    }

    private static void ReplayFixture(string stamp)
    {
        using var document = ReadFixture(stamp);
        var sections = document.RootElement.GetProperty("sections");
        Check(sections.GetArrayLength() >= 3,"Recording did not retain all three courses.");
        foreach(var section in sections.EnumerateArray())
        {
            using var f = new Fight();
            foreach(var actor in section.GetProperty("actors").EnumerateArray())
            {
                var id = actor.GetProperty("actor").GetUInt64();
                f.Actors[id] = new(id,actor.GetProperty("oid").GetUInt32(),Position(actor.GetProperty("pos")),Rotation:actor.GetProperty("rotation").GetSingle());
            }
            f.Scan();
            var drawings = 0;
            var castFacings = new Dictionary<ulong,float>();
            foreach(var e in section.GetProperty("events").EnumerateArray())
            {
                f.Now = Epoch.AddSeconds(e.GetProperty("t").GetDouble());
                var source = e.GetProperty("actor").GetUInt64();
                switch(e.GetProperty("type").GetString())
                {
                    case "sample":
                        var pos = Position(e.GetProperty("pos"));
                        var actorRotation = e.GetProperty("rotation").GetSingle();
                        f.Actors[source] = f.Actors.TryGetValue(source,out var prior)
                            ? prior with {Position=pos,Rotation=actorRotation} : new(source,e.GetProperty("oid").GetUInt32(),pos,Rotation:actorRotation);
                        f.Player = pos; f.Scan();
                        break;
                    case "cast":
                        var action = e.GetProperty("action").GetUInt32();
                        var origin = Position(e.GetProperty("pos"));
                        var rotation = e.GetProperty("rotation").GetSingle();
                        castFacings[source] = rotation;
                        f.Start(source,action,origin,rotation,e.GetProperty("seconds").GetDouble(),Position(e.GetProperty("location")));
                        if (action is not (34803 or 34774))
                        {
                            Check(f.Draw.Previews.TryGetValue($"cast/{source}",out var cast),$"{stamp}: real AID {action} cast disappeared.");
                            if(action!=35409) AssertShape(cast!.Geometry,action,origin,rotation);
                            else Check(((RayRectangleGeometry)cast!.Geometry).Contains(Position(e.GetProperty("location"))),"Recorded charge lost its destination.");
                            drawings++;
                        }
                        break;
                    case "effect":
                        var hitAction = e.GetProperty("action").GetUInt32();
                        var hitPos = Position(e.GetProperty("pos"));
                        var hitRotation = e.GetProperty("rotation").GetSingle();
                        f.Effect(source,hitAction,hitPos,hitRotation,e.GetProperty("sequence").GetUInt32());
                        if(hitAction is not (34774 or 34776 or 34803 or 35409))
                        {
                            var prefix = hitAction==34808 ? $"beam/{source}" : $"hit/{source}/{hitAction}";
                            var hit = f.Draw.Previews.Single(p=>p.Key.StartsWith(prefix) && Vector3.Distance(p.Value.Geometry.Position,hitPos)<0.001f).Value;
                            AssertShape(hit.Geometry,hitAction,hitPos,hitAction is 34797 or 34798 ? castFacings[source] : hitRotation); drawings++;
                        }
                        break;
                    case "remove": f.Actors.Remove(source); f.Engine.Remove(source); f.Render(); break;
                    default: throw new InvalidOperationException("Unknown anonymous fixture observation.");
                }
                f.AssertNoNavigation();
            }
            Check(drawings>10,"Fixture replay did not exercise real mechanism output.");
            f.Engine.Reset(); f.Render(); Check(f.Draw.Previews.Count==0,"Course reset leaked observations.");
        }
    }

    private static void FixturePrivacy()
    {
        var actions = new HashSet<uint>();
        foreach(var stamp in new[] {"193057","193849","195158","203534","204237"})
        {
            using var document = ReadFixture(stamp);
            var root = document.RootElement;
            Check(root.GetProperty("schema").GetInt32()==1 && root.GetProperty("territory").GetUInt32()==1165,"Wrong replay schema or territory.");
            Check(root.EnumerateObject().All(p=>new[]{"schema","territory","recording","sections"}.Contains(p.Name)),"Fixture contains unreviewed root fields.");
            foreach(var section in root.GetProperty("sections").EnumerateArray())
            {
                Check(section.GetProperty("duration").GetDouble() is >=12 and <=20,"Fixture expanded beyond an anonymous short segment.");
                foreach(var actor in section.GetProperty("actors").EnumerateArray())
                {
                    Check(actor.EnumerateObject().All(p=>new[]{"actor","oid","pos","rotation"}.Contains(p.Name)),"NPC fixture contains identity or raw fields.");
                    Check(actor.GetProperty("actor").GetUInt64() is >0 and <256 && BlundervilleCatalog.IsActor(actor.GetProperty("oid").GetUInt32()),"Original entity ID or player actor was retained.");
                }
                foreach(var e in section.GetProperty("events").EnumerateArray())
                {
                    Check(e.EnumerateObject().All(p=>new[]{"t","type","actor","oid","pos","action","sequence","rotation","seconds","location"}.Contains(p.Name)),"Fixture contains names, raw packets, chat or unreviewed fields.");
                    Check(e.GetProperty("actor").GetUInt64() is >0 and <256,"Original entity identifier escaped anonymization.");
                    if(e.TryGetProperty("action",out var action)) actions.Add(action.GetUInt32());
                }
            }
        }
        using var final = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "animation-final-194843.json")));
        foreach (var e in final.RootElement.GetProperty("events").EnumerateArray().Where(e => e[1].GetString() == "hit"))
        {
            Check(e.GetArrayLength() == 5 && e[2].GetUInt64() is > 0 and < 256, "Final fixture retained an original identifier or unreviewed fields.");
            actions.Add(e[3].GetUInt32());
        }
        Check(actions.SetEquals(Dimensions.Select(d=>d.Action).Concat([34774u,34803u,35409u])),"Recordings did not cover the complete observed action set.");
    }

    private static void AssertShape(IGeometryShape shape,uint action,Vector3 origin,float rotation)
    {
        var expected = Dimensions.Single(d=>d.Action==action);
        Check(Vector3.Distance(shape.Position,origin)<0.001f,$"AID {action} used a target location or a moving helper position.");
        var forward = new Vector3(MathF.Sin(rotation),0,MathF.Cos(rotation));
        var side = new Vector3(MathF.Cos(rotation),0,-MathF.Sin(rotation));
        switch(expected.Shape)
        {
            case "circle":
                Check(shape is CircleGeometry circle && Math.Abs(circle.Radius-expected.Range)<0.001f,$"AID {action} circle differs from client range.");
                Check(shape.Contains(origin+forward*(expected.Range-0.02f)) && !shape.Contains(origin+forward*(expected.Range+0.02f)),"Circle radius was inflated.");
                break;
            case "rectangle":
                Check(shape is RayRectangleGeometry rectangle && Math.Abs(rectangle.Length-expected.Range)<0.001f && Math.Abs(rectangle.Width-expected.Width)<0.001f,
                    $"AID {action} is not the client forward rectangle.");
                Check(shape.Contains(origin+forward*(expected.Range-0.02f)) && !shape.Contains(origin-forward*0.02f),"Forward rectangle was centered or reversed.");
                Check(shape.Contains(origin+forward*0.5f+side*(expected.Width/2-0.02f)) && !shape.Contains(origin+forward*0.5f+side*(expected.Width/2+0.02f)),"Rectangle full width was doubled or inflated.");
                break;
            case "centered":
                Check(shape is RectangleGeometry centered && Math.Abs(centered.Length-expected.Range)<0.001f && Math.Abs(centered.Width-expected.Width)<0.001f,
                    "Animated helper rectangle lost its recorded center or dimensions.");
                Check(shape.Contains(origin-forward*(expected.Range/2-.02f)) && shape.Contains(origin+forward*(expected.Range/2-.02f))
                    && !shape.Contains(origin+forward*(expected.Range/2+.02f)), "Animated rectangle did not cover both sides of its helper.");
                break;
            case "fan":
                Check(shape is FanGeometry fan && Math.Abs(fan.Radius-8)<0.001f && Math.Abs(fan.AngleRadians-MathF.PI)<0.001f,"Client fan180 omen was not interpreted as total 180 degrees.");
                Check(shape.Contains(origin+forward*7) && !shape.Contains(origin-forward*0.02f),"Fan used an ordinary or reversed facing.");
                Check(shape.Contains(origin+forward*0.1f+side*4) && !shape.Contains(origin-forward*0.1f+side*4),"Fan hemisphere boundary changed.");
                break;
        }
    }

    private static JsonDocument ReadFixture(string stamp) => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","Blunderville",$"arr-{stamp}.json")));
    private static Vector3 Position(JsonElement p) => new(p[0].GetSingle(),p[1].GetSingle(),p[2].GetSingle());
    private static void Check(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }

    private sealed class Fight : IDisposable
    {
        public BlundervilleEncounter Engine { get; }
        public BlundervilleOutput Output { get; }
        public ScriptSafeZoneApi Safe { get; } = new();
        public ScriptDrawApi Draw { get; } = new();
        public Dictionary<ulong,BlundervilleActor> Actors { get; } = [];
        public HashSet<string> Disabled { get; } = [];
        public List<string> Errors { get; } = [];
        public DateTime Now = Epoch;
        public Vector3? Player = Vector3.Zero;
        private uint _sequence = 1;
        public Fight() { Engine = new(Errors.Add); Output = new(Safe,Draw); }
        public void Start(ulong source,uint action,Vector3 origin,float? rotation,double seconds=3,Vector3? location=null)
        {
            Player = origin;
            var cast = new BlundervilleCast(action,location??origin,rotation,Now,Now.AddSeconds(seconds+0.3),origin);
            var oid = Actors.TryGetValue(source,out var actor) ? actor.Oid : action==35409 ? 16350u : 16338u;
            Actors[source] = new(source,oid,origin,false,cast,rotation??0);
            Engine.StartCast(source,cast,Now); Render();
        }
        public void Effect(ulong source,uint action,Vector3 position,float? rotation,uint? sequence=null)
        {
            Player = position; Engine.Effect(source,action,sequence??_sequence++,position,rotation,Now); Render();
        }
        public void Scan()
        {
            foreach(var (id,actor) in Actors.ToArray())
                if(actor.Cast is { } cast && Now >= cast.FinishAt.AddSeconds(-0.3)) Actors[id] = actor with {Cast=null};
            Engine.Synchronize(Actors.Values,Now); Render();
        }
        public void Render() => Output.Apply(Engine,Now,Player,g=>!Disabled.Contains(g));
        public void AssertNoNavigation()
        {
            var snapshot = Safe.Manager.BuildSnapshot(Now);
            Check(snapshot.DangerAreas.Count==0 && snapshot.GoalAreas.Count==0 && snapshot.HardObstacles.Count==0 && snapshot.ArenaBoundary==null,
                "Entertainment drawing registered combat avoidance, goals or map obstacles.");
        }
        public void Dispose() { Output.Clear(); Engine.Reset(); Safe.Dispose(); }
    }
}
