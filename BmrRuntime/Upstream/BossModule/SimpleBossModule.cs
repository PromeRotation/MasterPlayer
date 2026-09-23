namespace BossMod;

// base class for simple boss modules (hunts, fates, dungeons, etc.)
// these always center map around PC
public abstract class SimpleBossModule(WorldState ws, Actor primary) : BossModule(ws, primary, primary.Position, new ArenaBoundsCircle(30f, AllowObstacleMap: true))
{
    private WPos _prevFramePathfindCenter;

    public override bool CheckReset() => !PrimaryActor.InCombat;

    protected override void UpdateModule()
    {
        Arena.Center = WorldState.Party.Player()?.Position ?? default;
        // we don't want to change pathfinding map origin every time player slightly moves, it makes movement jittery
        // instead, (a) quantize origin and (b) only update it when player moves sufficiently far away
        if (!_prevFramePathfindCenter.AlmostEqual(Arena.Center, 5f))
        {
            _prevFramePathfindCenter = Arena.Center.Rounded();
        }
    }

    protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        hints.PathfindMapCenter = _prevFramePathfindCenter;
        hints.PathfindMapBounds = AIHints.DefaultBounds;
    }
}

public abstract class OpenWorldFate(WorldState ws, Actor primary) : SimpleBossModule(ws, primary)
{
    private uint _fateID;
    private WPos _fateCenter;
    private float _fateRadius;
    private ArenaBoundsCircle? _fateBounds;

    // only activate module when close and deactivate it if player leaves area
    protected override bool CheckPull()
    {
        UpdateFateArena();
        return base.CheckPull() && Raid.Player() is { } player && player.Position.InCircle(Arena.Center, Arena.Bounds.Radius);
    }

    public override bool CheckReset() => base.CheckReset()
        || (_fateID != 0 && WorldState.Client.ActiveFate.ID != _fateID)
        || Raid.Player() is not { } player
        || !player.Position.InCircle(Arena.Center, Arena.Bounds.Radius + 10f);

    protected override void UpdateModule() => UpdateFateArena();

    protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        hints.PathfindMapCenter = Arena.Center;
        hints.PathfindMapBounds = Arena.Bounds;
    }

    private void UpdateFateArena()
    {
        var fate = WorldState.Client.ActiveFate;
        var matches = fate.ID != 0 && fate.Radius > 0f
            && (PrimaryActor.FateID != 0
                ? PrimaryActor.FateID == fate.ID
                : Info?.GroupType == BossModuleInfo.GroupType.ForayFATE && Info.NameID == fate.ID);
        if (matches)
        {
            var center = new WPos(fate.Center.XZ());
            if (_fateBounds == null || _fateID != fate.ID || !_fateCenter.AlmostEqual(center, 0.01f) || MathF.Abs(_fateRadius - fate.Radius) > 0.01f)
            {
                var (_, bitmap) = Obstacles.Find(fate.Center);
                var resolution = bitmap?.PixelSize ?? fate.Radius switch
                {
                    > 60f => 2f,
                    > 30f => 1f,
                    _ => 0.5f
                };
                _fateID = fate.ID;
                _fateCenter = center;
                _fateRadius = fate.Radius;
                _fateBounds = new(fate.Radius, resolution, true);
            }
        }

        if (_fateBounds != null)
        {
            Arena.Center = _fateCenter;
            Arena.Bounds = _fateBounds;
        }
        else
        {
            Arena.Center = PrimaryActor.Position;
        }
    }
}
