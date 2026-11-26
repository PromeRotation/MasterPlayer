using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsDiablo => Definition.Key == "DAL4DiabloArmament";
    internal string? DiabloSequenceError { get; private set; }
    internal List<BozjaAoe> DiabloRuinous { get; } = [];
    internal List<BozjaActor> DiabloOrbs { get; } = [];
    internal DateTime[] DiabloBombs { get; } = new DateTime[64];
    private readonly Dictionary<uint, List<BozjaActor>> diabloGates = [];
    private readonly Dictionary<ulong, ReplayActorState> diabloCommitted = [];
    private static readonly uint[] DIABLO_GATE_OIDS = [0x1EB1D8, 0x1EB1D7, 0x1EB1D6, 0x1EB1D9];
    private CuchulainnGeometry? diabloWalls;
    private bool diabloCastChanged;

    private void ClearDiablo()
    {
        DiabloRuinous.Clear();
        DiabloOrbs.Clear();
        Array.Clear(DiabloBombs);
        diabloGates.Clear();
        diabloWalls = null;
        DiabloSequenceError = null;
    }

    private List<BozjaActor> DiabloGates(uint oid)
    {
        if (!diabloGates.TryGetValue(oid, out var gates))
            diabloGates.Add(oid, gates = Actors.Values.Where(a => a.Actor.Oid == oid).ToList());
        return gates;
    }

    private void DiabloCreated(BozjaActor actor)
    {
        if (!IsDiablo) return;
        if (diabloGates.TryGetValue(actor.Actor.Oid, out var gates)) gates.Add(actor);
        if (Active && actor.NativeNpc && actor.Actor.Oid == 0x31B5) DiabloOrbs.Add(actor);
    }

    private void DiabloDestroyed(BozjaActor actor)
    {
        if (diabloGates.TryGetValue(actor.Actor.Oid, out var gates)) gates.Remove(actor);
        // Aetheroplasm has no Destroy callback: the original orb survives.
    }

    private void BeginDiablo()
    {
        try
        {
            foreach (var rule in Definition.Rules)
                foreach (var actor in Actors.Values)
                {
                    if (rule.Kind == BozjaRuleKind.DiabloOrbs && actor.NativeNpc && actor.Actor.Oid == 0x31B5)
                        DiabloOrbs.Add(actor);
                    if (actor.Cast != null) Start(actor, rule);
                    if (rule.Kind is BozjaRuleKind.DiabloBomb or BozjaRuleKind.DiabloKnockback)
                        foreach (var status in actor.Statuses.OfType<ReplayStatus>())
                        {
                            if (rule.Kind == BozjaRuleKind.DiabloBomb) DiabloStatus(actor, status, true);
                            else ImmuneStatus(actor, status, true);
                        }
                }
        }
        catch (ArgumentOutOfRangeException) when (DiabloSequenceError != null)
        {
            // BossModuleManager unloads a module whose phase activation fails.
            // Keep the diagnostic and partial callback evidence, but never
            // reactivate this original boss when missing gates later appear.
            if (Primary != null) retired.Add(Primary);
            Active = false;
            Completed = true;
            Primary = null;
        }
    }

    private void PrepareDiabloInput(ReplayActorState value)
    {
        if (!IsDiablo) return;
        diabloCommitted.TryGetValue(value.Id, out var committed);
        diabloCastChanged = InputCastChanged(committed, value);
        // A new Feed actor installs its tracked snapshot before Start. An
        // existing actor commits only after every subscriber has succeeded.
        if (committed == null) diabloCommitted.Add(value.Id, value);
    }

    internal void CommitActorInput(ReplayActorState value)
    {
        if (IsDiablo) diabloCommitted[value.Id] = value;
    }

    private void StartDiablo(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        if (rule.Kind == BozjaRuleKind.DiabloArena)
        {
            Casts.RemoveAll(c => c.Rule == rule);
            Casts.Add(new(++serial, rule, actor, DungeonEncounter.QuantizedDungeonPosition(Center), 0, Activation(actor, 9.2d)));
        }
        else if (rule.Kind == BozjaRuleKind.DiabloRuinous)
        {
            void Add(Vector3 position, float rotation, DateTime activation)
                => DiabloRuinous.Add(new(new(++serial, rule, actor,
                    DungeonEncounter.QuantizedDungeonPosition(position), rotation, activation), false, true));
            var pos = spell.Location;
            var rot = spell.Rotation ?? 0;
            var act = Activation(actor, spell.ActionId == 23712 ? 1.5d : 0);
            Add(pos, rot, act);
            if (spell.ActionId != 23712) return;
            try
            {
                var gates = DIABLO_GATE_OIDS.SelectMany(DiabloGates).ToList();
                for (var i = 0; i < 8; ++i)
                    if (rot == gates[i].Actor.Rotation) { gates.RemoveAt(i); break; }
                for (var i = 0; i < 7; ++i)
                {
                    var gate = gates[i];
                    if (!DiabloGateInRect(gate.Actor.Position, pos, rot)) continue;
                    var pair = DiabloGates(gate.Actor.Oid);
                    pair.Remove(gate);
                    var exit = pair[0];
                    var act2 = act.AddSeconds(4d);
                    Add(exit.Actor.Position, exit.Actor.Rotation, act2);
                    for (var j = 0; j < 7; ++j)
                    {
                        var third = gates[j];
                        if (third.Actor.Oid == gate.Actor.Oid
                            || !DiabloGateInRect(third.Actor.Position, exit.Actor.Position, exit.Actor.Rotation)) continue;
                        var finalPair = DiabloGates(third.Actor.Oid);
                        finalPair.Remove(third);
                        var final = finalPair[0];
                        Add(final.Actor.Position, final.Actor.Rotation, act2.AddSeconds(4.1d));
                        return;
                    }
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                DiabloSequenceError = "RuinousPseudoomen 门序列索引失败，保留原队列及缓存副作用";
                throw;
            }
        }
        else if (rule.Kind == BozjaRuleKind.DiabloNox)
        {
            var (sin, cos) = Math.SinCos(actor.Actor.Rotation);
            Exaflares.Add(new(new(++serial, rule, actor, actor.Actor.Position, 0, Activation(actor)),
                new(10 * (float)sin, 0, 10 * (float)cos), 5, 5, 1.1d));
        }
        else if (rule.Kind == BozjaRuleKind.DiabloKnockback)
            Casts.Add(new(++serial, rule, actor, spell.Location, spell.Rotation ?? 0, Activation(actor)));
    }

    private static bool DiabloGateInRect(Vector3 point, Vector3 origin, float rotation)
    {
        var (sin, cos) = Math.SinCos(rotation);
        var dx = point.X - origin.X;
        var dz = point.Z - origin.Z;
        var along = dx * (float)sin + dz * (float)cos;
        var side = dx * (float)cos - dz * (float)sin;
        return along >= 0 && along <= 100 && MathF.Abs(side) <= 3;
    }

    private void DiabloMap(byte index, uint state)
    {
        if (!Active || !IsDiablo || index != 0x33) return;
        if (state == 0x00020001)
        {
            Center = DungeonEncounter.QuantizedDungeonPosition(Center);
            Arena = new CircleGeometry(Center, 17);
            Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.DiabloArena);
        }
        else if (state == 0x00080004)
        {
            Center = Definition.Center;
            Arena = Definition.Arena;
        }
    }

    private void DiabloFinish(BozjaActor actor, ReplayCast spell, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.DiabloRuinous && DiabloRuinous.Count > 0 && spell.ActionId is 24995 or 24908 or 24911)
            DiabloRuinous.RemoveAt(0);
        if (rule.Kind == BozjaRuleKind.DiabloKnockback && rule.Actions.Contains(spell.ActionId))
        {
            var index = Casts.FindIndex(c => c.Rule == rule && c.Source.Actor.Id == actor.Actor.Id);
            if (index >= 0) Casts.RemoveAt(index);
        }
    }

    private void DiabloEffect(BozjaActor actor, uint action)
    {
        if (!IsDiablo) return;
        if (action is 23733 or 23734) DiabloOrbs.Remove(actor);
        if (action is not (23744 or 23745)) return;
        var index = Exaflares.FindIndex(l => AlmostEqual(l.Cast.Origin, actor.Actor.Position));
        if (index < 0) return;
        var line = Exaflares[index];
        line.Cast = line.Cast with { Origin = actor.Actor.Position + line.Advance, Activation = Now.AddSeconds(line.Interval) };
        if (--line.Left == 0) Exaflares.RemoveAt(index);
        ++exaflareVersion;
    }

    private void DiabloStatus(BozjaActor actor, ReplayStatus status, bool gain)
    {
        if (!Active || !IsDiablo || status.Id != 2657) return;
        var slot = Party.Where(p => p.Slot is >= 0 and < 64).OrderBy(p => p.Slot)
            .FirstOrDefault(p => p.EntityId == actor.Actor.Id)?.Slot ?? -1;
        if (slot >= 0) DiabloBombs[slot] = gain ? status.ExpiresAt : default;
    }

    internal DateTime DiabloBombActivation(BozjaActor viewer)
    {
        if (!IsDiablo || !Active) return default;
        var slot = Party.Where(p => p.Slot is >= 0 and < 64).OrderBy(p => p.Slot)
            .FirstOrDefault(p => p.EntityId == viewer.Actor.Id)?.Slot ?? -1;
        return slot >= 0 ? DiabloBombs[slot] : default;
    }

    private IEnumerable<BozjaAoe> DiabloAoes(BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.DiabloRuinous)
        {
            if (DiabloRuinous.Count > 1)
            {
                DiabloRuinous[0] = DiabloRuinous[0] with { Danger = true };
                for (var i = 0; i < Math.Min(2, DiabloRuinous.Count); ++i)
                    DiabloRuinous[i] = DiabloRuinous[i] with { Risky = DiabloRuinous[i].Cast.Activation.AddSeconds(-5d) <= Now };
            }
            foreach (var aoe in DiabloRuinous.Take(2)) yield return aoe;
        }
        if (rule.Kind == BozjaRuleKind.DiabloNox)
            foreach (var aoe in exaflarePublished) yield return aoe;
        if (rule.Kind == BozjaRuleKind.DiabloArena)
            foreach (var cast in Casts.Where(c => c.Rule == rule)) yield return new(cast, false, true);
        if (rule.Kind != BozjaRuleKind.Aoe || !IsDiablo || rule.Component is not ("Explosion" or "PillarOfShamash")) yield break;
        var list = Casts.Where(c => c.Rule == rule).ToArray();
        if (list.Length == 0) yield break;
        foreach (var cast in list)
        {
            if (cast.Activation >= list[0].Activation.AddSeconds(1d)
                && (rule.Component != "Explosion" || cast.Rotation == list[0].Rotation)) yield break;
            yield return new(cast, false, true);
        }
    }

    private IEnumerable<BozjaHint> DiabloHints(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.DiabloKnockback && DiabloMovements(viewer).Any(m => DiabloDestinationUnsafe(m.To)))
            yield return new("About to be knocked into danger!");
        if (rule.Kind != BozjaRuleKind.DiabloBomb) yield break;
        var slot = ViewerSlot(viewer);
        if (slot is < 0 or >= 64 || DiabloBombs[slot] == default) yield break;
        if (DiabloBombs[slot] <= Now.AddSeconds(2.5d))
            yield return new("Stop everything!", viewer.Actor.Cast != null || viewer.Actor.TargetId != 0);
        if (viewer.Actor.Dead) DiabloBombs[slot] = default;
    }

    internal bool DiabloDestinationUnsafe(Vector3 pos)
        => Casts.Any(c => c.Rule.Component == "AssaultCannon" && c.Contains(pos))
            || exaflarePublished.Any(a => a.Cast.Contains(pos)) || !Arena.Contains(pos, 0);

    internal IEnumerable<(BozjaCast Cast, Vector3 From, Vector3 To)> DiabloMovements(BozjaActor viewer)
    {
        if (!Active) yield break;
        var from = viewer.Actor.Position;
        foreach (var cast in Casts.Where(c => c.Rule.Kind == BozjaRuleKind.DiabloKnockback))
        {
            if (WindImmune(viewer, cast.Activation)) continue;
            var delta = from - cast.Origin;
            var length = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
            if (!(length > 0)) continue;
            var inverse = 1f / length;
            var direction = new Vector3(delta.X * inverse, 0, delta.Z * inverse);
            float wall;
            if (Arena is CircleGeometry circle)
            {
                var offset = from - Center;
                var b = offset.X * direction.X + offset.Z * direction.Z;
                var discriminant = b * b - (offset.X * offset.X + offset.Z * offset.Z) + circle.Radius * circle.Radius;
                var distance = -b + MathF.Sqrt(discriminant);
                wall = discriminant < 0 || distance < 0 ? float.MaxValue : distance;
            }
            else wall = (diabloWalls ??= new(Definition.Arena)).WallDistance(from, direction);
            var hitbox = viewer.Actor.HitboxRadius - .499f;
            var to = from + MathF.Min(30, wall - Math.Clamp(hitbox, .5f - .499f, hitbox)) * direction;
            yield return (cast, from, to);
            from = to;
        }
    }

    private IEnumerable<BozjaZone> DiabloNavigation(BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.DiabloOrbs && DiabloOrbs.Count > 0)
            yield return new("diablo/orbs", rule.Component, Now.AddSeconds(5d),
                new BozjaDiabloOrbsTemplate(DiabloOrbs.Select(o =>
                {
                    var (sin, cos) = Math.SinCos(o.Actor.Rotation);
                    return o.Actor.Position + .5f * new Vector3((float)sin, 0, (float)cos);
                }).ToArray()), default, 0);
        foreach (var aoe in DiabloAoes(rule).Where(a => a.Risky))
            yield return new($"diablo/aoe/{aoe.Cast.Generation}", rule.Component, aoe.Cast.Activation,
                aoe.Cast.Shape, aoe.Cast.Origin, aoe.Cast.Rotation);
    }
}
