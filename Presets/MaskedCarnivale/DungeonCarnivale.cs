using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal bool IsCarnivale => territory == CarnivaleCatalog.TERRITORY;
    internal bool HasCarnivaleGaze => IsCarnivale && (Active?.BossOid is 0x26F8 or 0x26F4 || VisibleCarnivaleEyes != null);
    private DungeonActor? carnivalePrimary;
    private bool carnivalePrimaryRemoved;
    private bool carnivaleWiping;
    private bool carnivaleEnabled = true;
    private readonly Dictionary<ulong, AlphaActorState> carnivaleActors = [];
    private readonly List<AlphaActorState> carnivaleActorOrder = [];
    private readonly Dictionary<ulong, DungeonPendingCast> carnivaleUnobservedEnds = [];
    private readonly List<CarnivaleModuleState> carnivaleModules = [];
    private readonly List<CarnivaleModuleState> carnivaleLoaded = [];
    private readonly List<CarnivaleModuleState> carnivalePending = [];
    private ulong carnivalePartyZero;
    private CarnivaleModuleState? carnivaleSelected;
    private const float CARNIVALE_LOAD_DISTANCE_SQUARED = 500 * 500;
    private long carnivaleExplosionGeneration;
    private long carnivaleInterruptGeneration;
    private long carnivaleLineOfSightGeneration;
    private const string CARNIVALE_PRECOMBAT = "This stage is trivial.\nUse whatever skills you have to defeat these opponents.";
    private bool CarnivalePrecombatVisible => IsCarnivale && Active == null && carnivalePrimary != null
        && !carnivalePrimaryRemoved && carnivaleSelected?.Started == false && !carnivaleWiping && carnivaleEnabled;
    internal string? CarnivalePrecombatHint => CarnivalePrecombatVisible && carnivalePrimary!.Oid is not (0x25C1 or 0x2C6A or 0x2C6C or 0x30F7 or 0x3FA7)
        ? CarnivalePreparation(carnivalePrimary.Oid) : null;

    private static bool CarnivaleTrash(uint primary, uint oid) => primary switch
    {
        0x25CD => oid is 0x25CD or 0x2700,
        0x26FF => oid is 0x26FF or 0x2701 or 0x25CE,
        0x2706 => oid is 0x2706 or 0x2707,
        0x25C0 => oid is 0x25C0 or 0x25C2 or 0x25C4,
        0x25C1 => oid is 0x25C1 or 0x25C5 or 0x25C3,
        0x25C8 => oid is 0x25C8 or 0x25D2,
        0x25CC => oid == 0x25CC,
        0x25D5 => oid is 0x25D5 or 0x25D6,
        0x2717 => oid == 0x2717,
        0x2718 => oid == 0x2718,
        0x2719 => oid == 0x2719,
        0x271A => oid == 0x271A,
        0x271B => oid is 0x271B or 0x271C,
        0x26F5 => oid is 0x26F5 or 0x26F6,
        0x26F8 => oid is 0x26F8 or 0x26F7,
        0x2724 => oid is 0x2724 or 0x2726,
        0x2725 => oid is 0x2725 or 0x2726,
        0x272F => oid == 0x272F,
        0x26FC => oid == 0x26FC,
        0x2735 => oid is 0x2735 or 0x2734,
        0x2720 => oid is 0x2720 or 0x271F,
        0x2721 => oid is 0x2721 or 0x2722 or 0x2723,
        0x26F2 => oid == 0x26F2,
        0x26F4 => oid is 0x26F4 or 0x26F3,
        0x26F9 => oid is 0x26F9 or 0x26FA or 0x26FB,
        0x271D => oid == 0x271D,
        0x271E => oid == 0x271E,
        0x2703 => oid is 0x2703 or 0x2702,
        0x2705 => oid is 0x2705 or 0x2704,
        0x2708 => oid is 0x2708 or 0x2709 or 0x270A,
        0x270B => oid is 0x270B or 0x270C or 0x270D,
        _ => oid is 0x25BE or 0x25BD,
    };

    private string CarnivalePreparation(uint oid) => oid switch
    {
        0x25CD => "Kill mandragoras last incase you need to get blinded again.",
        0x26FF => "Kill mandragoras last incase you need to get blinded again.",
        0x2706 => "In slime explosion radius!",
        0x25C0 => "To beat this stage in a timely manner,\nyou should have at least one spell of each element.\n(Water, Fire, Ice, Lightning, Earth and Wind)",
        0x25D4 => $"{carnivalePrimary?.Name} is weak against water based spells.\nFlying Sardine is recommended to interrupt raidwide.",
        0x242D => $"{carnivalePrimary?.Name} summons a total of 6 adds during the fight, one of each element.\nHealer mimikry can be helpful if you have trouble surviving.",
        0x3FA5 => $"For this fight The Ram's Voice, Ultravibration, Diamondback,\nExuviation, Flying Sardine, Loom, a physical dmg ability and a healing\nability (preferably Pom Cure with healer mimicry) are mandatory.",
        0x30F5 => $"For this fight Diamondback, Exuviation, Flying Sardine and a healing\nability (preferably Pom Cure with healer mimicry) are mandatory.\nEerie Soundwave is also recommended.",
        0x2C67 => $"{carnivalePrimary?.Name} will have phases where all magic damage gets reflected.\nExuviation, a melee ability and fire, wind and ice spells are recommended.",
        0x2C5B => $"For this act Exuviation and Diamondback are mandatory.\nBringing Flying Sardine, lightning and wind spells is higly recommended.",
        0x2C5D => $"{carnivalePrimary?.Name} will cast Throttle on you which needs to be\ncleansed with Excuviation. It will also spawn two hands which need to be\nkilled asap. Focus the left hand first because it will drain all your MP.",
        0x2CD2 => $"{carnivalePrimary?.Name} will spawn upto 4 waves of adds which are weak to fire.\nA way to quickly heal yourself to full is mandatory and a ranged fire\nability such as Mustard Bomb and Flying Sardine for interrupts\nare highly recommended.",
        0x2CF8 => $"{carnivalePrimary?.Name} will spawn Bombs and Magitek Explosives throughout the fight.\nUse Snort to push away Bombs from Magitek Explosives and bait Fireballs\naway from the MEs. Meanwhile destroy the MEs asap because they will blow\nup on their own after about 35s. If any ME detonates you will be wiped.\nThe MEs are weak against water abilities and strong against fire attacks.",
        0x2C84 => $"{carnivalePrimary?.Name} will cast Alternate Plumage, which makes him almost\nimmune to damage. Use Eerie Soundwave to dispel it. Caber Toss must be\ninterrupted or you will wipe.\nAdditionally Exuviation and earth spells are recommended for act 2.",
        0x2C58 => $"{carnivalePrimary?.Name} will cast Raw Instinct, which causes all his hits to crit.\nUse Eerie Soundwave to dispel it.\n{carnivalePrimary?.Name} is weak against earth and strong against lightning attacks.",
        0x2678 => $"{carnivalePrimary?.Name} will reflect all physical damage in act 1, all magic damage in act 2\nand switch between both in act 3. Loom, Exuviation and Diamondback\nare recommended. In act 3 can start the Final Sting combination\nat about 50% health left. (Off-guard->Bristle->Moonflute->Final Sting)",
        0x267F => $"In this act {carnivalePrimary?.Name} will reflect all magic attacks.\nHe will also spawn adds that need to be dealed with swiftly\nsince they will spam raidwides. The adds are immune against magic\nand fire attacks.",
        0x2680 => $"In this act {carnivalePrimary?.Name} will switch between magic and physical reflects.\nSpend attention to that so you don't accidently kill yourself.\nAs soon as he starts casting Web go to the edge to bait Meteor, then use Loom\nto escape. You can start the Final Sting combination at about 50% health left.\n(Off-guard->Bristle->Moonflute->Final Sting)",
        0x2735 => $"The {carnivalePrimary?.Name} is immune to magic, the {CarnivaleEnemies(0x2734).FirstOrDefault()?.Name} is immune to\nphysical attacks. For the 2nd act Diamondback is highly recommended.\nFor the 3rd act a ranged physical spell such as Fire Angon\nis highly recommended.",
        0x2736 => $"The {carnivalePrimary?.Name} casts Silence which should be interrupted.\nCondensed Libra puts a debuff on you. Use Diamondback to survive the\nfollowing attack. Alternatively you can cleanse the debuff with Exuviation.",
        0x2739 => $"{carnivalePrimary?.Name} spawns two adds when casting Boneshaker. These should be a\npriority or they will explode and wipe you. To kill them without touching\nthe electric field use a ranged physical attack such as Fire Angon.\nYou can start the Final Sting combination at about 50% health left.\n(Off-guard->Bristle->Moonflute->Final Sting)",
        0x2732 => $"The {carnivalePrimary?.Name} will use Ecliptic Meteor.\nUse Diamondback to survive it.\nYou can start the Final Sting combination at about 40% health left.\n(Off-guard->Bristle->Moonflute->Final Sting)",
        0x26FC => "The first act is easy. Kill the grenades in one hit each or they will wipe you.\nIf you gear is bad consider using 1000 Needles.\nFor the 2nd act you should bring Sticky Tongue. In the 2nd act you can start\nthe Final Sting combination at about 50%\nhealth left. (Off-guard->Bristle->Moonflute->Final Sting)",
        0x26FE => $"{carnivalePrimary?.Name} spawns grenades and gas bombs during the fight. Just as in\nact 1 the grenades must be killed in one hit each or they will wipe you.\nUse Sticky Tongue to pull Gas Bombs to the boss so they interrupt the enrage.\nYou can start the Final Sting combination at about 50% health left.\n(Off-guard->Bristle->Moonflute->Final Sting)",
        0x272F => "The first act is fairly easy. Interrupt the Void Blizzards with Flying\nSardine and most of the danger is gone. The Imps are weak against fire spells.\nIn the 2nd act you can start the Final Sting combination at about 50%\nhealth left. (Off-guard->Bristle->Moonflute->Final Sting)",
        0x2730 => "Interrupt The Rams Keeper with Flying Sardine. You can start the\nFinal Sting combination at about 50% health left.\n(Off-guard->Bristle->Moonflute->Final Sting).\nThe boss will sometimes spawn an Arena Imp during the fight.",
        0x272A => "Diamondback and Flying Sardine are essential for this stage. The Final\nSting combo (Off-guard->Bristle->Moonflute->Final Sting) can make act 3\nincluding the achievement much easier. Ultros in act 2 and 3 is weak to\nfire.",
        0x272B => $"{carnivalePrimary?.Name} is weak to fire. Interrupt Imp Song.",
        0x272C => $"This act is act 1+2 combined with tentacles on top.\nThe Final Sting combo (Off-guard->Bristle->Moonflute->Final Sting) makes\nthis act including the achievement much easier. {carnivalePrimary?.Name} is weak to fire.",
        0x2727 => "At the start of the fight Rebekkah will cast Reflect. This will reflect all\nmagic damage back to you. Useful skills: Sharpened Knife,\nFlying Sardine, Ink Jet (Act 2), Exuviation (Act 2), potentially a Final Sting\ncombo. (Off-guard->Bristle->Moonflute->Final Sting)",
        0x2728 => "Same as first act, but this time the boss will cast a gaze from all directions.\nThe easiest counter for this is to blind yourself by casting Ink Jet on the\nboss after it casted Schizocarps.\nThe Final Sting combo window opens at around 75% health.\n(Off-guard->Bristle->Moonflute->Final Sting)",
        0x2724 => "Make the manticores run to the kegs and their attacks will make them\nblow up. They take 2500 damage per keg explosion.\nThe Ram's Voice and Ultravibration combo can be used to kill manticores.",
        0x2725 => "Same as last stage. Make the manticores run to the kegs and their attacks\nwill make them blow up. Their attacks will also do friendly fire damage\nto each other.\nThe Ram's Voice and Ultravibration combo can be used to kill manticores.",
        0x2720 => $"The {carnivalePrimary?.Name} counters magical attacks, the {CarnivaleEnemies(0x271F).FirstOrDefault()?.Name} counters physical\nattacks. If you have healing spells you can just tank the counter damage\nand kill them however you like anyway. All opponents in this stage are\nweak to lightning.\nThe Ram's Voice and Ultravibration combo can be used in Act 2.",
        0x2721 => $"{carnivalePrimary?.Name} is weak to lightning spells.\nDuring the fight he will spawn one of each claws as known from act 1.\nIf available use the Ram's Voice + Ultravibration combo for instant kill.",
        0x26F2 => "The cyclops are very slow, but will instantly kill you, if they catch you.\nKite them or kill them with the self-destruct combo. (Toad Oil->Bristle->\nMoonflute->Swiftcast->Self-destruct) If you don't use the self-destruct\ncombo in act 1, you can bring the Final Sting combo for act 2.\n(Off-guard->Bristle->Moonflute->Final Sting)\nDiamondback is highly recommended in act 2.",
        0x26F4 => $"{carnivalePrimary?.Name} will spawn a cyclops a few seconds into the fight. Make sure\nto kill it before it reaches you. After that you can just slowly take down the\nboss. Use Diamondback to survive the 1111 Tonze Swing. Alternatively\nyou can try the Final Sting combo when he drops to about 75% health.\n(Off-guard->Bristle->Moonflute->Final Sting)",
        0x26F9 => "For this stage Flying Sardine and Acorn Bomb are highly recommended.\nUse Flying Sardine to interrupt High Voltage.\nUse Acorn Bomb to put Shabtis to sleep until their buff runs out.",
        0x25C8 => "Trivial act. Enemies here are weak to lightning and fire.\nIn Act 2 the Ram's Voice and Ultravibration combo can be useful.\nFlying Sardine for interrupts can be beneficial.",
        0x25CC => "These turtles have very high defenses.\nBring 1000 Needles or Doom to defeat them.\nAlternatively you can remove their buff with Eerie Soundwave.",
        0x2703 => "For this stage the spells Sticky Tongue and Snort are recommended.\nUse them to pull or push Slimes close to Ice Sprites.\nThen hit the slime from a distance with anything but fire spells to set of an explosion.",
        0x2705 => "Pull or push the Lava Slimes to the Ice Sprites and then hit the slimes\nfrom a distance to set of the explosions.",
        0x2708 => "For this stage the spell Flying Sardine to interrupt the Progenitrix in Act 2\nis highly recommended. Hit the Cherry Bomb from a safe distance\nwith anything but fire damage to set of a chain reaction to win this act.",
        0x25D5 => $"{carnivalePrimary?.Name} is weak to lightning spells.\nDuring the fight he will spawn 6 beetles.\nIf available use the Ram's Voice + Ultravibration combo for the instant kill.",
        0x270B => "Clever activation of cherry bombs will freeze the Progenitrix.\nInterrupt its burst skill or wipe. The Progenitrix is weak to wind spells.",
        0x2717 => $"{carnivalePrimary?.Name} will cast King's Will during the fight. Interrupt it with\nFlying Sardine or he will become stronger each time. After 3 casts he\nstarts using the interruptible enrage Black Nebula.",
        0x2718 => "These bombs start self-destruction on combat start. Pull them together\nwith Sticky Tongue and attack them with anything to interrupt them.\nThey are weak against wind and strong against fire.",
        0x2719 => "Same as last act, but this time there are 4 bombs. Pull them to the\nmiddle with Sticky Tongue and attack them with any AOE to keep them\ninterrupted. They are weak against wind and strong against fire.",
        0x271A => "For this stage Ice Spikes and Bomb Toss are recommended spells.\nUse Ice Spikes to instantly kill roselets once they become aggressive.\nHydnora in act 2 is weak against water and strong against earth spells.",
        0x271B => $"Use Bomb Toss to stun {carnivalePrimary?.Name} when he casts Inflammable Fumes.\nUse Ice Spikes to instantly kill roselets once they become aggressive.\n{carnivalePrimary?.Name} is weak against water and strong against earth spells.",
        0x26F5 => "The first act is trivial, almost anything will work.\nFor act 2 having Flying Sardine is recommended.",
        0x26F8 => $"{carnivalePrimary?.Name} will cast various AOEs and summons adds.\nInterrupt the adds with Flying Sardine and kill them fast.\nIf the add is still alive during the next Black Sabbath, you will be wiped.",
        0x271D => "These slimes start casting Final Song after death.\nWhile Final Song is not deadly, it does heavy damage and applies silence\nto you. Take cover! For act 2 the spell Loom is strongly recommended.\nThe slimes are strong against blunt melee damage such as J Kick.",
        0x271E => "Same as first act, but the slimes will apply heavy to you.\nUse Loom to get out of line of sight as soon as Final Song gets casted.",
        _ => CARNIVALE_PRECOMBAT,
    };

    private void CarnivaleInput(Func<string, bool>? enabled)
    {
        if (!IsCarnivale) return;
        carnivaleMovingActors.Clear();
        carnivaleSiegfriedCreates.Clear();
        carnivaleMarionetteCounts.Clear();
        var previousPlayer = _actors.ContainsKey(carnivalePartyZero) ? carnivaleActors.GetValueOrDefault(carnivalePartyZero)?.Actor : null;
        foreach (var id in carnivaleActors.Keys.Where(id => !_actors.ContainsKey(id)).ToArray())
        {
            Finish(id);
            foreach (var module in carnivaleModules)
            {
                module.ChargeImmunity.Removed(id);
                module.Blindness.Removed(id);
                module.Eyes?.Removed(id);
                module.Draugar?.Removed(id);
                module.Red?.Pyretic.Removed(id);
                module.Gogo?.Pyretic.Removed(id);
            }
            carnivaleActors[id].Removed = true;
            carnivaleActorOrder.Remove(carnivaleActors[id]);
            carnivaleActors.Remove(id);
            treasureObservedCasts.Remove(id);
        }
        foreach (var input in _actors.Values.ToArray())
        {
            var created = !carnivaleActors.TryGetValue(input.Id, out var state);
            if (created)
            {
                carnivaleActors[input.Id] = state = new(input);
                carnivaleActorOrder.Add(state);
            }
            else
                _actors[input.Id] = input with { Oid = state!.Actor.Oid };
            // The source compares this actor's current and previous XZ, not
            // a change of perspective or a move event. New identities start still.
            if (!created && (state!.Actor.Position.X != input.Position.X || state.Actor.Position.Z != input.Position.Z))
                carnivaleMovingActors.Add(input.Id);
            var previousActor = created ? null : state!.Actor;
            var died = !created && !state!.Actor.Dead && input.Dead;
            state!.Actor = _actors[input.Id];
            carnivaleMarionetteCounts[input.Id] = CarnivaleEnemies(0x3FA8).Count();
            CarnivaleSnorterInput(state, created, previousActor);
            CarnivaleSiegfriedInput(state, created);
            foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Draugar is { NeedsBootstrap: false }))
                module.Draugar!.Observe(state.Actor);
            foreach (var module in carnivaleModules)
            {
                if (created && CarnivaleExplosionMember(module.Primary.Actor.Oid, state.Actor.Oid))
                    module.Explosions.Add((++carnivaleExplosionGeneration, state));
                if (created && module.Started && !module.ChargeNeedsBootstrap
                    && module.Primary.Actor.Oid is 0x2724 or 0x2725 && state.Actor.Oid == 0x2726)
                    module.ChargeKegs.Add(state);
                if (died || created && state.Actor.Dead)
                {
                    module.Explosions.RemoveAll(a => ReferenceEquals(a.State, state));
                    module.ChargeKegs.RemoveAll(a => ReferenceEquals(a, state));
                }
            }
            if (created && CarnivaleCatalog.Owns(territory, state.Actor.Oid))
            {
                var loaded = carnivaleLoaded.FirstOrDefault(m => m.Primary.Actor.Oid == state.Actor.Oid);
                if (loaded?.Primary.Actor.Id == input.Id)
                {
                    loaded.Primary = state;
                    if (loaded.DadJoke is { } kb)
                        loaded.DadJoke = kb with { Origin = state };
                }
                else
                    CarnivaleCreateModule(state, previousPlayer);
            }
            if (input.Id == carnivalePartyZero) previousPlayer = state.Actor;
        }
        carnivalePartyZero = CarnivaleSourcePlayerId ?? _actors.Values.FirstOrDefault(a => a.PartySlot == 0)?.Id ?? 0;
        if (_actors.GetValueOrDefault(carnivalePartyZero) is { } player)
        {
            for (var i = carnivalePending.Count - 1; i >= 0; --i)
            {
                var module = carnivalePending[i];
                if (module.Primary.Removed || module.Primary.Actor.Dead)
                {
                    carnivalePending.RemoveAt(i);
                    carnivaleModules.Remove(module);
                }
                else if (Vector3.DistanceSquared(player.Position, module.Primary.Actor.Position) <= CARNIVALE_LOAD_DISTANCE_SQUARED
                    && !carnivaleLoaded.Any(loaded => loaded.Primary.Actor.Oid == module.Primary.Actor.Oid))
                {
                    carnivalePending.RemoveAt(i);
                    carnivaleLoaded.Add(module);
                }
            }
            foreach (var module in carnivaleLoaded.ToArray())
            {
                var primary = module.Primary;
                if (!carnivaleWiping)
                {
                    if (!module.Started && CarnivaleModulePull(module))
                    {
                        module.Started = true;
                        if (module.Primary.Actor.Oid == 0x2706)
                            module.VoltageNeedsBootstrap = true;
                        if (module.Primary.Actor.Oid is 0x25C0 or 0x25C1)
                            module.PuddingNeedsBootstrap = true;
                        if (module.Primary.Actor.Oid is 0x271D or 0x271E)
                            module.LastSongNeedsBootstrap = true;
                        if (module.Primary.Actor.Oid == 0x26F9)
                            module.BallastNeedsBootstrap = true;
                        if (module.Primary.Actor.Oid is 0x26F4 or 0x2724 or 0x2725 or 0x2739)
                            module.ChargeNeedsBootstrap = true;
                        if (module.Primary.Actor.Oid is 0x25D4 or 0x242D or 0x2721 or 0x2727 or 0x2728 or 0x2730 or 0x2680)
                            module.VoidzoneNeedsBootstrap = true;
                        if (module.Primary.Actor.Oid is 0x2727 or 0x2728)
                            module.RebekkahNeedsBootstrap = true;
                        if (module.Primary.Actor.Oid is 0x272A or 0x272B or 0x272C or 0x2CF8)
                            module.TyphonNeedsBootstrap = true;
                        if (module.Primary.Actor.Oid is 0x272F or 0x2730)
                            module.ChimeraNeedsBootstrap = true;
                        if (module.Primary.Actor.Oid == 0x26FE)
                            module.SapNeedsBootstrap = true;
                        if (module.Primary.Actor.Oid is 0x3FA5 or 0x3FA7)
                        {
                            module.Goldor = new();
                            module.SapNeedsBootstrap = true;
                        }
                        if (module.Primary.Actor.Oid is 0x30F5 or 0x30F7)
                        {
                            module.Gogo = new();
                            module.SapNeedsBootstrap = true;
                        }
                        if (module.Primary.Actor.Oid == 0x30F7)
                            module.VoidzoneNeedsBootstrap = true;
                        if (module.Primary.Actor.Oid is 0x2C67 or 0x2C6A or 0x2C6C)
                            module.Siegfried = new();
                        if (module.Primary.Actor.Oid is 0x2C6A or 0x2C6C)
                            module.VoidzoneNeedsBootstrap = true;
                        if (module.Primary.Actor.Oid is 0x2C5B or 0x2C5D)
                        {
                            module.Red = new();
                            module.SapNeedsBootstrap = true;
                        }
                        if (module.Primary.Actor.Oid == 0x2CD2)
                        {
                            module.Draugar = new();
                            module.SapNeedsBootstrap = true;
                        }
                        if (module.Primary.Actor.Oid == 0x2CF8)
                            module.Snorter = new();
                        if (module.Primary.Actor.Oid is 0x2C84 or 0x2C58)
                            module.PapaNeedsBootstrap = true;
                        if (module.Primary.Actor.Oid is 0x2678 or 0x267F or 0x2680)
                            module.AzulmagiaNeedsBootstrap = true;
                        if (module.Primary.Actor.Oid is 0x2735 or 0x2736 or 0x2739)
                            module.PitNeedsBootstrap = true;
                    }
                    if (module.Started && CarnivaleModuleComplete(module))
                    {
                        CarnivaleUnload(module);
                        continue;
                    }
                }
                if (!module.Started && (primary.Removed
                    || Vector3.DistanceSquared(player.Position, primary.Actor.Position) > CARNIVALE_LOAD_DISTANCE_SQUARED))
                {
                    CarnivaleUnload(module);
                    if (!primary.Removed) CarnivaleCreateModule(primary, player);
                }
            }
        }
        CarnivaleSelect(enabled);
    }

    private void CarnivaleSelect(Func<string, bool>? enabled)
    {
        int Priority(CarnivaleModuleState? m)
        {
            if (m == null || !carnivaleLoaded.Contains(m)) return 0;
            if (m.Started) return 4;
            if (!m.Primary.Removed && !m.Primary.Actor.Dead && m.Primary.Actor.Targetable) return 3;
            return 1;
        }
        var previousSelected = carnivaleSelected;
        var best = carnivaleLoaded.OrderByDescending(Priority).FirstOrDefault();
        if (Priority(best) > Priority(carnivaleSelected)) carnivaleSelected = best;
        if (carnivaleSelected == null || !carnivaleLoaded.Contains(carnivaleSelected)) carnivaleSelected = best;
        carnivalePrimary = carnivaleSelected?.Primary.Actor;
        carnivalePrimaryRemoved = carnivaleSelected?.Primary.Removed ?? true;
        if (Active != null && (!ReferenceEquals(previousSelected, carnivaleSelected)
            || Active.BossOid != carnivalePrimary?.Oid))
        {
            Active = null;
            PrimaryId = 0;
            Arena = null;
        }
        var definition = Definitions.FirstOrDefault(d => d.BossOid == carnivalePrimary?.Oid);
        carnivaleEnabled = definition != null && enabled?.Invoke(definition.Name) != false;
    }

    private bool CarnivaleModulePull(CarnivaleModuleState module)
    {
        var primary = module.Primary;
        if (primary.Actor.Oid == 0x26FC)
            return !_actors.Values.Any(a => a.Oid == 0x26FE && a.Targetable)
                && carnivaleMovingActors.Contains(PlayerId)
                && _actors.Values.Any(a => a.Oid == 0x26FC && a.Targetable);
        if (primary.Actor.Oid is 0x25D4 or 0x242D or 0x25D5 or 0x2717 or 0x271A or 0x271B or 0x26F8 or 0x26F9 or 0x26F4 or 0x2721 or 0x2727 or 0x2728 or 0x272A or 0x272B or 0x272C or 0x2730 or 0x26FE or 0x2732 or 0x2736 or 0x2739 or 0x2678 or 0x267F or 0x2680 or 0x2C84 or 0x2C58 or 0x2CF8 or 0x2CD2 or 0x2C5B or 0x2C5D or 0x2C67 or 0x2C6A or 0x2C6C or 0x30F5 or 0x30F7 or 0x3FA5 or 0x3FA7)
            return !primary.Removed && primary.Actor.Targetable && primary.Actor.InCombat;
        return _actors.Values.Any(a => CarnivaleTrash(primary.Actor.Oid, a.Oid) && a.InCombat);
    }

    private bool CarnivaleModuleComplete(CarnivaleModuleState module)
    {
        var primary = module.Primary;
        if (primary.Actor.Oid is 0x271D or 0x271E) return false;
        if (primary.Actor.Oid is 0x25D4 or 0x242D or 0x25D5 or 0x2717 or 0x271A or 0x271B or 0x26F8 or 0x26F9 or 0x26F4 or 0x2721 or 0x2727 or 0x2728 or 0x272A or 0x272B or 0x272C or 0x2730 or 0x26FE or 0x2732 or 0x2736 or 0x2739 or 0x2678 or 0x267F or 0x2680 or 0x2C84 or 0x2C58 or 0x2CF8 or 0x2CD2 or 0x2C5B or 0x2C5D or 0x2C67 or 0x2C6A or 0x2C6C or 0x30F5 or 0x30F7 or 0x3FA5 or 0x3FA7)
            return primary.Removed || primary.Actor.Dead || primary.Actor.Hp == 0;
        return !_actors.Values.Any(a => CarnivaleTrash(primary.Actor.Oid, a.Oid) && !a.Dead);
    }

    // These components carry interrupt intent, without an action request in the old MP host.
    // Keep the complete observed cast (identity, lock, start and deadline) for hints and inspection.
    internal IEnumerable<CarnivaleInterruptCast> CarnivaleInterrupts => IsCarnivale && Active != null
        && carnivaleSelected is { Started: true } module ? module.Interrupts : [];
    internal IEnumerable<DungeonPendingCast> CarnivaleInterruptCasts => CarnivaleInterrupts.Select(c => c.Cast);

    private void CarnivaleCastStarted(DungeonPendingCast cast, bool correction, bool firstRaw = false)
    {
        CarnivaleEyeStarted(cast, correction);
        CarnivaleVoltageStarted(cast);
        CarnivaleLastSongStarted(cast);
        CarnivaleBallastStarted(cast, correction);
        CarnivaleChargeStarted(cast, correction, firstRaw);
        CarnivaleClawStarted(cast, correction);
        CarnivaleRebekkahStarted(cast);
        CarnivaleTyphonStarted(cast, correction);
        CarnivaleSapStarted(cast, correction);
        CarnivalePitStarted(cast, correction);
        CarnivaleAzulmagiaStarted(cast, correction);
        CarnivaleSnorterStarted(cast, correction);
        CarnivaleDraugarStarted(cast, correction);
        CarnivaleRedStarted(cast, correction);
        CarnivaleSiegfriedStarted(cast, correction);
        CarnivaleGogoStarted(cast, correction);
        CarnivaleGoldorStarted(cast, correction);
        if (!IsCarnivale || cast.Rule.Action is not (14265 or 14365 or 14369 or 14680 or 14719 or 14720 or 14721 or 14724 or 14753 or 15045 or 14890 or 15049 or 15076 or 14712 or 14744 or 15063 or 15081 or 15318 or 15321 or 18688 or 19053 or 18689 or 18961 or 23114 or 34589)
            || !carnivaleActors.TryGetValue(cast.Source, out var caster)) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && !m.PuddingNeedsBootstrap && !m.BallastNeedsBootstrap && !m.VoidzoneNeedsBootstrap && !m.TyphonNeedsBootstrap && !m.ChimeraNeedsBootstrap && !m.PitNeedsBootstrap && !m.PapaNeedsBootstrap && m.Draugar is not { NeedsBootstrap: true } && m.Red is not { NeedsBootstrap: true } && m.Gogo is not { NeedsBootstrap: true } && m.Goldor is not { NeedsBootstrap: true }
            && Definitions.Single(d => d.BossOid == m.Primary.Actor.Oid).Casts.Any(c => c.Action == cast.Rule.Action)))
        {
            var previous = module.Interrupts.FindIndex(c => ReferenceEquals(c.Caster, caster) && c.Cast.Rule.Action == cast.Rule.Action);
            if (previous >= 0)
                module.Interrupts[previous] = module.Interrupts[previous] with { Cast = cast };
            else
                module.Interrupts.Add(new(++carnivaleInterruptGeneration, caster, cast));
            if (cast.Rule.Action == 14744 && previous < 0) CarnivaleImpSongHint(module);
        }
    }

    private void CarnivaleCastFinished(DungeonPendingCast cast)
    {
        if (!IsCarnivale) return;
        CarnivaleEyeFinished(cast);
        CarnivaleVoltageFinished(cast);
        CarnivaleLastSongFinished(cast);
        CarnivaleBallastFinished(cast);
        CarnivaleChargeFinished(cast);
        CarnivaleClawFinished(cast);
        CarnivaleRebekkahFinished(cast);
        CarnivaleTyphonFinished(cast);
        CarnivaleSapFinished(cast);
        CarnivaleAzulmagiaFinished(cast);
        CarnivaleDraugarFinished(cast);
        CarnivaleRedFinished(cast);
        CarnivaleSiegfriedFinished(cast);
        CarnivaleGogoFinished(cast);
        CarnivaleGoldorFinished(cast);
        foreach (var module in carnivaleModules)
        {
            module.Interrupts.RemoveAll(c => ReferenceEquals(c.Cast, cast));
            if (cast.Rule.Action == 14744 && module.Started && !module.TyphonNeedsBootstrap)
                CarnivaleImpSongHint(module);
        }
    }

    private void CarnivaleCreateModule(AlphaActorState primary, DungeonActor? player)
    {
        var module = new CarnivaleModuleState(primary);
        if (primary.Actor.Oid is 0x25CD or 0x26FF) module.Eyes = new();
        foreach (var state in carnivaleActors.Values.Where(a => !a.Actor.Dead
            && CarnivaleExplosionMember(primary.Actor.Oid, a.Actor.Oid)))
            module.Explosions.Add((++carnivaleExplosionGeneration, state));
        carnivaleModules.Add(module);
        if (player != null && Vector3.DistanceSquared(player.Position, primary.Actor.Position) <= CARNIVALE_LOAD_DISTANCE_SQUARED
            && !carnivaleLoaded.Any(m => m.Primary.Actor.Oid == primary.Actor.Oid))
            carnivaleLoaded.Add(module);
        else
            carnivalePending.Add(module);
    }

    private void CarnivaleUnload(CarnivaleModuleState module)
    {
        var oid = module.Primary.Actor.Oid;
        carnivaleLoaded.Remove(module);
        carnivaleModules.Remove(module);
        var actions = Definitions.Single(d => d.BossOid == oid).Casts.Select(c => c.Action)
            .Where(action => !carnivaleModules.Any(m => (m.Started || m.Eyes != null && CarnivaleConstructorAction(action))
                && Definitions.Single(d => d.BossOid == m.Primary.Actor.Oid).Casts.Any(c => c.Action == action))).ToHashSet();
        foreach (var cast in _casts.Values.Where(c => actions.Contains(c.Rule.Action)).ToArray())
            _casts.Remove(cast.Source);
    }

    private IEnumerable<DungeonActor> CarnivaleCandidates => carnivalePrimary == null ? [] : [carnivalePrimary];

    private void CarnivaleUnselect()
    {
        Active = null;
        PrimaryId = 0;
        Arena = null;
    }

    private bool CarnivalePull(DungeonActor actor) => !carnivaleWiping && actor.Id == carnivaleSelected?.Primary.Actor.Id
        && carnivaleSelected?.Started == true;

    private DungeonDefinition? CarnivaleCastDefinition(uint action) => Definitions.FirstOrDefault(d =>
        carnivaleModules.Any(m => (m.Started || m.Eyes != null && CarnivaleConstructorAction(action)) && m.Primary.Actor.Oid == d.BossOid)
        && d.Casts.Any(c => c.Action == action))
        ?? Definitions.FirstOrDefault(d => carnivaleModules.Any(m => (m.Started || m.Eyes != null && CarnivaleConstructorAction(action)) && m.Primary.Actor.Oid == d.BossOid));

    private bool CarnivaleCastVisible(DungeonPendingCast cast) => !IsCarnivale
        || Active?.Casts.Any(r => r.Action == cast.Rule.Action) == true;

    private IEnumerable<DungeonAmbientAoe> CarnivaleAoes()
    {
        if (!IsCarnivale || carnivalePrimary == null || carnivaleWiping
            || Active == null && !CarnivalePrecombatVisible) yield break;
        var center = new Vector3(100, carnivalePrimary.Position.Y, 100);
        // Pre-pull arena is a display preview. The source only publishes navigation after pull.
        if (Active == null)
            yield return new("carnivale/precombat/arena", "天青斗场", new DungeonShapeTemplate(CarnivaleCatalog.Arena(center.Y, carnivalePrimary.Oid)),
                center, Navigate: false, OutlineOnly: true, Unclipped: true);
        foreach (var aoe in CarnivaleExplosionAoes()) yield return aoe;
        foreach (var aoe in CarnivaleEyeAoes()) yield return aoe;
        foreach (var aoe in CarnivaleVoltageAoes()) yield return aoe;
        foreach (var aoe in CarnivaleLastSongAoes()) yield return aoe;
        foreach (var aoe in CarnivaleBallastAoes()) yield return aoe;
        foreach (var aoe in CarnivaleChargeAoes()) yield return aoe;
        foreach (var aoe in CarnivaleClawAoes()) yield return aoe;
        foreach (var aoe in CarnivaleTyphonAoes()) yield return aoe;
        foreach (var aoe in CarnivaleSapAoes()) yield return aoe;
        foreach (var aoe in CarnivaleMaelstromAoes()) yield return aoe;
        foreach (var aoe in CarnivalePitAoes()) yield return aoe;
        foreach (var aoe in CarnivalePapaAoes()) yield return aoe;
        foreach (var aoe in CarnivaleSnorterAoes()) yield return aoe;
        foreach (var aoe in CarnivaleDraugarAoes()) yield return aoe;
        foreach (var aoe in CarnivaleRedAoes()) yield return aoe;
        foreach (var aoe in CarnivaleSiegfriedAoes()) yield return aoe;
        foreach (var aoe in CarnivaleGogoAoes()) yield return aoe;
        foreach (var aoe in CarnivaleGoldorAoes()) yield return aoe;
        var shape = new LocalPolygonTemplate([new(0, -.7f), new(.433f, .35f), new(-.433f, .35f)]);
        var arena = Arena ?? CarnivaleCatalog.Arena(center.Y, carnivalePrimary.Oid);
        var enemies = new List<DungeonActor>();
        if (carnivalePrimary.Oid == 0x2725)
            enemies.AddRange(CarnivaleEnemies(0x2725).Concat(CarnivaleEnemies(0x2726)));
        else if (carnivalePrimary.Oid == 0x25CD)
            enemies.AddRange(CarnivaleEnemies(0x25CD).Concat(CarnivaleEnemies(0x2700)));
        else if (carnivalePrimary.Oid == 0x2706)
            enemies.AddRange(CarnivaleEnemies(0x2706).Concat(CarnivaleEnemies(0x2707)));
        else if (carnivalePrimary.Oid is 0x25CC or 0x2705 or 0x2718 or 0x2719 or 0x271A or 0x271D or 0x271E or 0x26F2 or 0x272F or 0x26FC)
            enemies.AddRange(_actors.Values.Where(a => a.Oid == carnivalePrimary.Oid || carnivalePrimary.Oid == 0x2705 && a.Oid == 0x2704));
        else
        {
            if (!carnivalePrimaryRemoved) enemies.Add(carnivalePrimary);
            var adds = carnivalePrimary.Oid switch
            {
                0x25CD => new uint[] { 0x2700 },
                0x26FF => [0x25CE, 0x2701],
                0x2706 => [0x2707],
                0x25C0 => new uint[] { 0x25C2, 0x25C4 },
                0x25C1 => [0x25C5, 0x25C3],
                0x25D4 => [],
                0x242D => [0x2714, 0x2716, 0x2711, 0x2713, 0x2715, 0x2712],
                0x25C8 => new uint[] { 0x25D2 },
                0x25D5 => [0x25D6],
                0x2717 => [],
                0x271B => [0x271C],
                0x26F5 => [0x26F6],
                0x26F8 => [0x26F7],
                0x26F9 => [0x26FA, 0x26FB],
                0x26F4 => [0x26F3],
                0x2724 => [0x2726],
                0x2727 or 0x2728 or 0x272A or 0x272B or 0x2732 => [],
                0x272C => [0x272D, 0x272E],
                0x2730 => [0x2731],
                0x26FE => [0x26FC, 0x26FD],
                0x2735 => [0x2734],
                0x2736 => [0x2737, 0x2738],
                0x2739 => [0x273A],
                0x2678 or 0x2680 or 0x2C84 or 0x2C58 => [],
                0x267F => [0x2682],
                0x3FA5 => [0x3FA6, 0x3FAB],
                0x3FA7 => [],
                0x30F5 or 0x30F7 => [],
                0x2C67 or 0x2C6A => [],
                0x2C6C => [0x2C6E, 0x2C6D, 0x2C6F],
                0x2C5B => [],
                0x2C5D => [0x2C5F, 0x2C5E],
                0x2CF8 => [0x2CF9, 0x2CEC],
                0x2CD2 => [0x2CD4, 0x2CD3, 0x2CD7, 0x2CD5, 0x2CD6],
                0x2720 => [0x271F],
                0x2721 => [0x2722, 0x2723],
                0x2703 => [0x2702],
                0x2708 => [0x2709, 0x270A],
                0x270B => [0x270C, 0x270D],
                _ => [0x25BD],
            };
            enemies.AddRange(_actors.Values.Where(a => adds.Contains(a.Oid)));
        }
        foreach (var actor in enemies.Where(a => !a.Dead && a.Targetable))
        {
            var position = actor.Position;
            var outside = !arena.Contains(position);
            var offset = position - center;
            if (outside && MathF.Abs(offset.X) >= .1f && !(MathF.Abs(offset.X) <= 1 && MathF.Abs(offset.Z) <= 1))
            {
                var point = new Vector2(position.X, position.Z);
                var closest = arena.GetContours().SelectMany(c => Enumerable.Range(0, c.Count).Select(i =>
                {
                    var a = new Vector2(c[i].X, c[i].Z);
                    var b = new Vector2(c[(i + 1) % c.Count].X, c[(i + 1) % c.Count].Z);
                    var edge = b - a;
                    return a + edge * Math.Clamp(Vector2.Dot(point - a, edge) / edge.LengthSquared(), 0, 1);
                })).MinBy(p => Vector2.DistanceSquared(p, point));
                position = new(closest.X, position.Y, closest.Y);
            }
            var color = new Vector4(1, 0, 0, 1);
            if (carnivalePrimary.Oid == 0x25CD && actor.Oid == 0x2700
                    || carnivalePrimary.Oid == 0x26FF && actor.Oid == 0x2701
                    || carnivalePrimary.Oid == 0x242D && actor.Oid is 0x2711 or 0x2712 or 0x2713 or 0x2714 or 0x2715 or 0x2716
                    || carnivalePrimary.Oid == 0x271B && actor.Oid == 0x271C
                    || carnivalePrimary.Oid == 0x26F8 && actor.Oid == 0x26F7
                    || carnivalePrimary.Oid == 0x26F9 && actor.Oid is 0x26FA or 0x26FB
                    || carnivalePrimary.Oid == 0x26F4 && actor.Oid == 0x26F3
                    || carnivalePrimary.Oid == 0x2721 && actor.Oid is 0x2722 or 0x2723
                    || carnivalePrimary.Oid is 0x2724 or 0x2725 && actor.Oid == 0x2726
                    || carnivalePrimary.Oid == 0x272C && actor.Oid is 0x272D or 0x272E
                    || carnivalePrimary.Oid == 0x2730 && actor.Oid == 0x2731
                    || carnivalePrimary.Oid == 0x26FE && actor.Oid is 0x26FC or 0x26FD
                    || carnivalePrimary.Oid == 0x267F && actor.Oid == 0x2682
                    || carnivalePrimary.Oid == 0x2CF8 && actor.Oid == 0x2CF9
                    || carnivalePrimary.Oid == 0x2C6C && actor.Oid is 0x2C6E or 0x2C6D or 0x2C6F
                    || carnivalePrimary.Oid == 0x3FA5 && actor.Oid is 0x3FA6 or 0x3FAB)
                color = new(1, 128f / 255, 0, 1);
            else if (carnivalePrimary.Oid == 0x2CF8 && actor.Oid == 0x2CEC)
                color = new(1, 1, 0, 1);
            yield return new($"carnivale/enemy/{actor.Id}", "敌人及朝向", shape, position, actor.Rotation,
                Navigate: false, OutlineOnly: outside, Unclipped: true, Color: color);
        }
    }

    private IEnumerable<string> CarnivaleHints(Vector3? player)
    {
        if (CarnivalePrecombatHint is { } hint) yield return hint;
        if (!IsCarnivale || carnivaleWiping || Active == null && !CarnivalePrecombatVisible) yield break;
        if (player is { } viewer && CarnivaleExplosionAoes().Any(a => a.Shape.Resolve(a.Position, 0, null).Contains(viewer, 0)))
            yield return carnivalePrimary?.Oid switch
            {
                0x2724 or 0x2725 => "In keg explosion radius!",
                0x2708 or 0x270B => "In bomb explosion radius!",
                _ => "In slime explosion radius!",
            };
        if (Active == null && carnivalePrimary?.Oid is 0x272A or 0x272C)
            yield return carnivalePrimary.Oid == 0x272A
                ? "Requirement for achievement: Don't kill any tentacles in act 3"
                : "Requirement for achievement: Don't kill any tentacles in this act";
        if (Active == null && carnivalePrimary?.Oid == 0x3FA5)
            yield return "Requirements for achievement: Don't destroy the crystal in act 2,\nuse no sprint, use all 6 magic elements, take no optional damage.";
        if (Active == null && carnivalePrimary?.Oid == 0x30F5)
            yield return "Requirements for achievement: Take no optional damage and finish faster\nthan ideal time.";
        if (Active == null && carnivalePrimary?.Oid == 0x2C67)
            yield return "Requirements for achievement: Take no damage, use all 6 magic elements,\nuse all 3 melee types, kill all 3 clones in act3 and finish faster than ideal time.";
        if (Active == null && carnivalePrimary?.Oid == 0x2678)
            yield return "Requirements for achievement: Take no damage, use all 6 magic elements,\nuse all 3 melee types and finish faster than ideal time";
        if (Active == null && carnivalePrimary?.Oid == 0x2717)
            yield return $"Requirement for achievement: Let {carnivalePrimary.Name} cast King's Will 3 times.";
        if (player is { } eyePlayer)
            foreach (var message in CarnivaleEyeHints(eyePlayer)) yield return message;
        if (!IsCarnivale || Active == null || player is not { } p) yield break;
        if (Active.BossOid == 0x2706)
        {
            yield return CarnivalePreparation(0x2706);
            if (CarnivaleVoltages.Any(c => c.Visibility.Contains(p, 0))) yield return "GTFO from aoe!";
        }
        foreach (var message in CarnivaleLastSongHints(p)) yield return message;
        foreach (var message in CarnivaleChargeHints(p)) yield return message;
        foreach (var message in CarnivaleClawHints(p)) yield return message;
        foreach (var message in CarnivaleRebekkahHints(p)) yield return message;
        foreach (var message in CarnivaleTyphonHints()) yield return message;
        foreach (var message in CarnivaleChimeraHints(p)) yield return message;
        foreach (var message in CarnivaleBoomHints(p)) yield return message;
        foreach (var message in CarnivaleBehemothHints(p)) yield return message;
        foreach (var message in CarnivalePitHints(p)) yield return message;
        foreach (var message in CarnivaleAzulmagiaHints(p)) yield return message;
        foreach (var message in CarnivalePapaHints(p)) yield return message;
        foreach (var message in CarnivaleSnorterHints(p)) yield return message;
        foreach (var message in CarnivaleDraugarHints(p)) yield return message;
        foreach (var message in CarnivaleRedHints(p)) yield return message;
        foreach (var message in CarnivaleSiegfriedHints(p)) yield return message;
        foreach (var message in CarnivaleGogoHints(p)) yield return message;
        foreach (var message in CarnivaleGoldorHints(p)) yield return message;
        if (CarnivaleBallastAoes().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(p)))
            yield return "GTFO from aoe!";
        foreach (var component in CarnivaleInterruptCasts.GroupBy(c => c.Rule.Action))
        {
            if (component.Key == 14753) yield return "Stun!";
            else if (component.Key == 14744)
                yield return carnivaleSelected!.ImpSongHint;
            else yield return "Interrupt!";
        }
        if (Active.BossOid == 0x25C0)
            yield return "Pudding is weak to wind spells.\nMarshmallow is weak to ice spells.\nBavarois is weak to earth spells.";
        if (Active.BossOid == 0x25C1)
            yield return "Gelato is weak to fire spells.\nFlan is weak to lightning spells.\nLicorice is weak to water spells.";
        if (Active.BossOid == 0x25D4)
            yield return $"{carnivalePrimary?.Name} is weak against water based spells.\nEarth based spells are useless against {carnivalePrimary?.Name}.";
        if (Active.BossOid is 0x25D4 or 0x242D && CarnivaleClawAoes()
            .Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(p, 0)))
            yield return "GTFO from voidzone!";
        if (Active.BossOid == 0x271B && _casts.Values.Any(c => c.Rule.Action == 14752))
            yield return "Calls Roselets. Prepare Ice Spikes if available.";
        if (Active.BossOid == 0x25D5)
            yield return $"{carnivalePrimary?.Name} is weak against lightning spells and can be frozen.";
        if (Active.BossOid == 0x270B) yield return CarnivalePreparation(0x270B);
        if (Active.BossOid is 0x25D4 or 0x242D or 0x25D5 or 0x270B or 0x2717 or 0x271B or 0x26F5 or 0x26F8 or 0x26F9 or 0x26F4 or 0x2720 or 0x2721 or 0x2724 or 0x2725 or 0x2727 or 0x2728 or 0x272A or 0x272B or 0x272C or 0x272F or 0x2730 or 0x26FE or 0x2732 or 0x2735 or 0x2736 or 0x2739 or 0x2678 or 0x267F or 0x2680 or 0x2C84 or 0x2C58 or 0x2CF8 or 0x2CD2 or 0x2C5B or 0x2C5D or 0x2C67 or 0x2C6A or 0x2C6C
            && OutputCasts.Any(c => c.Value.Rule.Shape?.Resolve(c.Value.Location, c.Value.Rotation, null).Contains(p) == true))
            yield return "GTFO from aoe!";
        if (Active.BossOid == 0x2703) yield return $"Hit the {carnivalePrimary?.Name} from a safe distance to win this act.";
        if (Active.BossOid == 0x2705) yield return CarnivalePreparation(0x2705);
        if (Active.BossOid == 0x2708) yield return "Hit the Cherry Bomb from a safe distance to win this act.";
        if (Active.BossOid == 0x25C8) yield return "Bats are weak to lightning.\nThe wolf is weak to fire.";
        if (Active.BossOid == 0x25BE && _casts.Values.Any(c => c.Rule.Action == 14199 && c.Rule.Shape!.Resolve(c.Location, c.Rotation, null).Contains(p)))
            yield return "GTFO from aoe!";
    }

    private IEnumerable<DungeonDamagePrediction> CarnivaleDamage()
    {
        foreach (var damage in CarnivaleChargeDamage()) yield return damage;
        foreach (var damage in CarnivaleSingleTargetDamage()) yield return damage;
        foreach (var damage in CarnivaleDraugarDamage()) yield return damage;
        foreach (var damage in CarnivaleRedDamage()) yield return damage;
        foreach (var damage in CarnivaleGogoDamage()) yield return damage;
        foreach (var damage in CarnivaleGoldorDamage()) yield return damage;
        if (!HasCarnivaleGaze && Active?.BossOid is not (0x26FE or 0x2732 or 0x2739 or 0x2C58)) yield break;
        var raidwide = Active?.BossOid switch { 0x26F4 => 14686u, 0x26FE => 15040u, 0x2732 => 15257u, 0x2739 => 15326u, 0x2C58 => 18603u, _ => 14882u };
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == raidwide))
        {
            var current = _actors.GetValueOrDefault(cast.Source)?.Cast;
            var activation = current?.Action == cast.Rule.Action ? current.FinishAt : cast.FinishAt;
            if (Active?.BossOid is 0x26FE or 0x2732 or 0x2739 or 0x2C58 && activation < _now.AddSeconds((double).3f))
                activation = _now.AddSeconds((double).3f);
            foreach (var actor in _actors.Values.Where(a => !a.Dead && a.PartySlot is >= 0 and < 64))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, activation, "Raidwide");
        }
    }

    private static bool CarnivaleExplosionMember(uint primary, uint oid) => primary switch
    {
        0x2724 or 0x2725 => oid == 0x2726,
        0x2706 => oid == 0x2707,
        0x2705 => oid == 0x2705,
        0x2708 => oid is 0x2709 or 0x270A,
        0x270B => oid is 0x270C or 0x270D,
        _ => false,
    };

    private IEnumerable<DungeonAmbientAoe> CarnivaleExplosionAoes()
    {
        if (carnivalePrimary == null) yield break;
        if (carnivalePrimary.Oid is 0x2703 or 0x2708 && !carnivalePrimary.Dead)
            yield return new("carnivale/explosion/primary", "Explosion radius",
                new CircleTemplate(carnivalePrimary.Oid == 0x2703 ? 7.6f : 10),
                carnivalePrimary.Position, Navigate: false, OutlineOnly: true, Color: new(1, 1, 0, 1));
        foreach (var item in carnivaleSelected!.Explosions)
            yield return new($"carnivale/explosion/{item.Generation}", "Explosion radius",
                new CircleTemplate(carnivalePrimary.Oid switch { 0x2705 or 0x2706 => 7.6f, 0x2724 or 0x2725 => 10, _ => 6 }),
                item.State.Actor.Position, Navigate: false, OutlineOnly: true, Color: carnivalePrimary.Oid is 0x2724 or 0x2725 ? null : new(1, 1, 0, 1));
    }

    private void CarnivaleDirector(uint update)
    {
        if (!IsCarnivale || update is not (0x40000005 or 0x40000011)) return;
        carnivaleWiping = update == 0x40000005;
        if (!carnivaleWiping) return;
        foreach (var module in carnivaleLoaded.ToArray()) CarnivaleUnload(module);
        carnivaleSelected = null;
        carnivalePrimary = null;
        ResetFight();
    }
}
