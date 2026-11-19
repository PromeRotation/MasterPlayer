using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision.Math;
using MasterPlayer.Spatial.Terrain;
using InteropGenerator.Runtime;

internal static unsafe class NativeSceneFixture
{
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static ColliderType Type(Collider* collider) => collider->LayoutObjectId == 1 ? ColliderType.Mesh : ColliderType.Box;
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void World(Collider* collider, Matrix4x4* world)
    {
        var box = (ColliderBox*)collider;
        *world = collider->LayoutObjectId == 1 ? Matrix4x4.Identity
            : Matrix4x4.CreateScale(box->Scale) * Matrix4x4.CreateTranslation(box->Translation);
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Bounds(Collider* collider, AABB* bounds) => *bounds = new() { Min = new(-10), Max = new(10) };
    [UnmanagedCallersOnly]
    private static CStringPointer Path(Resource* resource) => new((byte*)resource + 0x28);

    internal static void Verify() => Inspect(false);
    internal static void VerifyMutable() => Inspect(true);

    private static void Inspect(bool mutable)
    {
        var vtable = (void**)NativeMemory.AllocZeroed(25, (nuint)sizeof(void*));
        var scene = (Scene*)NativeMemory.AllocZeroed((nuint)sizeof(Scene));
        var mesh = (ColliderMesh*)NativeMemory.AllocZeroed((nuint)sizeof(ColliderMesh));
        var pcb = (MeshPCB*)NativeMemory.AllocZeroed((nuint)sizeof(MeshPCB));
        var inactive = (ColliderBox*)NativeMemory.AllocZeroed((nuint)sizeof(ColliderBox));
        var resource = (Resource*)NativeMemory.AllocZeroed((nuint)sizeof(Resource));
        var bytes = SceneTerrainTests.PcbNode();
        var node = (byte*)NativeMemory.Alloc((nuint)bytes.Length);
        TerrainSceneSnapshot? detached = null;
        try
        {
            vtable[18] = (delegate* unmanaged[MemberFunction]<Collider*, ColliderType>)&Type;
            vtable[1] = (delegate* unmanaged<Resource*, CStringPointer>)&Path;
            vtable[16] = (delegate* unmanaged[MemberFunction]<Collider*, Matrix4x4*, void>)&World;
            vtable[10] = (delegate* unmanaged[MemberFunction]<Collider*, AABB*, void>)&Bounds;
            *(void***)mesh = vtable; *(void***)inactive = vtable; *(void***)resource = vtable;
            "fixture/wall.pcb\0"u8.CopyTo(new Span<byte>((byte*)resource + 0x28, 80));
            mesh->Resource = resource;
            Marshal.Copy(bytes, 0, (nint)node, bytes.Length);
            pcb->RootNode = (MeshPCB.FileNode*)node;
            mesh->Mesh = (Mesh*)pcb; mesh->Loaded = true; mesh->TotalPrimitives = 2;
            var collider = (Collider*)mesh;
            collider->LayoutObjectId = 1; collider->VisibilityFlags = 1; collider->LayerMask = 1;
            ((Collider*)inactive)->LayoutObjectId = 2; ((Collider*)inactive)->LayerMask = 1;
            ((Node*)mesh)->NodeLink.Next = (Node*)inactive;
            scene->FirstCollider = (Collider*)mesh; scene->NumColliders = 2;
            var source = new NativeTerrainSceneSource();
            if (!source.CaptureScene(scene, Vector3.Zero, 8, out detached) || detached!.Instances.Length != 1)
                throw new InvalidOperationException("An inactive deep-dungeon collider was included or PCB capture failed.");
            var copied = source.CopiedBytes;
            if (source.Diagnostics is not { Complete: true, VisitedColliders: 2, Instances: 1, CopiedModels: 1 } initial
                || initial.CopiedBytes != bytes.Length || initial.CopyMilliseconds < 0)
                throw new InvalidOperationException("Native capture diagnostics lost the detached copy or active-instance counts.");
            if (!source.CaptureScene(scene, Vector3.Zero, 8, out var again) || source.CopiedBytes != copied
                || !ReferenceEquals(detached.Instances[0].Asset, again!.Instances[0].Asset))
                throw new InvalidOperationException($"The same collision resource was copied again: bytes {copied}->{source.CopiedBytes}, key {detached.Instances[0].Asset.Key}->{again?.Instances[0].Asset.Key}.");
            if (source.Diagnostics is not { Complete: true, CopiedModels: 0, CopiedBytes: 0, CacheHits: 1 })
                throw new InvalidOperationException("Warm capture diagnostics accumulated a previous copy instead of reporting this attempt.");
            if (mutable)
            {
                mesh->MemoryData = node;
                *(float*)(node + 60) = 2;
                if (!source.CaptureScene(scene, Vector3.Zero, 8, out var modified) || detached.SameGeometry(modified!)
                    || modified!.Instances[0].Asset.Mesh.Value.Vertices[1].X != 2)
                    throw new InvalidOperationException("A procedural mesh reused stale file geometry with the same resource, counts and address.");
                if (!source.CaptureScene(scene, Vector3.Zero, 8, out var stable) || !modified.SameGeometry(stable!))
                    throw new InvalidOperationException("Unchanged procedural geometry did not reuse its decoded asset.");
                *(float*)(node + 60) = 3;
                if (!source.CaptureScene(scene, Vector3.Zero, 8, out var changed) || modified.SameGeometry(changed!)
                    || changed!.Instances[0].Asset.Mesh.Value.Vertices[1].X != 3 || modified.Instances[0].Asset.Mesh.Value.Vertices[1].X != 2)
                    throw new InvalidOperationException("An in-place procedural mesh update was missed or mutated an old snapshot.");
            }
            new Span<byte>(node, bytes.Length).Clear();
        }
        finally
        {
            NativeMemory.Free(node); NativeMemory.Free(resource); NativeMemory.Free(inactive); NativeMemory.Free(pcb);
            NativeMemory.Free(mesh); NativeMemory.Free(scene); NativeMemory.Free(vtable);
        }
        var result = detached!.Instances[0].Asset.Mesh.Value;
        if (result.Triangles.Length != 2 || result.Vertices[4] != new Vector3(2, 0, 0))
            throw new InvalidOperationException("A detached scene retained native model pointers.");
    }

    internal static void VerifyNavigation()
    {
        var vtable = (void**)NativeMemory.AllocZeroed(25, (nuint)sizeof(void*));
        var scene = (Scene*)NativeMemory.AllocZeroed((nuint)sizeof(Scene));
        var floor = (ColliderBox*)NativeMemory.AllocZeroed((nuint)sizeof(ColliderBox));
        var door = (ColliderBox*)NativeMemory.AllocZeroed((nuint)sizeof(ColliderBox));
        try
        {
            vtable[18] = (delegate* unmanaged[MemberFunction]<Collider*, ColliderType>)&Type;
            vtable[16] = (delegate* unmanaged[MemberFunction]<Collider*, Matrix4x4*, void>)&World;
            vtable[10] = (delegate* unmanaged[MemberFunction]<Collider*, AABB*, void>)&Bounds;
            *(void***)floor = vtable; *(void***)door = vtable;
            floor->Scale = new(30, 1, 30); floor->Translation = new(0, -1, 0);
            door->Scale = new(.01f, 2, 30); door->Translation = new(1.8f, 2, 0);
            var a = (Collider*)floor; var b = (Collider*)door;
            a->LayoutObjectId = 3; b->LayoutObjectId = 2;
            a->LayerMask = b->LayerMask = 1;
            a->VisibilityFlags = b->VisibilityFlags = 1;
            a->ObjectMaterialValue = 0x7000; b->ObjectMaterialValue = 0x1400;
            ((Node*)floor)->NodeLink.Next = (Node*)door;
            scene->FirstCollider = a; scene->NumColliders = 2;
            var source = new NativeTerrainSceneSource();
            TerrainSceneSnapshot Capture()
            {
                if (!source.CaptureScene(scene, Vector3.Zero, 8, out var snapshot))
                    throw new InvalidOperationException("A native door snapshot was unavailable.");
                return snapshot!;
            }
            bool CanReach(TerrainSceneSnapshot snapshot) => SceneTerrainBuilder.Build(snapshot, Vector3.Zero, 8, default).Boundary.Contains(new(4, 0, 0));
            var closed = Capture();
            if (closed.Instances.Length != 2 || CanReach(closed))
                throw new InvalidOperationException("An active conditional deep-dungeon door was discarded by ray-material filtering.");
            b->VisibilityFlags = 0;
            var open = Capture();
            if (open.Instances.Length != 1 || !CanReach(open) || CanReach(closed))
                throw new InvalidOperationException("Opening the door failed to update navigation or mutated an earlier snapshot.");
            b->VisibilityFlags = 1;
            door->Translation = new(6, 2, 0);
            if (!CanReach(Capture())) throw new InvalidOperationException("A translated door left a phantom wall.");
            door->Translation = new(1.8f, 2, 0);
            b->ObjectMaterialValue = 0x202411;
            if (CanReach(Capture())) throw new InvalidOperationException("An active overworld boundary collider was omitted.");
        }
        finally
        {
            NativeMemory.Free(door); NativeMemory.Free(floor); NativeMemory.Free(scene); NativeMemory.Free(vtable);
        }
    }
}
