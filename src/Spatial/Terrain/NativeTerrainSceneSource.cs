using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision.Math;

namespace MasterPlayer.Spatial.Terrain;

internal sealed unsafe class NativeTerrainSceneSource : ITerrainSceneSource
{
    private const int MAX_MESH_BYTES = 64 * 1024 * 1024;
    private const int CACHE_BYTES = 128 * 1024 * 1024;
    private readonly Dictionary<string, TerrainMeshAsset> meshes = new(StringComparer.Ordinal);
    private readonly Queue<string> cacheOrder = new();
    private readonly Dictionary<(nint Mesh, nint Root, int Primitives, int Children), TerrainMeshAsset> pendingProcedural = new();
    private nint pendingScene;
    private int cacheBytes;
    internal long CopiedBytes { get; private set; }
    public TerrainCaptureDiagnostics? Diagnostics { get; private set; }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool TryAcquireSRWLockShared(nint* srwLock);
    [DllImport("kernel32.dll")]
    private static extern void ReleaseSRWLockShared(nint* srwLock);

    public bool TryCapture(Vector3 origin, float radius, out TerrainSceneSnapshot? snapshot)
    {
        var started = Stopwatch.GetTimestamp();
        Diagnostics = new();
        snapshot = null;
        var framework = Framework.Instance();
        var module = framework == null ? null : framework->BGCollisionModule;
        if (module == null || module->ShuttingDown || module->LoadInProgressCounter != 0 || module->SceneManager == null)
        {
            pendingProcedural.Clear();
            return false;
        }
        if (!TryAcquireSRWLockShared(&module->UpdateTaskLock)) return false;
        var locked = Stopwatch.GetTimestamp();
        try
        {
            var wrapper = module->SceneManager->FirstScene;
            return wrapper != null && wrapper->Scene != null && wrapper->Scene->NumLoading == 0
                && CaptureScene(wrapper->Scene, origin, radius, out snapshot);
        }
        finally
        {
            ReleaseSRWLockShared(&module->UpdateTaskLock);
            Diagnostics = (Diagnostics ?? new()) with
            {
                TotalMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                LockMilliseconds = Stopwatch.GetElapsedTime(locked).TotalMilliseconds,
            };
        }
    }

    // The caller holds the scene read lock. Nothing referencing native memory leaves this method.
    internal bool CaptureScene(Scene* scene, Vector3 origin, float radius, out TerrainSceneSnapshot? snapshot)
    {
        snapshot = null;
        if (pendingScene != (nint)scene)
        {
            pendingProcedural.Clear();
            pendingScene = (nint)scene;
        }
        var start = Stopwatch.GetTimestamp();
        var instances = new List<TerrainSceneSnapshot.Instance>();
        var needed = new HashSet<string>(StringComparer.Ordinal);
        var incomplete = false;
        var copied = false;
        var visited = 0;
        var cacheHits = 0;
        var copiedModels = 0;
        var hashedModels = 0;
        long copiedBytes = 0;
        double copyMilliseconds = 0, hashMilliseconds = 0;
        try
        {
            foreach (var collider in scene->Colliders)
            {
                ++visited;
                if (!TerrainSceneSnapshot.IsActive(collider->VisibilityFlags, collider->LayerMask)) continue;
                var type = collider->GetColliderType();
                if (type == ColliderType.Streamed) continue;
                var bounds = default(AABB);
                collider->GetWorldBB(&bounds);
                if (bounds.Max.X < origin.X - radius - 2 || bounds.Min.X > origin.X + radius + 2
                    || bounds.Max.Z < origin.Z - radius - 2 || bounds.Min.Z > origin.Z + radius + 2
                    || bounds.Max.Y < origin.Y - 64 || bounds.Min.Y > origin.Y + 64) continue;
                var world = default(Matrix4x4);
                collider->GetWorldTransform(&world);
                TerrainMeshAsset? asset;
                var mask = ulong.MaxValue;
                if (type == ColliderType.Mesh)
                {
                    var mesh = (ColliderMesh*)collider;
                    if (!mesh->Loaded || mesh->Mesh == null) { incomplete = true; continue; }
                    if (mesh->MeshIsSimple) throw new NotSupportedException("暂不支持 MeshSimple 场景碰撞体");
                    var root = ((MeshPCB*)mesh->Mesh)->RootNode;
                    if (root == null) { incomplete = true; continue; }
                    var path = mesh->MemoryData != null || mesh->Resource == null ? "" : mesh->Resource->GetPath().ToString();
                    var procedural = path.Length == 0;
                    var identity = ((nint)mesh, (nint)root, mesh->TotalPrimitives, mesh->TotalChildren);
                    var key = $"{path}/{mesh->TotalPrimitives}/{mesh->TotalChildren}";
                    var cached = procedural ? pendingProcedural.TryGetValue(identity, out asset) : meshes.TryGetValue(key, out asset);
                    if (!cached)
                    {
                        if (copied && Stopwatch.GetElapsedTime(start).TotalMilliseconds >= 1) { incomplete = true; continue; }
                        var copyStart = Stopwatch.GetTimestamp();
                        var bytes = CopyNodes(root, mesh->TotalChildren);
                        copyMilliseconds += Stopwatch.GetElapsedTime(copyStart).TotalMilliseconds;
                        ++copiedModels;
                        copiedBytes += bytes.Length;
                        copied = true;
                        CopiedBytes += bytes.Length;
                        if (procedural)
                        {
                            var hashStart = Stopwatch.GetTimestamp();
                            key = $"memory/{Convert.ToHexString(SHA256.HashData(bytes))}";
                            hashMilliseconds += Stopwatch.GetElapsedTime(hashStart).TotalMilliseconds;
                            ++hashedModels;
                        }
                        if (!meshes.TryGetValue(key, out asset))
                        {
                            asset = new(key, bytes);
                            meshes.Add(key, asset); cacheOrder.Enqueue(key);
                            cacheBytes += bytes.Length;
                        }
                        if (procedural) pendingProcedural[identity] = asset;
                    }
                    else ++cacheHits;
                    needed.Add(asset!.Key);
                    mask = collider->ObjectMaterialMask;
                }
                else
                {
                    asset = type switch
                    {
                        ColliderType.Box => TerrainPrimitives.Box,
                        ColliderType.Cylinder => TerrainPrimitives.Cylinder,
                        ColliderType.Sphere => TerrainPrimitives.Sphere,
                        ColliderType.Plane => TerrainPrimitives.Plane,
                        ColliderType.PlaneTwoSided => TerrainPrimitives.TwoSidedPlane,
                        _ => throw new NotSupportedException($"未知场景碰撞体类型：{type}"),
                    };
                }
                if (!Finite(world)) throw new InvalidDataException("场景碰撞体变换无效");
                instances.Add(new(collider->LayoutObjectId, asset, world, collider->ObjectMaterialValue, mask));
            }
            var remaining = cacheOrder.Count;
            while (cacheBytes > CACHE_BYTES && remaining-- > 0)
            {
                var key = cacheOrder.Dequeue();
                if (needed.Contains(key)) { cacheOrder.Enqueue(key); continue; }
                cacheBytes -= meshes[key].Bytes;
                meshes.Remove(key);
            }
            if (incomplete) return false;
            pendingProcedural.Clear();
            snapshot = new(instances.OrderBy(i => i.Id).ThenBy(i => i.Asset.Key, StringComparer.Ordinal).ToArray());
            return true;
        }
        finally
        {
            Diagnostics = new()
            {
                Complete = snapshot != null,
                TotalMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds,
                VisitedColliders = visited,
                Instances = instances.Count,
                CacheHits = cacheHits,
                CopiedModels = copiedModels,
                HashedModels = hashedModels,
                CopiedBytes = copiedBytes,
                CachedBytes = cacheBytes,
                CopyMilliseconds = copyMilliseconds,
                HashMilliseconds = hashMilliseconds,
            };
        }
    }

    private static byte[] CopyNodes(MeshPCB.FileNode* root, int children)
    {
        var pending = new Stack<int>();
        var visited = new HashSet<int>();
        var extent = 0;
        pending.Push(0);
        while (pending.TryPop(out var offset))
        {
            if (offset < 0 || offset > MAX_MESH_BYTES - 48 || !visited.Add(offset) || visited.Count > Math.Clamp(children + 1, 1, 200000))
                throw new InvalidDataException("场景碰撞模型节点范围无效");
            var node = (MeshPCB.FileNode*)((byte*)root + offset);
            if (node->NumVertsRaw + node->NumVertsCompressed > 256) throw new InvalidDataException("场景碰撞模型顶点数量无效");
            extent = Math.Max(extent, checked(offset + 48 + node->NumVertsRaw * 12 + node->NumVertsCompressed * 6 + node->NumPrims * 12));
            if (extent > MAX_MESH_BYTES) throw new InvalidDataException("场景碰撞模型过大");
            if (node->Child1Offset != 0) pending.Push(checked(offset + node->Child1Offset));
            if (node->Child2Offset != 0) pending.Push(checked(offset + node->Child2Offset));
        }
        return new ReadOnlySpan<byte>(root, extent).ToArray();
    }

    private static bool Finite(Matrix4x4 m) => TerrainMesh.Finite(new(m.M11, m.M12, m.M13))
        && TerrainMesh.Finite(new(m.M21, m.M22, m.M23)) && TerrainMesh.Finite(new(m.M31, m.M32, m.M33))
        && TerrainMesh.Finite(new(m.M41, m.M42, m.M43)) && m.M14 == 0 && m.M24 == 0 && m.M34 == 0 && m.M44 == 1;
}
