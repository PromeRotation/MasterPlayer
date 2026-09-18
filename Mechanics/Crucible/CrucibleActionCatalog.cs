using System.Collections.Frozen;
using MasterPlayer.Dodge;
using MasterPlayer.Mechanics.Crucible.CriticalEngagement;
using PromeRotation.Spatial.Geometry;
using PromeRotation.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Crucible;

internal static class CrucibleActionCatalog
{
    public static uint[] ClaimedActions => [46864u, 46866u, 46867u, 46868u, 46869u, 46870u, 46871u, 46873u, 46874u, 46875u, 46876u, 46877u, 46878u, 46879u, 46880u, 46882u, 46884u, 46886u, 46889u, 46890u, 46892u, 46893u, 46894u, 46895u, 46897u, 46898u, 46900u, 46901u, 46902u, 46903u, 46904u, 46905u, 46906u, 46907u, 46908u, 46909u, 46910u, 46912u, 46913u, 46915u, 46916u, 46917u, 46918u, 46919u, 46920u, 46921u, 46922u, 46923u, 46924u, 46925u, 46926u, 46927u, 46928u, 46929u, 46930u, 46932u, 46933u, 46935u, 48123u, 48125u, 48127u, 48128u, 48130u, 48132u, 48134u, 48137u, 48138u, 48140u, 48142u, 48143u, 48153u, 48155u, 48157u, 48167u, 48168u, 48172u, 48175u, 48178u, 48180u, 48182u, 48183u, 48186u, 48189u, 48190u, 48192u, 48193u, 48194u, 48196u, 48203u, 48206u, 48210u, 48212u, 48214u, 48218u, 48219u, 48221u, 48222u, 48223u, 48225u, 48227u, 48229u, 48235u, 48239u, 48242u, 48247u, 48251u, 48462u, 48464u, 48471u, 48473u, 48477u, 48479u, 48481u, 48483u, 48485u, 48489u, 48491u, 48492u, 48494u, 48495u, 48497u, 48499u, 48501u, 48506u, 48508u, 48509u, 48510u, 48512u, 48519u, 48555u, 48556u, 48557u, 48560u, 48561u, 48562u, 48563u, 48564u, 48565u, 48566u, 48568u, 48570u, 48575u, 48579u, 48581u, 48582u, 48588u, 48589u, 48597u, 48603u, 48605u, 48607u, 48611u, 48614u, 48620u, 48621u, 48626u, 48656u, 48657u, 48658u, 48659u, 48660u, 48664u, 48665u, 48667u, 48668u, 48669u, 48671u, 48672u, 48673u, 48674u, 48675u, 48679u, 48680u, 48682u, 48683u, 48685u, 48689u, 48690u, 48691u, 48693u, 48697u, 48699u, 48704u, 48708u, 48712u, 48714u, 48715u, 48716u, 48717u, 48718u, 48719u, 48720u, 48721u, 48724u, 48725u, 48727u, 48729u, 48735u, 48736u, 48737u, 48738u, 48740u, 48742u, 48747u, 48748u, 48750u, 48751u, 48753u, 48755u, 48757u, 48758u, 48759u, 48760u, 48761u, 48763u, 48766u, 48768u, 48771u, 48772u, 48773u, 48774u, 48775u, 48778u, 48779u, 48780u, 48781u, 48782u, 48783u, 48785u, 48786u, 48790u, 48791u, 48792u, 48793u, 48794u, 48796u, 48799u, 48800u, 48801u, 48802u, 48807u, 48808u, 48809u, 48810u, 48812u, 48814u, 48816u, 48819u, 48821u, 48828u, 49186u, 49189u, 49191u, 49193u, 49194u, 49195u, 49196u, 49197u, 49198u, 49200u, 49203u, 49204u, 49206u, 49207u, 49208u, 49209u, 49210u, 49211u, 49212u, 49213u, 49214u, 49231u, 49233u, 49236u, 49237u, 49238u, 49240u, 49241u, 49242u, 49243u, 49244u, 49245u, 49246u, 49247u, 49248u, 49249u, 49250u, 49252u, 49253u, 49254u, 49260u, 49261u, 49265u, 49266u, 49267u, 49268u, 49270u, 49271u, 49272u, 49273u, 49274u, 49277u, 49278u, 49279u, 49281u, 49284u, 49286u, 49287u, 49289u, 49291u, 49293u, 49295u, 49296u, 49297u, 49298u, 49300u, 49302u, 49304u, 49306u, 49308u, 49315u, 49316u, 49318u, 49319u, 49320u, 49327u, 49329u, 49331u, 49332u, 49335u, 49337u, 49339u, 49341u, 49343u, 49344u, 49348u, 49350u, 49351u, 49353u, 49359u, 49361u, 49363u, 49366u, 49367u, 49368u, 49370u, 49373u, 49374u, 49375u, 49376u, 49377u, 49378u, 49379u, 49381u, 49383u, 49384u, 49385u, 49387u, 49394u, 49395u, 49397u, 49399u, 49400u, 49408u, 49410u, 49411u, 49415u, 49418u, 49419u, 49420u, 49421u, 49422u, 49424u, 49425u, 49426u, 49428u, 49429u, 49431u, 49434u, 49435u, 49443u, 49446u, 49449u, 49451u, 49453u, 49457u, 49460u, 49462u, 49464u, 49468u, 49469u, 49470u, 49472u, 49477u, 49480u, 49483u, 49485u, 49486u, 49489u, 49688u, 49886u, 50411u, 50413u, 50452u, 50465u, 50532u, 50540u, 50541u, 50543u, 50544u, 50649u, 50662u, 50688u, 50693u, 50695u, 50696u, 50747u, 50750u, 50758u, 50793u, 50846u, 50848u, 50932u, 50939u, 50943u];
    private static readonly FrozenSet<uint> Claimed = ClaimedActions.ToFrozenSet();
    private static readonly FrozenDictionary<uint, AoeShapeDescriptor> Shapes = Build();
    public static bool IsClaimed(uint action) => Claimed.Contains(action);
    public static bool TryGetShape(uint action, out AoeShapeDescriptor? descriptor) => Shapes.TryGetValue(action, out descriptor);
    private static FrozenDictionary<uint, AoeShapeDescriptor> Build()
    {
        var result = new Dictionary<uint, AoeShapeDescriptor>();
        for (var group = 1; group <= 45; ++group)
        {
            var module = new BossModule(new WorldState(), new Actor(), group);
            foreach (var component in module.Components.OfType<ReplayValidatedCastAOEs>())
                foreach (var id in Claimed)
                    if (component is not CrucibleVoidFlare && component.Describe(id) is { } c)
                    {
                        AoeShapeDescriptor descriptor = c.Shape switch
                        {
                            AOEShapeCircle circle => new(AoeShapeKind.Circle, circle.Radius, 0, !c.LocationTargeted, 0.3f),
                            AOEShapeDonut donut => new(AoeShapeKind.Donut, donut.InnerRadius, donut.OuterRadius, !c.LocationTargeted, 0.3f),
                            AOEShapeCone cone => new(AoeShapeKind.Fan, cone.Radius, cone.HalfAngle.Rad * 2, !c.LocationTargeted, 0.3f),
                            AOEShapeRect rect when rect.LengthBack == 0 => new(AoeShapeKind.RayRectangle, rect.HalfWidth * 2, rect.LengthFront, !c.LocationTargeted, 0.3f),
                            _ => new(AoeShapeKind.Rectangle, 0, 0, !c.LocationTargeted, 0.3f) { ExactTemplate = new MechanicShapeTemplate(c.Shape) }
                        };
                        result[id] = descriptor with { UseCastRotation = true, UseSourcePosition = !c.LocationTargeted };
                    }
        }
        // Specialized state machines: only their directly observed cast geometry has a generic fallback.
        result[48690] = new(AoeShapeKind.RayRectangle, 12, 45, false, 0.5f) { UseCastRotation = true };
        result[48210] = result[48212] = new(AoeShapeKind.Fan, 40, MathF.PI / 3, false, 0.3f) { UseCastRotation = true };
        result[49483] = new(AoeShapeKind.RayRectangle, 5, 50, false, 0.3f) { UseCastRotation = true };
        result[48720] = new(AoeShapeKind.Circle, 13, 0, false, 0.3f) { UseCastRotation = true };
        result[48721] = new(AoeShapeKind.Donut, 6, 30, false, 0.3f) { UseCastRotation = true };
        result[48235] = new(AoeShapeKind.Circle, 12, 0, true, 0.3f) { UseCastRotation = true, UseSourcePosition = true };
        result[48130] = new(AoeShapeKind.RayRectangle, 8, 0, false, 0.3f) { IsCharge = true };
        result[48673] = result[48675] = new(AoeShapeKind.Fan, 50, MathF.PI / 2, false, 0.3f) { UseCastRotation = true };
        result[48562] = new(AoeShapeKind.Circle, 12, 0, false, 0.3f) { UseCastRotation = true };
        return result.ToFrozenDictionary();
    }
    private sealed record MechanicShapeTemplate(AOEShape Shape) : GeometryTemplate
    {
        public override GeometryKind Kind => GeometryKind.LocalPolygon;
        public override IGeometryShape Resolve(System.Numerics.Vector3 position, float rotation, float? lengthOverride)
            => Shape.Geometry(WPos.FromVec3(position), new(rotation), position.Y);
    }
}
