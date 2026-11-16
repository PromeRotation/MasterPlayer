using System.Runtime.CompilerServices;

namespace MasterPlayer.Mechanics.Crucible;

// f1x1/level/vfx.lgb places b4157 (circle), b4156 (square), b4155 (rectangle).
// Their AVFX ground meshes start at radius/half-width 20 (rectangle half-height 15)
// and extend about 3y outwards. ARR MapEffect indices 0/1/2 enable these same fields.
sealed class CrucibleArena(WPos center, byte? index, bool circular, float halfWidth, float halfHeight, float boundaryWidth = 3f, float wallMargin = 0f, ArenaBounds? customDisplay = null, ArenaBounds? customSafe = null)
{
    public readonly WPos Center = center;
    public readonly byte? Index = index;
    public readonly bool Circular = circular;
    public readonly float HalfWidth = halfWidth, HalfHeight = halfHeight;
    public readonly float BoundaryWidth = boundaryWidth;
    public readonly ArenaBounds SafeBounds = customSafe ?? (circular ? new ArenaBoundsCircle(halfWidth - wallMargin) : new ArenaBoundsRect(halfWidth - wallMargin, halfHeight - wallMargin));
    public readonly ArenaBounds DisplayBounds = customDisplay ?? (circular ? new ArenaBoundsCircle(halfWidth + boundaryWidth) : new ArenaBoundsRect(halfWidth + boundaryWidth, halfHeight + boundaryWidth));
    public readonly Components.GenericAOEs.AOEInstance[] Boundary = boundaryWidth <= 0f ? [] : circular
        ? [new(new AOEShapeDonut(halfWidth, halfWidth + boundaryWidth), center)]
        : [new(new AOEShapeRect(boundaryWidth, halfWidth + boundaryWidth), center + new WDir(0, halfHeight)),
           new(new AOEShapeRect(boundaryWidth, halfWidth + boundaryWidth), center - new WDir(0, halfHeight), 180f.Degrees()),
           new(new AOEShapeRect(boundaryWidth, halfHeight), center + new WDir(halfWidth, 0), 90f.Degrees()),
           new(new AOEShapeRect(boundaryWidth, halfHeight), center - new WDir(halfWidth, 0), -90f.Degrees())];

    private static readonly CrucibleArena[] Fields =
    [
        new(new(120, -420), 0, true, 20, 20),
        new(new(120, 0), 1, false, 20, 20),
        new(new(520, 0), 2, false, 20, 15),
        // f1x1 LVD_STAGE_BOSS collision box 12291199: center (520.1,-420),
        // half extents (18.7,20.2). Solid walls, not an electric-fence MapEffect.
        // Keep navigation 0.7y inside the box to avoid choosing wall-adjacent cells.
        new(new(520.1f, -420), null, false, 18.7f, 20.2f, boundaryWidth: 0f, wallMargin: 0.7f),
        // f1x4 LVD_STAGE_BOSS collision 12459075, cylinder (TriggerBoxShape 3).
        // High first-board dragon: solid circular wall, radius 19.75y.
        new(new(920, -420), null, true, 19.75f, 19.75f, boundaryWidth: 0f, wallMargin: 0.7f)
    ];

    // f1x2 b4222 uses the same 40x30 footprint as b4155, but MapEffect 7.
    private static readonly CrucibleArena Wyvern = new(new(520, 0), 7, false, 20, 15);
    // The desert terrain has no LGB collision box. Keep a conservative interior
    // envelope until its irregular terrain collision is reconstructed; do not
    // claim that the first board's solid-wall box belongs to this encounter.
    private static readonly CrucibleArena Desert = new(new(520, -420), null, false, 18, 19, boundaryWidth: 0, wallMargin: 0.7f);

    // f1x3 reverses the circle/square MapEffect indices. Its other indices
    // control encounter props and must not toggle another board's boundary.
    private static readonly CrucibleArena ThirdSquare = new(new(120, 0), 0, false, 20, 20);
    private static readonly CrucibleArena ThirdCircle = new(new(120, -420), 1, true, 20, 20);
    // f1x3 uses the same gim05 SGB / b4228 fire mesh as f1x5, at unit scale.
    // Its 3x5 LVD trigger envelope includes burning gaps, not only safe floor.
    private static readonly CrucibleArena ThirdFinal = new(new(520, -420), 22, false, 15, 25, boundaryWidth: 0,
        customDisplay: CrucibleLaudaFloor.Display, customSafe: CrucibleLaudaFloor.Safe);

    // b4228yuka1_v2's fire mesh leaves a 20x40 core and six 5x5 extensions.
    // LGB trigger cells cover the surrounding fire too; they are not the floor.
    private static readonly CrucibleArena Lauda = new(new(520, -420), 42, false, 15, 25, boundaryWidth: 0,
        customDisplay: CrucibleLaudaFloor.Display, customSafe: CrucibleLaudaFloor.Safe);

    private static readonly CrucibleArena Treant = new(new(120, -420), 0, true, 20, 20, wallMargin: 0.3f);

    public static CrucibleArena? Find(WPos position, uint nameID = 0) => nameID switch
    {
        >= 14618 and <= 14622 => Treant,
        14549 => Wyvern,
        14561 or 14562 => Desert,
        >= 14564 and <= 14571 or >= 14580 and <= 14582 => ThirdSquare,
        >= 14572 and <= 14579 or >= 14583 and <= 14586 => ThirdCircle,
        >= 14592 and <= 14595 => ThirdFinal,
        >= 14693 and <= 14699 => Lauda,
        _ => Fields.FirstOrDefault(f => (position - f.Center).LengthSq() < 60f * 60f)
    };
}

sealed class CrucibleBoundary(BossModule module) : Components.GenericAOEs(module, warningText: "离开场地边缘的电网！")
{
    private readonly CrucibleArena? _field = CrucibleArena.Find(module.PrimaryActor.Position, module.PrimaryActor.NameID);
    private DateTime _returnUntil;
    private bool _enteredReturn;
    private bool _active = true; // The enable packet commonly precedes the boss spawn/module construction.

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _active && _field != null ? _field.Boundary : [];

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (Module.Group == 22 && spell.Action.ID == 48667 || Module.Group == 14 && spell.Action.ID == 48471)
        {
            _returnUntil = WorldState.CurrentTime.AddSeconds(12);
            _enteredReturn = false;
        }
    }
    public override void OnActorDeath(Actor actor)
    {
        if (actor == WorldState.Player || actor == Module.PrimaryActor) { _returnUntil = default; _enteredReturn = false; }
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var firstBoundaryZone = hints.ForbiddenZones.Count;
        base.AddAIHints(slot, actor, assignment, hints);
        if (_field != null)
            for (var i = firstBoundaryZone; i < hints.ForbiddenZones.Count; ++i)
                hints.ForbiddenZones[i] = hints.ForbiddenZones[i] with { ArenaMarginInterior = _field.SafeBounds };
        if (actor.IsDeadOrDestroyed) { _returnUntil = default; _enteredReturn = false; return; }
        if (_field == null) return;
        // run2323：夜魔人圆场这里 _active 是 false（没有「离开电网」提示），人被挥舞击退到 r=22.3 后照样
        // NoReachableSafeCell 站了 18s。新的回场租约不看 _active：只要人越过了场边就拉回来。
        var legacy = Module.Group is 14 or 22 or 26;
        if (!_active && legacy) return;
        var offset = actor.Position - _field.Center;
        // The electric display envelope is walkable floor, not a physical wall/hole.
        // Retire this lease after reaching the ordinary eroded interior.
        var inInterior = Module.Group == 22 ? offset.Length() <= 19 : Math.Max(Math.Abs(offset.X), Math.Abs(offset.Z)) <= 19;
        // run1884: Panic (4167) at the gargoyle's second five-hit ran the player into the fence;
        // the solver cannot start outside the eroded bounds, so MP released and the player bled 15s there.
        if (Module.Group == 26 && !inInterior && _returnUntil <= WorldState.CurrentTime
            && actor.FindStatus(CrucibleMiasma.Panic) == null && _field.DisplayBounds.Contains(offset))
        {
            _returnUntil = WorldState.CurrentTime.AddSeconds(12);
            _enteredReturn = false;
        }
        // run2318：夜魔人挥舞、杜尔迦气化炸弹把人击退进电网后同样是 NoReachableSafeCell，人在电网里站到死
        // （夜魔人 30s、杜尔迦 8s）。其它场次人站进电网时也给同样的回场租约；只在真的越过场边后触发，
        // 不影响贴边站位（杜尔迦角落放炸弹）。
        if (!legacy)
        {
            var past = _field.Circular ? offset.Length() - _field.HalfWidth
                : Math.Max(Math.Abs(offset.X) - _field.HalfWidth, Math.Abs(offset.Z) - _field.HalfHeight);
            inInterior = past <= -1;
            if (_field.Boundary.Length > 0 && past > 0.3f && _returnUntil <= WorldState.CurrentTime && _field.DisplayBounds.Contains(offset))
            {
                _returnUntil = WorldState.CurrentTime.AddSeconds(12);
                _enteredReturn = false;
            }
        }
        if (_returnUntil <= WorldState.CurrentTime) return;
        if (inInterior)
        {
            // Native action effect can precede the player's displaced position update.
            // Await landing until the absolute deadline; only a completed return retires it.
            if (_enteredReturn) { _returnUntil = default; _enteredReturn = false; }
            return;
        }
        if (!_field.DisplayBounds.Contains(offset)) return;
        _enteredReturn = true;
        hints.PathfindMapBounds = _field.DisplayBounds;
        hints.ArenaSafetyMargin = 0;
        hints.GoalDirectedTemporalPlanning = true;
        var goal = Module.Group == 22 ? _field.Center + offset.Normalized() * 18.0f
            : legacy ? _field.Center + new WDir(Math.Clamp(offset.X,-18.0f,18.0f),Math.Clamp(offset.Z,-18.0f,18.0f))
            : _field.Circular ? _field.Center + offset.Normalized() * (_field.HalfWidth - 2)
            : _field.Center + new WDir(Math.Clamp(offset.X, 2 - _field.HalfWidth, _field.HalfWidth - 2), Math.Clamp(offset.Z, 2 - _field.HalfHeight, _field.HalfHeight - 2));
        // B26: returning into a live pitch residue re-panics on the next tick and runs back into the fence.
        if (Module.FindComponent<CrucibleGargoyleResidue>() is { } residue && residue.Covers(goal, 1))
        {
            var from = goal;
            goal = Enumerable.Range(0, 144).Select(i => _field.Center + new WDir(i < 36 ? -18 + i : i < 72 ? 18 : i < 108 ? 18 - (i - 72) : -18,
                    i < 36 ? -18 : i < 72 ? -18 + (i - 36) : i < 108 ? 18 : 18 - (i - 108)))
                .Where(p => !residue.Covers(p, 1)).DefaultIfEmpty(goal).MinBy(p => (p - from).LengthSq());
        }
        hints.GoalPoints.Add((goal,.5f,100));
        // Existing electric-fence ForbiddenZones and all AOE/obstacles are unchanged.
    }

    public override void OnMapEffect(byte index, uint state)
    {
        if (index == _field?.Index && state is 0x00020001 or 0x00080004)
        {
            _active = state == 0x00020001;
            if (!_active) { _returnUntil = default; _enteredReturn = false; }
        }
    }
}
