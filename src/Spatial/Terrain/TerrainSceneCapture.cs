using System.Numerics;

namespace MasterPlayer.Spatial.Terrain;

internal sealed record TerrainSceneCapture(TerrainSceneSnapshot Scene, Vector3 Origin, float Radius,
    DateTime CapturedAtUtc, TerrainCaptureDiagnostics? CaptureDiagnostics, SceneTerrainBuilder.Result Result);
