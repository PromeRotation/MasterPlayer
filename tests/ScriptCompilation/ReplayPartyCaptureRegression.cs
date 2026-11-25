using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using PromeRotation.Helpers;
using NativeGroupManager = FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager;

internal static class ReplayPartyCaptureRegression
{
    internal static unsafe void Run(Assembly assembly)
    {
        var capture = assembly.GetType("MasterPlayer.Replay.ReplayWorldCapture", true)!
            .GetMethod("CaptureParty", BindingFlags.Static | BindingFlags.NonPublic)!;
        var priority = typeof(PartyHelper).Assembly.GetType("PromeRotation.Managers.PartyPriority.PartyPriorityManager", true)!;
        var set = priority.GetMethod("SetSortedParty", BindingFlags.Static | BindingFlags.Public)!;
        var oldMembers = ((IEnumerable)priority.GetProperty("PartyList", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null)!).Cast<IBattleChara>().ToArray();
        var adjusted = priority.GetField("_userAdjusted", BindingFlags.Static | BindingFlags.NonPublic)!;
        var oldAdjusted = adjusted.GetValue(null);
        var oldParty = Svc.Party;
        var oldCondition = Svc.Condition;
        var manager = (NativeGroupManager*)NativeMemory.AllocZeroed((nuint)sizeof(NativeGroupManager));
        var address = (nint)manager;
        var replay = false;
        static IBattleChara Actor(uint id) => CatalogServiceProxy.Create<IBattleChara>((method, _) => method switch
        {
            "get_EntityId" => (object)id,
            "get_GameObjectId" => (ulong)id,
            _ => throw new InvalidOperationException("Unexpected party actor access: " + method)
        });
        void Service(string name, object? value) => typeof(Svc).GetProperty(name, BindingFlags.Static | BindingFlags.Public)!.SetValue(null, value);
        (int Slot, ulong Id)[] Capture()
        {
            return ((IEnumerable)capture.Invoke(null, null)!).Cast<object>().Select(p =>
                ((int)p.GetType().GetProperty("Slot")!.GetValue(p)!, (ulong)p.GetType().GetProperty("EntityId")!.GetValue(p)!)).ToArray();
        }
        try
        {
            set.Invoke(null, [new IBattleChara[] { Actor(100), Actor(101) }]);
            Service("Party", CatalogServiceProxy.Create<IPartyList>((method, _) => method == "get_GroupManagerAddress"
                ? address : throw new InvalidOperationException("Unexpected party-list access: " + method)));
            Service("Condition", CatalogServiceProxy.Create<ICondition>((method, _) => method == "get_Item"
                ? replay : throw new InvalidOperationException("Unexpected condition access: " + method)));
            manager->MainGroup.AllianceFlags = 1;
            manager->ReplayGroup.AllianceFlags = 1;
            manager->MainGroup.AllianceMembers[0].EntityId = 200;
            manager->MainGroup.AllianceMembers[0].Flags = 1;
            manager->MainGroup.AllianceMembers[1].EntityId = 201; // Invalid Flags with nonzero ID.
            manager->MainGroup.AllianceMembers[2].Flags = 1; // Zero ID.
            manager->MainGroup.AllianceMembers[3].EntityId = 0xE0000000;
            manager->MainGroup.AllianceMembers[3].Flags = 1;
            manager->MainGroup.AllianceMembers[4].EntityId = 100; // Duplicate own-party ID.
            manager->MainGroup.AllianceMembers[4].Flags = 1;
            manager->MainGroup.AllianceMembers[5].EntityId = 200; // Duplicate alliance ID.
            manager->MainGroup.AllianceMembers[5].Flags = 1;
            manager->MainGroup.AllianceMembers[8].EntityId = 208;
            manager->MainGroup.AllianceMembers[8].Flags = 1;
            manager->MainGroup.AllianceMembers[15].EntityId = 215;
            manager->MainGroup.AllianceMembers[15].Flags = 1;
            manager->MainGroup.AllianceMembers[16].EntityId = 216; // Not in a regular alliance.
            manager->MainGroup.AllianceMembers[16].Flags = 1;
            manager->ReplayGroup.AllianceMembers[0].EntityId = 300;
            manager->ReplayGroup.AllianceMembers[0].Flags = 1;
            Check(Capture().SequenceEqual(new (int, ulong)[] { (0, 100), (2, 101), (8, 200), (16, 208), (23, 215) }),
                "Production CaptureParty lost PR slots, alliance holes/flags, deduplication or regular-alliance bounds.");
            replay = true;
            Check(Capture().SequenceEqual(new (int, ulong)[] { (0, 100), (2, 101), (8, 300) }), "ARR captured MainGroup alliance members.");
            replay = false;
            manager->MainGroup.AllianceFlags = 3;
            Check(Capture().Length == 2, "Small-group alliance was treated as the source's regular 8..23 slots.");
            manager->MainGroup.AllianceFlags = 0;
            Check(Capture().Length == 2, "Non-alliance read stale alliance slots.");
            manager->MainGroup.MemberCount = 3;
            manager->MainGroup.PartyMembers[0].EntityId = 100;
            manager->MainGroup.PartyMembers[1].EntityId = 101;
            manager->MainGroup.PartyMembers[2].EntityId = 102;
            Check(Capture().SequenceEqual(new (int, ulong)[] { (0, 100), (2, 101), (1, 102) }),
                "Roster member without an actor was lost or replaced a resolved PR priority slot.");
            manager->MainGroup.MemberCount = 2;
            Check(Capture().Length == 2, "Removed roster member was retained from stale native memory.");
            manager->ReplayGroup.MemberCount = 1;
            manager->ReplayGroup.PartyMembers[0].EntityId = 103;
            replay = true;
            Check(Capture().SequenceEqual(new (int, ulong)[] { (0, 100), (2, 101), (1, 103), (8, 300) }),
                "ARR missing-actor roster read MainGroup instead of ReplayGroup.");
            replay = false;
            address = 0;
            Check(Capture().Length == 2, "Zero GroupManager address lost the own party.");
            Service("Party", null);
            Check(Capture().Length == 2, "Missing party service lost the own party.");
        }
        finally
        {
            Service("Party", oldParty);
            Service("Condition", oldCondition);
            set.Invoke(null, [oldMembers]);
            adjusted.SetValue(null, oldAdjusted);
            NativeMemory.Free(manager);
        }
        Console.WriteLine("PASS production party capture: PR priority slots, missing-actor native roster/member removal, alliance 8..23/holes/Flags/dedup, no object/name lookup, ARR ReplayGroup, small-group exclusion, zero/null services and restoration");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
