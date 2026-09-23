using System.Numerics;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Replay;
using PromeRotation.LogSystem;

internal static class MechanicReplayAdapterRegression
{
    public static void Run(Action<string, Action> run)
    {
        run("mechanic replay adapter converts full world frames field by field", () =>
        {
            var t0 = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
            var world = new ReplayWorldState { CurrentTime = t0 };
            world.Apply(new ReplayWorldFrame
            {
                Full = true,
                Territory = 1036,
                LocalPlayerId = 100,
                TargetId = 200,
                Party = [new(0, "Tank", 100), new(1, "Healer", 300)],
                Actors =
                [
                    new ReplayActorState { Id = 100, Oid = 0, Name = "玩家", Kind = 1, Position = new(80, 0, -45), HitboxRadius = 0.5f, ClassJob = 19, Level = 100, Hp = 9000, MaxHp = 10000, Mp = 900, MaxMp = 1000, Shield = 25, Targetable = true },
                    new ReplayActorState
                    {
                        Id = 200, Oid = 0x419B, Name = "敌人", Kind = 2, Hostile = true, Targetable = true, InCombat = true,
                        Position = new(100, 0, 100), Rotation = 1.5f, HitboxRadius = 3f, Level = 90, Hp = 500, MaxHp = 1000,
                        EventState = 7, ModelState = 3, AnimationState1 = 1, AnimationState2 = 2,
                        Cast = new(351, 100, new Vector3(90, 0, 90), null, t0.AddSeconds(-1), t0.AddSeconds(4), true, false),
                        Statuses = [new(42, 3, 300, t0.AddSeconds(30)), new(43, 0, 0, DateTime.MaxValue)],
                        Tethers = [new(7, 100)],
                        EventAnimation = 0x00010002,
                    },
                    new ReplayActorState { Id = 300, Oid = 0, Name = "盟友", Kind = 2, Hostile = false, Position = new(82, 0, -45), HitboxRadius = 0.5f },
                ],
            });

            var adapter = new ReplayFeedAdapter(1036, _ => 4);
            var frame = adapter.ConvertFrame(world);
            Check(frame.Territory == 1036 && frame.ContentFinderConditionId == 4, "territory/cfc lost");
            Check(frame.LocalPlayerId == 100 && frame.TargetId == 200, "local player/target lost");
            Check(frame.Actors.Length == 3, "actor count wrong");

            var player = frame.Actors.Single(a => a.EntityId == 100);
            Check(player.IsAlly && player.IsTargetable && !player.IsDead, "player flags wrong");
            Check(player.CurHp == 9000 && player.ShieldPercent == 25 && player.ClassJobId == 19 && player.Level == 100, "player fields lost");
            Check(player.SpawnIndex >= 0 && frame.Actors.Select(a => a.SpawnIndex).Distinct().Count() == 3, "spawn index must be stable and unique per frame");

            var enemy = frame.Actors.Single(a => a.EntityId == 200);
            Check(!enemy.IsAlly && enemy.InCombat && !enemy.IsDead, "hostile battle npc must stay enemy");
            Check(enemy.OID == 0x419B && enemy.EventState == 7 && enemy.ModelState == 3 && enemy.AnimState1 == 1 && enemy.AnimState2 == 2, "enemy fields lost");
            Check(enemy.TetherId == 7 && enemy.TetherTargetId == 100, "tether not mapped from first slot");
            Check(enemy.Cast != null && enemy.Cast.ActionId == 351 && enemy.Cast.Interruptible, "cast lost");
            Check(Math.Abs(enemy.Cast!.TotalTime - 5f) < 0.01f, "cast total time must derive from finish-start");
            Check(Math.Abs(enemy.Cast.Rotation - 1.5f) < 1e-6f, "cast rotation must fall back to actor rotation");
            Check(enemy.Statuses.Length == 2 && Math.Abs(enemy.Statuses[0].RemainingSeconds - 30f) < 0.5f, "status remaining time wrong");
            Check(enemy.Statuses[1].RemainingSeconds >= 100000f, "infinite status must clamp to the feed cap");
            Check(frame.Actors.Single(a => a.EntityId == 300).IsAlly, "non-hostile battle npc must count as ally");

            Check(frame.Party.Length == 2 && frame.Party[0].Slot == 0 && frame.Party[0].InstanceId == 100, "self must land in party slot 0");
            Check(frame.Party[1].Slot == 1 && frame.Party[1].InstanceId == 300 && frame.Party[1].Name == "盟友", "party order/name wrong");

            var frameEvents = adapter.DrainFrameEvents();
            var eanim = (MechanicEventEObjAnimation)frameEvents.Single();
            Check(eanim.SourceId == 200 && eanim.Param1 == 1 && eanim.Param2 == 2, "event animation must become an EObjAnimation event");
            Check(adapter.DrainFrameEvents().Count == 0, "frame events must drain once");
            adapter.ConvertFrame(world);
            Check(adapter.DrainFrameEvents().Count == 0, "unchanged event animation must not re-emit");
            world.Apply(new ReplayWorldFrame
            {
                Full = false, Territory = 1036, LocalPlayerId = 100,
                Actors = [world.Actors[200] with { EventAnimation = 0x00010003 }],
            });
            adapter.ConvertFrame(world);
            Check(((MechanicEventEObjAnimation)adapter.DrainFrameEvents().Single()).Param2 == 3, "changed event animation not re-emitted");
        });

        run("mechanic replay adapter maps transient events and skips state-covered ones", () =>
        {
            var adapter = new ReplayFeedAdapter(44);
            Check(adapter.TryConvertEvent(new LogSystemTetherEvent { SourceId = 1, TargetId = 2, TetherId = 9 }) is MechanicEventTether { SourceId: 1, TetherId: 9, TargetId: 2 }, "tether mapping wrong");
            Check(adapter.TryConvertEvent(new LogSystemTetherCancelEvent { SourceId = 1 }) is MechanicEventTetherCancel { SourceId: 1 }, "tether cancel mapping wrong");
            Check(adapter.TryConvertEvent(new LogSystemTargetIconEvent { SourceId = 1, TargetId = 2, MarkerId = 0x4010 }) is MechanicEventIcon { IconId: 0x4010, TargetId: 2, SourceId: 1 }, "icon mapping wrong");
            Check(adapter.TryConvertEvent(new LogSystemMarkerEvent { SourceId = 1, TargetId = 2, MarkerId = 2 }) is MechanicEventHeadMarker { SignId: 1, TargetId: 2 }, "head marker mapping wrong");
            Check(adapter.TryConvertEvent(new LogSystemMarkerEvent { MarkerId = 0 }) == null, "invalid marker id must be dropped");
            Check(adapter.TryConvertEvent(new LogSystemMapEffectEvent { Position = 3, Param1 = 0x11, Param2 = 0x22 }) is MechanicEventMapEffect { Index: 3, State: 0x220011 }, "map effect state packing wrong");
            Check(adapter.TryConvertEvent(new LogSystemEObjAnimationEvent { SourceId = 5, Param1 = 6, Param2 = 7 }) is MechanicEventEObjAnimation { SourceId: 5, Param1: 6, Param2: 7 }, "eobj animation mapping wrong");
            Check(adapter.TryConvertEvent(new LogSystemEObjSetStateEvent { SourceId = 5, State = 9 }) is MechanicEventEObjSetState { SourceId: 5, State: 9 }, "eobj state mapping wrong");
            Check(adapter.TryConvertEvent(new LogSystemDirectorUpdateEvent { Instance = 1, Command = 2, Param1 = 3, Param2 = 4, Param3 = 5, Param4 = 6 }) is MechanicEventDirectorUpdate { DirectorID: 1, UpdateID: 2, Param4: 6 }, "director update mapping wrong");
            Check(adapter.TryConvertEvent(new LogSystemNpcYellEvent { SourceId = 5, NpcYellId = 42 }) is MechanicEventNpcYell { SourceId: 5, Message: 42 }, "npc yell mapping wrong");
            Check(adapter.TryConvertEvent(new LogSystemPlayActionTimelineEvent { SourceId = 5, TimelineId = 100 }) is MechanicEventPlayActionTimeline { SourceId: 5, TimelineId: 100 }, "play action timeline mapping wrong");
            Check(adapter.TryConvertEvent(new LogSystemPlayActionTimelineEvent { SourceId = 5, TimelineId = 70000 }) == null, "oversized timeline id must be dropped");
            Check(adapter.TryConvertEvent(new LogSystemTargetVfxEvent { SourceId = 5, VfxId = 66 }) is MechanicEventVfx { SourceId: 5, VfxId: 66, TargetId: 0 }, "target vfx mapping wrong");

            LogSystemEvent[] stateCovered =
            [
                new LogSystemCastStartEvent(), new LogSystemCancelCastEvent(), new LogSystemInterruptCastEvent(),
                new LogSystemStatusAddEvent(), new LogSystemStatusRemoveEvent(),
                new LogSystemObjectCreateEvent(), new LogSystemObjectRemoveEvent(), new LogSystemTargetableEvent(),
                new LogSystemSetObjPosEvent(), new LogSystemKnockbackEvent(),
                new LogSystemTerritoryEvent(), new LogSystemChangeMapEvent(), new LogSystemWeatherChangedEvent(),
                new LogSystemBattleStartedEvent(), new LogSystemBattleEndedEvent(), new LogSystemCountdownStartedEvent(),
                new LogSystemChatLogEvent(), new LogSystemInstanceContentTextEvent(), new LogSystemMorelogCompatEvent(),
                new LogSystemTimelineExecutionEvent(),
            ];
            foreach (var ev in stateCovered)
                Check(adapter.TryConvertEvent(ev) == null, $"state-covered event {ev.GetType().Name} must not double-feed");

            adapter.TryConvertEvent(new LogSystemWaymarkEvent { MarkerId = 0, Active = true, Position = new(1, 2, 3) });
            var t0 = DateTime.UtcNow;
            var world = new ReplayWorldState { CurrentTime = t0 };
            world.Apply(new ReplayWorldFrame { Full = true, Territory = 44, LocalPlayerId = 0 });
            Check(adapter.ConvertFrame(world).Waymarks[0].Pos == new Vector3(1, 2, 3), "waymark state must ride the frame channel");
            adapter.TryConvertEvent(new LogSystemWaymarkEvent { MarkerId = 0, Active = false, Position = default });
            Check(adapter.ConvertFrame(world).Waymarks[0].Pos == null, "cleared waymark must clear the frame slot");
        });

        run("mechanic replay adapter merges degraded action effects by global sequence", () =>
        {
            var adapter = new ReplayFeedAdapter(44);
            var first = new LogSystemActionEffectEvent { SourceId = 10, ActionId = 500, TargetId = 100, GlobalSequence = 77, SourceRotation = 1f };
            var second = new LogSystemActionEffectEvent { SourceId = 10, ActionId = 500, TargetId = 101, GlobalSequence = 77 };
            var third = new LogSystemActionEffectEvent { SourceId = 10, ActionId = 500, TargetId = 102, GlobalSequence = 78 };
            Check(adapter.TryConvertEvent(first) == null && adapter.TryConvertEvent(second) == null, "merged effects must not emit per target");
            var merged = adapter.TryConvertEvent(third);
            Check(merged is MechanicEventActionEffect { GlobalSequence: 77 } effect && effect.Targets.Length == 2
                && effect.Targets[0].Id == 100 && effect.Targets[1].Id == 101, "same-sequence targets must merge into one cast event");
            Check(merged is MechanicEventActionEffect { Targets: [{ RawEffects.Length: 8 }, ..] }, "degraded effects keep empty raw effect slots");
            Check(adapter.FlushPendingEffect() is MechanicEventActionEffect { GlobalSequence: 78 }, "pending effect not flushed");
            Check(adapter.FlushPendingEffect() == null, "flush must be single-shot");
            Check(adapter.DegradedEffectCount == 2, "degraded effect counter wrong");
        });

        run("mechanic replay adapter never double-feeds casts covered by frames", () =>
        {
            var adapter = new ReplayFeedAdapter(44);
            Check(adapter.TryConvertEvent(new LogSystemCastStartEvent { SourceId = 10, ActionId = 500 }) == null, "cast start must stay on the frame channel");
            Check(adapter.TryConvertEvent(new LogSystemCancelCastEvent { SourceId = 10, ActionId = 500 }) == null, "cast cancel must stay on the frame channel");
            Check(adapter.TryConvertEvent(new LogSystemInterruptCastEvent { SourceId = 10, ActionId = 500 }) == null, "cast interrupt must stay on the frame channel");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
