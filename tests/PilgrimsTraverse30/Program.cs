using System.Numerics;
using System.Reflection;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicRuntime;

NativePt30Tests.Run();
if (args is ["--recording", var recordingPath])
{
    RecordedPt30Tests.Run(recordingPath);
    return;
}
using var data = new Lumina.GameData(args[0], new Lumina.LuminaOptions
{
    DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified,
    PanicOnSheetChecksumMismatch = true
});
MechanicRuntimeEnvironment.Initialize(data, DispatchProxy.Create<Dalamud.Plugin.Services.IPluginLog, RecordingLog>());
MechanicCoverageTests.Run();
using var session = new MechanicSession("PT30 failure regression");
var start = new DateTime(2026, 11, 17, 5, 8, 11, DateTimeKind.Utc);
session.BeginFrame(start);
session.Execute(new WorldState.OpZoneChange(1283, 1034));
// Last run's position when Divine Favor began. Heading must not select travel direction.
session.Execute(new ActorState.OpCreate(100, 0, 0, 0, "Player", 0, ActorType.Player, Class.MCH, 100,
    new(-287.4f, 0, -311.8f, 0), .5f, new(120145, 120145, 0, 10000, 10000), true, true, 0, 0, 0));
session.Execute(new PartyState.OpModify(0, new(100, 100, false, "Player")));
session.Execute(new ActorState.OpCreate(1, 0x4609, 2, 0, "Boss", 13863, ActorType.Enemy, Class.None, 100,
    new(-300, 0, -300, 0), 14, new(5741267, 5741267, 0, 0, 0), true, false, 0, 0, 0));
session.Execute(new ActorState.OpCombat(1, true));
session.Update();
var module = session.BossModules.ActiveModule ?? throw new Exception("PT30 module did not activate");
if (args.Length > 1 && args[1] == "--export-arena")
{
    var bounds = (ArenaBoundsCustom)module.Bounds;
    var part = bounds.Polygon.Parts.Single();
    var dataOut = new {
        exterior = part.Exterior.ToArray().Select(p => new[] { p.X, p.Z }),
        holes = Enumerable.Range(0, part.HoleStarts.Count).Select(i => part.Interior(i).ToArray().Select(p => new[] { p.X, p.Z }))
    };
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(dataOut));
    return;
}
var player = session.World.Actors.Find(100)!;
var chase = module.FindComponent<StandardChasingAOEs>() ?? throw new Exception("Divine Favor missing");
var chaser = new GenericChasingAOEs.Chaser(new AOEShapeCircle(4), player, player.Position, 0, 8, start.AddSeconds(3), .6);
chase.Chasers.Add(chaser);
var adapter = new PilgrimsTraverse30ChaseHints();
var hints = new AIHints();
adapter.Update(module, hints, 0);
var initialGoal = adapter.Destination ?? throw new Exception("Chase did not start");
Check(hints.TemporaryObstacles.Any(o => o.Contains(new(-300, -300))), "Central hole must block shortcuts");
session.Execute(new ActorState.OpMove(100, new(player.Position.X, 0, player.Position.Z, MathF.PI)));
hints.Clear();
adapter.Update(module, hints, 0);
Check(adapter.Destination == initialGoal, "Turning to attack must not reverse chase direction");
double pulseAt = 3;
var pulses = 0;
var minimumSeparation = float.MaxValue;
WPos? movementGoal = initialGoal;
for (int step = 1; step <= 73; step++)
{
    double elapsed = step * .1;
    session.BeginFrame(start.AddSeconds(elapsed));
    hints.Clear();
    adapter.Update(module, hints, 0);
    // Match MechanicNavigationHost's 500ms strong-goal conversion cadence.
    if (step % 5 == 0) movementGoal = adapter.Destination;
    if (movementGoal is { } goal)
    {
        var delta = goal - player.Position;
        var distance = delta.Length();
        var next = player.Position + delta * (Math.Min(.6f, distance) / distance);
        session.Execute(new ActorState.OpMove(100, new(next.X, 0, next.Z, step % 2 == 0 ? 0 : MathF.PI)));
        Check(module.Bounds.Contains(next - module.Center), "Continuous chase route left the actual arena");
        Check((next - new WPos(-300, -300)).Length() > 14.5f, "Route cut across the central hole");
    }
    if (chaser.NumRemaining > 0 && elapsed + .001 >= pulseAt)
    {
        var hit = chaser.PredictedPosition();
        minimumSeparation = Math.Min(minimumSeparation, (player.Position - hit).Length());
        Check((player.Position - hit).Length() > 4.5f, "Simulated Divine Favor caught the running player");
        chase.Advance(hit, 3.5f, session.World.CurrentTime);
        pulses++;
        pulseAt += .6;
    }
}
hints.Clear();
adapter.Update(module, hints, 0);
Check(pulses == 8 && !adapter.Active && hints.GoalZones.Count == 0, "All eight hits must end the movement goal");
Console.WriteLine($"PASS PT30: real module, log-derived start, heading reversal, arena hole, eight chase pulses; minimum separation={minimumSeparation:F2}m.");
Console.WriteLine("This is an offline movement model at 6m/s with 500ms MP goal refresh, not proof of live network timing or all boss mechanics.");

void Check(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}
