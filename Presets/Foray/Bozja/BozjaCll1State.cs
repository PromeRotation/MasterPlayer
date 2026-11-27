using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed class BozjaCll1State
{
    internal bool Top;
    internal BozjaActor? HellDiver;
    internal readonly Dictionary<uint, List<BozjaActor>> Enemies = [];
    internal readonly Dictionary<long, int> ChainHits = [];
    internal int EnergyGenerations;
    internal readonly List<(BozjaActor Actor, GeometryTemplate Shape)> Orbs = [];
    internal readonly List<(ulong Id, Vector3 Position, List<BozjaActor> Targets, byte Pole)> Poles = [];
    internal readonly (Vector3 Origin, DateTime Activation, bool Away)?[] MagnetSources = new (Vector3, DateTime, bool)?[8];
    internal readonly byte[] PlayerPoles = new byte[8];
    internal ulong MagnetTethered;
    internal readonly List<BozjaActor> InfraredPlayers = [];
    internal readonly List<(BozjaActor Player, BozjaActor Enemy)> InfraredTethers = [];
    internal ulong InfraredTethered;
    internal ulong Fire;
    internal DateTime InfraredActivation;
}
