using System.Numerics;

namespace MasterPlayer.Drawing;

// Resolved multipart shapes carry their fill separately from boundary contours.
internal interface ITriangulatedDrawingGeometry
{
    IReadOnlyList<IReadOnlyList<Vector3>>? FillPolygons { get; }
}
