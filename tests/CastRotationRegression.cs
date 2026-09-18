using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using MasterPlayer.Scripting;
using PromeRotation.LogSystem;
using NativeBattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;

internal static class CastRotationRegression
{
    public static void Run(Action<string, Action> run)
    {
        run("cast packet direction wins over the current cast and ordinary actor rotation", () =>
        {
            using var actor = new CastActor { Rotation = -2.1f };
            var e = Cast() with { Raw = Packet(1.2f), SourceRotation = 0.3f };
            Check(CastRotation.TryGet(e, out var rotation) && rotation == 1.2f,
                "The event must retain its packet direction, even when the live actor has another direction.");
        });

        run("queued cast direction survives cast completion and actor disappearance", () =>
        {
            using var actor = new CastActor { IsCasting = false, ActionId = 1 };
            var e = Cast() with { Raw = Packet(-0.7f) };
            Check(CastRotation.TryGet(e, out var completed) && completed == -0.7f,
                "A completed or replaced native cast must not invalidate the captured packet.");
            actor.Available = false;
            Check(CastRotation.TryGet(e, out var removed) && removed == -0.7f,
                "A missing helper must not discard its already captured cast direction.");
        });

        run("zero is a valid captured cast direction", () =>
        {
            using var actor = new CastActor { Available = false };
            Check(CastRotation.TryGet(Cast() with { Raw = Packet(0f) }, out var rotation) && rotation == 0f,
                "North/south aligned packet values must not be treated as missing.");
        });

        run("events without a captured packet retain matching native cast fallback", () =>
        {
            using var actor = new CastActor { Rotation = 0.9f };
            Check(CastRotation.TryGet(Cast(), out var rotation) && rotation == 0.9f,
                "Older events without Raw still need the native CastRotation fallback.");
            Check(CastRotation.TryGet(Cast() with { Raw = new { PacketFacing = -0.4f } }, out rotation) && rotation == 0.9f,
                "An unrelated Raw object must not be interpreted as the host capture contract.");
        });

        run("non-finite captured directions fall back only to a valid matching cast", () =>
        {
            using var actor = new CastActor { Rotation = 0.8f };
            foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var e = Cast() with { Raw = Packet(invalid), SourceRotation = 1.4f };
                Check(CastRotation.TryGet(e, out var rotation) && rotation == 0.8f,
                    "A malformed packet direction must not override valid native direction.");
                actor.Available = false;
                Check(!CastRotation.TryGet(e, out _), "Invalid packet plus missing native must fail explicitly.");
                actor.Available = true;
            }
        });

        run("missing or invalid locked directions never use SourceRotation", () =>
        {
            using var actor = new CastActor();
            var e = Cast() with { SourceRotation = 1.4f };
            actor.Rotation = float.NaN;
            Check(!CastRotation.TryGet(e, out _), "Non-finite native direction must fail.");
            actor.Rotation = 0.6f;
            actor.IsCasting = false;
            Check(!CastRotation.TryGet(e, out _), "A completed native cast without its packet must fail.");
            actor.IsCasting = true;
            actor.ActionId = 49204;
            Check(!CastRotation.TryGet(e, out _), "A different native cast must not supply the direction.");
            actor.ActionId = 49203;
            actor.HasAddress = false;
            Check(!CastRotation.TryGet(e, out _), "A missing native address must fail.");
        });
    }

    private static LogSystemCastStartEvent Cast() => new() { SourceId = 1, ActionId = 49203 };

    private static object Packet(float rotation)
    {
        var type = typeof(LogSystemCastStartEvent).Assembly
            .GetType("PromeRotation.LogSystem.Capture.StartCastCapture+StartCastCaptureRaw", throwOnError: true)!;
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(c => c.GetParameters().Length == 7);
        return constructor.Invoke([(byte)1, 49203u, rotation, null, null, null, null]);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed unsafe class CastActor : IDisposable
    {
        private readonly PropertyInfo _objects = typeof(Svc).GetProperty(nameof(Svc.Objects))!;
        private readonly object? _original;
        private readonly NativeBattleChara* _native = (NativeBattleChara*)NativeMemory.AllocZeroed((nuint)sizeof(NativeBattleChara));
        public bool Available = true;
        public bool HasAddress = true;
        public bool IsCasting = true;
        public uint ActionId = 49203;
        public float Rotation { get => _native->CastRotation; set => _native->CastRotation = value; }

        public CastActor()
        {
            _original = _objects.GetValue(null);
            var caster = ScriptServiceProxy.Create<IBattleChara>((method, _) => method switch
            {
                "get_Address" => HasAddress ? (nint)_native : nint.Zero,
                "get_IsCasting" => IsCasting,
                "get_CastActionId" => ActionId,
                _ => throw new NotSupportedException(method),
            });
            _objects.SetValue(null, ScriptServiceProxy.Create<IObjectTable>((method, args) => method switch
            {
                "SearchByEntityId" => Available && (uint)args[0]! == 1 ? caster : null,
                _ => throw new NotSupportedException(method),
            }));
        }

        public void Dispose()
        {
            _objects.SetValue(null, _original);
            NativeMemory.Free(_native);
        }
    }
}
