using System.Runtime.CompilerServices;

namespace MasterPlayer.Mechanics.Crucible;

// f1x1/level/vfx.lgb places b4157 (circle), b4156 (square), b4155 (rectangle).
// Their AVFX ground meshes start at radius/half-width 20 (rectangle half-height 15)
// and extend about 3y outwards. ARR MapEffect indices 0/1/2 enable these same fields.
sealed class CrucibleArena(WPos center, byte? index, bool circular, float halfWidth, float halfHeight, float boundaryWidth = 3f, float wallMargin = 0f, ArenaBounds? customDisplay = null, ArenaBounds? customSafe = null)
{
    public readonly WPos Center = center;
    public readonly byte? Index = index;
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
    private bool _active = true; // The enable packet commonly precedes the boss spawn/module construction.

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _active && _field != null ? _field.Boundary : [];

    public override void OnMapEffect(byte index, uint state)
    {
        if (index == _field?.Index && state is 0x00020001 or 0x00080004)
            _active = state == 0x00020001;
    }
}
