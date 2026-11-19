using System.Numerics;

namespace MasterPlayer.Spatial;

// Direct-traversal result contract; the full runtime also loads unrelated native terrain services.
internal sealed record DirectTraversalExecution(IReadOnlyList<Vector3> Path, int CurrentIndex,
    bool HasControl, bool Rejected, bool InputLocked, DirectTraversalMotion? Motion = null);
