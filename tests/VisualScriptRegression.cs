using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using MasterPlayer.Dodge;
using MasterPlayer.Scripting;
using MasterPlayer.Scripting.Visual;
using PromeRotation.Helpers;
using PromeRotation.LogSystem;
using MasterPlayer.Spatial.SafeZone;

internal static class VisualScriptRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("visual JSON preserves identity, Unicode, geometry, bindings and disabled rules", () =>
        {
            var doc = Document();
            doc.Name = "高一 / 钢铁 ## 月环";
            doc.Rules[0].Origin = VisualAoeOrigin.Fixed;
            doc.Rules[0].Position = new Vector3(12.5f, 9f, -30f);
            doc.Rules[0].Offset = new Vector2(1.25f, -2.5f);
            doc.Rules[0].CasterDataId = uint.MaxValue;
            doc.Rules[0].Enabled = false;
            doc.Rules[0].AngleOffsetDegrees = -45f;
            var loaded = VisualScriptStore.Parse(VisualScriptStore.Serialize(doc));
            Check(loaded.Id == doc.Id && loaded.Name == doc.Name, "Script identity and label must round-trip.");
            Check(loaded.Rules[0].Position == doc.Rules[0].Position && loaded.Rules[0].Offset == doc.Rules[0].Offset, "Vector fields must not be lost by JSON serialization.");
            Check(!loaded.Rules[0].Enabled && loaded.Rules[0].CasterDataId == uint.MaxValue, "Disabled rules and full-width Data IDs must be preserved.");
            Check(loaded.Rules[0].AngleOffsetDegrees == -45f, "Signed angle offsets must survive JSON persistence.");
            var copy = VisualScriptStore.CloneRule(loaded.Rules[0]);
            copy.Radius = 9f;
            Check(copy.Id != loaded.Rules[0].Id && loaded.Rules[0].Radius == 5f, "Copies need independent identity and mutable data.");
            Check(copy.AngleOffsetDegrees == -45f, "Copying a rule must preserve its angle offset.");
            var legacy = JsonNode.Parse(VisualScriptStore.Serialize(doc))!;
            legacy["Rules"]![0]!.AsObject().Remove("AngleOffsetDegrees");
            Check(VisualScriptStore.Parse(legacy.ToJsonString()).Rules[0].AngleOffsetDegrees == 0f,
                "Documents created before angle offsets must retain their original direction.");
        });

        run("visual JSON rejects incompatible versions, unknown members, nulls and duplicate identities", () =>
        {
            var json = VisualScriptStore.Serialize(Document());
            Reject(() => VisualScriptStore.Parse(json.Replace("\"SchemaVersion\": 1", "\"SchemaVersion\": 2")));
            Reject(() => VisualScriptStore.Parse(json.Replace("\"SchemaVersion\": 1,", "")));
            Reject(() => VisualScriptStore.Parse(json.Replace("\"Circle\"", "\"UnsupportedShape\"")));
            Reject(() => VisualScriptStore.Parse(json.Replace("\"Circle\"", "0")));
            Reject(() => VisualScriptStore.Parse(json.Replace("\"Note\":", "\"FutureFeature\": true, \"Note\":")));
            Reject(() => VisualScriptStore.Parse("null"));
            var doc = Document();
            doc.Rules.Add(doc.Rules[0]);
            Check(doc.Validate().Count > 0, "Duplicate rule IDs must be rejected.");
            doc.Rules = null!;
            Check(doc.Validate().Count > 0, "Null collection must yield validation errors.");
            doc.Rules = [null!];
            Check(doc.Validate().Count > 0, "Null rule must yield validation errors.");
        });

        run("visual validation covers IDs, nonfinite values and invalid binding combinations", () =>
        {
            var invalid = new Action<VisualScriptDefinition>[]
            {
                d => d.TerritoryId = 0, d => d.TerritoryId = 65536,
                d => d.Rules[0].ActionId = -1, d => d.Rules[0].ActionId = 65536,
                d => d.Rules[0].Radius = 0, d => { d.Rules[0].Shape = VisualAoeShape.Rectangle; d.Rules[0].Width = float.PositiveInfinity; },
                d => d.Rules[0].Offset = new Vector2(float.NaN, 0),
                d => d.Rules[0].Position = new Vector3(0, float.NaN, 0),
                d => d.Rules[0].AngleOffsetDegrees = float.NaN,
                d => d.Rules[0].AngleOffsetDegrees = float.PositiveInfinity,
                d => d.Rules[0].HitDurationSeconds = 0, d => d.Rules[0].DelaySeconds = -1,
                d => { d.Rules[0].Shape = VisualAoeShape.Fan; d.Rules[0].AngleDegrees = 361; },
                d => { d.Rules[0].Shape = VisualAoeShape.Donut; d.Rules[0].InnerRadius = d.Rules[0].Radius; },
                d => { d.Rules[0].Origin = VisualAoeOrigin.CastLocation; d.Rules[0].Tracking = VisualAoeTracking.FollowPosition; },
            };
            foreach (var change in invalid)
            {
                var doc = Document();
                change(doc);
                Check(doc.Validate().Count > 0, "Invalid definition must be blocked before persistence/runtime.");
            }
            var boundary = Document();
            boundary.TerritoryId = ushort.MaxValue;
            boundary.Rules[0].ActionId = ushort.MaxValue;
            boundary.Rules[0].AngleDegrees = 360f;
            Check(boundary.Validate().Count == 0, "Legal boundary values must remain usable.");
        });

        run("visual save is atomic, preserves external edits and rejects invalid replacement", () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "MasterPlayerVisualTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "test.mechanic.json");
            try
            {
                var doc = Document();
                var saved = VisualScriptStore.Save(path, doc, null);
                Check(!File.ReadAllBytes(path).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }), "UTF-8 output must not carry a BOM.");
                doc.Name = "updated";
                var next = VisualScriptStore.Save(path, doc, saved);
                Check(VisualScriptStore.Parse(File.ReadAllText(path)).Name == "updated", "Successful saves must replace the document.");
                doc.Rules[0].Radius = -1;
                Reject(() => VisualScriptStore.Save(path, doc, next));
                Check(File.ReadAllText(path) == next, "Invalid save must leave previous file intact.");
                doc.Rules[0].Radius = 5;
                File.WriteAllText(path, "external edit");
                Reject(() => VisualScriptStore.Save(path, doc, next));
                Reject(() => VisualScriptStore.Delete(path, next));
                Check(File.ReadAllText(path) == "external edit", "Save/delete must not overwrite conflicting external edits.");
                Reject(() => VisualScriptStore.Save(path, doc, null));
                Reject(() => VisualScriptStore.Save(Path.Combine(path, "child.mechanic.json"), doc, null));
                Check(Directory.GetFiles(directory).Length == 1, "Failed saves must leave no temporary files.");
                VisualScriptStore.Delete(path, "external edit");
                Reject(() => VisualScriptStore.Save(path, doc, next));
                Check(!File.Exists(path), "Saving a deleted document requires an explicit reload/new save.");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                Directory.Delete(directory);
            }
        });

        run("visual shape switching recovers from invalid input without hidden validation traps", () =>
        {
            var rule = Document().Rules[0];
            foreach (var shape in new[] { VisualAoeShape.Rectangle, VisualAoeShape.RayRectangle })
            {
                rule.Shape = shape;
                rule.Width = 0;
                Check(rule.ValidateGeometry().Count > 0, "Zero width must be rejected before preview Resolve is called.");
                rule.Width = 6;
                rule.Length = -1;
                Check(rule.ValidateGeometry().Count > 0, "Negative length must be rejected before preview Resolve is called.");
            }
            rule.Shape = VisualAoeShape.Fan;
            rule.AngleDegrees = 0;
            rule.AngleOffsetDegrees = float.NaN;
            Check(rule.ValidateGeometry().Count > 0, "Nonfinite angle offsets must stop preview resolution.");
            rule.ChangeShape(VisualAoeShape.Circle);
            Check(rule.Validate().Count == 0, "Switching away from an invalid fan must leave an editable, saveable circle.");
            rule.Radius = float.NaN;
            rule.ChangeShape(VisualAoeShape.Rectangle);
            Check(rule.Validate().Count == 0, "Inactive invalid radius must not poison rectangle persistence.");
            rule.Position = new Vector3(float.NaN);
            rule.RotationDegrees = float.PositiveInfinity;
            rule.ResetInactiveValues();
            Check(rule.Validate().Count == 0, "Changing away from fixed bindings must not leave invalid hidden values.");
            VisualScriptStore.Parse(VisualScriptStore.Serialize(new VisualScriptDefinition { TerritoryId = 1234, Rules = [rule] }));
        });

        run("visual timing compensates event delay and never revives expired AOEs", () =>
        {
            var now = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
            var rule = Document().Rules[0];
            var cast = Cast().Event with { Timestamp = now.AddSeconds(-2) };
            Check(VisualMechanicScript.TryTiming(cast, rule, now, out var delay, out var duration, out var end), "Delayed event must retain its remaining lifetime.");
            Check(Math.Abs(delay - 2) < 0.0001 && Math.Abs(duration - 0.6) < 0.0001 && Math.Abs((end - now).TotalSeconds - 2.6) < 0.0001,
                "A two-second queue delay must not move the hit two seconds into the future.");
            Check(VisualMechanicScript.TryTiming(cast with { Timestamp = now.AddSeconds(-4.5).ToLocalTime() }, rule, now, out delay, out duration, out _)
                && delay == 0f && Math.Abs(duration - 0.1) < 0.0001, "Already-active hit window must be clipped, with local timestamps converted to UTC.");
            Check(!VisualMechanicScript.TryTiming(cast with { Timestamp = now.AddSeconds(-10) }, rule, now, out _, out _, out _), "Expired event must not create a fresh telegraph.");
            Check(!VisualMechanicScript.TryTiming(cast with { CastTime = float.NaN }, rule, now, out _, out _, out _), "Invalid packet times must not produce an AOE.");
        });

        run("visual caster filtering is per rule and never assumes a missing actor matches", () =>
        {
            var rule = Document().Rules[0];
            rule.CasterDataId = 42;
            var cast = Cast();
            Check(VisualAoeBinding.Matches(rule, cast), "Configured actor must match.");
            Check(!VisualAoeBinding.Matches(rule, cast with { Caster = cast.Caster! with { DataId = 43 } }), "Other NPCs with the same action must remain unaffected.");
            Check(!VisualAoeBinding.Matches(rule, cast with { Caster = null }), "Missing Data ID must not pass a constrained filter.");
            rule.CasterDataId = 0;
            Check(VisualAoeBinding.Matches(rule, cast with { Caster = null }), "Unconstrained off-table casters can use packet positions.");
            rule.Enabled = false;
            Check(!VisualAoeBinding.Matches(rule, cast), "Disabled rule must not match.");
        });

        run("visual origins keep packet locations, target actors and fixed coordinates distinct", () =>
        {
            var rule = Document().Rules[0];
            rule.Offset = new Vector2(2, -3);
            var cast = Cast();
            var expected = new[] { cast.Event.SourcePosition!.Value, cast.Event.EffectPosition, cast.Target!.Position, new Vector3(70, 8, 90) };
            rule.Position = expected[3];
            for (var i = 0; i < expected.Length; i++)
            {
                rule.Origin = (VisualAoeOrigin)i;
                Check(VisualAoeBinding.TryCreate(rule, cast, out var binding, out _), "Every configured origin must resolve.");
                Check(binding is StaticTransform t && t.Position == expected[i] + new Vector3(2, 0, -3), "Origin and world offset must resolve exactly once.");
            }
            rule.Origin = VisualAoeOrigin.Target;
            Check(!VisualAoeBinding.TryCreate(rule, cast with { Target = null }, out _, out _), "Missing target must not silently use packet effect position.");
            rule.Origin = VisualAoeOrigin.Caster;
            Check(!VisualAoeBinding.TryCreate(rule, cast with { Caster = null, Event = cast.Event with { SourcePosition = null } }, out _, out _), "Missing caster position must not create an AOE at zero.");
        });

        run("visual cast direction never falls back to actor facing for directional shapes", () =>
        {
            var rule = Document().Rules[0];
            rule.Shape = VisualAoeShape.Fan;
            var cast = Cast();
            Check(VisualAoeBinding.TryCreate(rule, cast, out var binding, out _), "Known cast direction must resolve.");
            Check(binding is StaticTransform t && t.Rotation == 1.2f, "Cast facing must stay distinct from the actor's 0.3 rad facing.");
            Check(!VisualAoeBinding.TryCreate(rule, cast with { CastRotation = null }, out _, out _), "Missing native cast direction must fail explicitly.");
            rule.Direction = VisualAoeDirection.Fixed;
            rule.RotationDegrees = 90;
            Check(VisualAoeBinding.TryCreate(rule, cast with { CastRotation = null }, out binding, out _) && binding is StaticTransform fixedTransform
                && MathF.Abs(fixedTransform.Rotation - MathF.PI / 2f) < 0.0001f, "Explicit fixed angle must work without native cast info.");
            rule.Shape = VisualAoeShape.Circle;
            rule.Direction = VisualAoeDirection.CastRotation;
            Check(VisualAoeBinding.TryCreate(rule, cast with { CastRotation = null }, out _, out _), "Circular geometry does not require a directional cast read.");
        });

        run("visual follow modes bind entity IDs with the correct offset coordinate system", () =>
        {
            var rule = Document().Rules[0];
            rule.Origin = VisualAoeOrigin.Target;
            rule.Offset = new Vector2(2, -4);
            rule.Shape = VisualAoeShape.RayRectangle;
            rule.Tracking = VisualAoeTracking.FollowPosition;
            Check(VisualAoeBinding.TryCreate(rule, Cast(), out var binding, out _) && binding is FollowEntityPosition p
                && p.EntityId == 2 && p.WorldOffsetXZ == rule.Offset && p.Rotation == 1.2f && p.MissingPolicy == MissingEntityPolicy.RemoveArea,
                "Position-only following must freeze rotation and use target EntityId, not DataId.");
            rule.Tracking = VisualAoeTracking.FollowPositionAndRotation;
            Check(VisualAoeBinding.TryCreate(rule, Cast() with { CastRotation = null }, out binding, out _) && binding is FollowEntityPositionAndRotation pr
                && pr.EntityId == 2 && pr.LocalOffsetXZ == rule.Offset, "Both-follow mode must use local offsets and ordinary actor rotation.");
            Check(!VisualAoeBinding.TryCreate(rule, Cast() with { Target = null }, out _, out _), "Cannot start a follow binding without the actor.");
        });

        run("visual templates preserve holes, full fan angles and rectangle anchor semantics", () =>
        {
            var rule = Document().Rules[0];
            rule.Shape = VisualAoeShape.Donut;
            var donut = rule.CreateTemplate().Resolve(Vector3.Zero, 0, null);
            Check(!donut.Contains(Vector3.Zero) && donut.Contains(new Vector3(4, 0, 0)), "Donut center must remain safe.");
            Check(donut.GetContours().Count == 2, "Preview must receive both donut contours.");
            rule.Shape = VisualAoeShape.Fan;
            rule.AngleDegrees = 270;
            var fan = rule.CreateTemplate().Resolve(Vector3.Zero, 0, null);
            Check(fan.Contains(new Vector3(2, 0, 0)) && !fan.Contains(new Vector3(0, 0, -2)), "Fan input is the full opening angle.");
            rule.Shape = VisualAoeShape.Rectangle;
            var rectangle = rule.CreateTemplate().Resolve(Vector3.Zero, 0, null);
            rule.Shape = VisualAoeShape.RayRectangle;
            var ray = rule.CreateTemplate().Resolve(Vector3.Zero, 0, null);
            Check(rectangle.Contains(new Vector3(0, 0, -5)) && !ray.Contains(new Vector3(0, 0, -5)), "Center and rear-edge anchored rectangles must differ.");
            Check(ray.Contains(new Vector3(0, 0, 15)) && !rectangle.Contains(new Vector3(0, 0, 15)), "Ray must extend its full length in front of origin.");
        });

        foreach (var tracking in new[] { VisualAoeTracking.Snapshot, VisualAoeTracking.FollowPosition })
        foreach (var direction in Enum.GetValues<VisualAoeDirection>())
        {
            run($"visual angle offset composes with {tracking} / {direction}", () =>
            {
                using var actors = new ActorTable();
                var rule = Document().Rules[0];
                rule.Shape = VisualAoeShape.Fan;
                rule.Tracking = tracking;
                rule.Direction = direction;
                rule.RotationDegrees = 90f;
                rule.AngleOffsetDegrees = -45f;
                rule.Offset = new Vector2(2f, -3f);
                var cast = Cast();
                Check(VisualAoeBinding.TryCreate(rule, cast, out var binding, out _), "Binding must remain valid with an angle offset.");
                using var manager = new SafeZoneManager(new SafeZoneSetting { EnableSafeZone = true });
                new SafeZoneRegistry(manager).AddDelayedHit(rule.CreateTemplate(), binding, 5f);
                var geometry = manager.BuildSnapshot(DateTime.UtcNow).DangerAreas.Single().Geometry;
                var basis = direction switch
                {
                    VisualAoeDirection.CastRotation => 1.2f,
                    VisualAoeDirection.CasterRotation => 0.3f,
                    _ => MathF.PI / 2f,
                };
                CheckRotation(geometry.Rotation, basis - MathF.PI / 4f, "Final geometry must apply the angle offset exactly once.");
                Check(geometry.Position == actors.Position + new Vector3(2f, 0f, -3f), "Angular offset must not rotate the world position offset.");
                var preview = rule.CreateTemplate().Resolve(geometry.Position, basis, null);
                Check(preview.GetContours()[0].SequenceEqual(geometry.GetContours()[0]), "Preview and solver geometry must use the same angle composition.");
            });
        }

        run("visual angle offsets support left, right, reverse and full turns", () =>
        {
            var rule = Document().Rules[0];
            rule.Shape = VisualAoeShape.RayRectangle;
            var center = new Vector3(10f, 2f, 20f);
            foreach (var (offset, forward) in new[]
            {
                (90f, new Vector3(-5f, 0f, 0f)), (-90f, new Vector3(5f, 0f, 0f)),
                (180f, new Vector3(0f, 0f, 5f)), (450f, new Vector3(-5f, 0f, 0f)),
                (-450f, new Vector3(5f, 0f, 0f)), (360f, new Vector3(0f, 0f, -5f)),
            })
            {
                rule.AngleOffsetDegrees = offset;
                var geometry = rule.CreateTemplate().Resolve(center, MathF.PI, null);
                Check(geometry.Contains(center + forward) && !geometry.Contains(center - forward),
                    $"Facing north with offset {offset} must place the ray in the intended direction.");
                Check(geometry.Position == center, "Rotating the ray must retain its rear-edge anchor.");
            }
            foreach (var shape in new[] { VisualAoeShape.Fan, VisualAoeShape.Rectangle, VisualAoeShape.RayRectangle })
            {
                rule.Shape = shape;
                rule.AngleOffsetDegrees = 90f;
                CheckRotation(rule.CreateTemplate().Resolve(center, 0.3f, null).Rotation, 0.3f + MathF.PI / 2f,
                    "Every directional shape must support angle offsets.");
            }
            rule.Shape = VisualAoeShape.Circle;
            Check(rule.CreateTemplate() is CircleTemplate, "Circular geometry must remain unchanged.");
            rule.Shape = VisualAoeShape.Donut;
            Check(rule.CreateTemplate() is DonutTemplate, "Donut geometry must remain unchanged.");
            rule.Shape = VisualAoeShape.RayRectangle;
            rule.AngleOffsetDegrees = 0f;
            Check(rule.CreateTemplate() is RayRectangleTemplate, "The default offset must preserve the existing template.");
        });

        run("visual angle offset remains relative to moving actor facing without orbiting its anchor", () =>
        {
            using var actors = new ActorTable();
            var doc = Document();
            var rule = doc.Rules[0];
            rule.Shape = VisualAoeShape.RayRectangle;
            rule.Tracking = VisualAoeTracking.FollowPositionAndRotation;
            rule.Offset = new Vector2(2f, -3f);
            rule.AngleOffsetDegrees = 90f;
            using var fight = new Fight(doc);
            fight.Script.SendForTest(Cast().Event);
            foreach (var facing in new[] { 0.3f, MathF.PI / 2f, MathF.PI })
            {
                actors.Rotation = facing;
                var geometry = fight.Areas.Single().Geometry;
                CheckRotation(geometry.Rotation, facing + MathF.PI / 2f, "Follow mode must preserve the relative angular offset as the actor turns.");
                var expectedPosition = GeometryHelper.LocalToWorld(rule.Offset, actors.Position, facing);
                Check(Vector3.Distance(geometry.Position, expectedPosition) < 0.0001f, "The anchor must still follow the actor's local offset, not the rotated AOE frame.");
                var preview = rule.CreateTemplate().Resolve(expectedPosition, facing, null);
                Check(preview.GetContours()[0].SequenceEqual(geometry.GetContours()[0]), "Follow-mode preview and runtime must match.");
            }
            actors.Available = false;
            Check(fight.Areas.Count == 0, "A rotated AOE must retain missing-entity cleanup.");
        });

        run("visual rotated templates preserve danger margin and length overrides", () =>
        {
            var rule = Document().Rules[0];
            rule.AngleOffsetDegrees = 90f;
            foreach (var shape in new[] { VisualAoeShape.Fan, VisualAoeShape.Rectangle, VisualAoeShape.RayRectangle })
            {
                rule.Shape = shape;
                var template = rule.CreateTemplate();
                var inflated = DangerMarginInflator.Inflate(template, 0.5f);
                var original = template.Resolve(Vector3.Zero, 0.3f, null);
                var geometry = inflated.Resolve(Vector3.Zero, 0.3f, null);
                CheckRotation(geometry.Rotation, original.Rotation, "Danger inflation must preserve the angular offset.");
                Check(geometry.Kind == original.Kind, "The rotated template must retain shape classification for rendering.");
                var localSample = shape == VisualAoeShape.Fan
                    ? new Vector2(0f, -5.25f)
                    : new Vector2(3.25f, -2f);
                var sample = GeometryHelper.LocalToWorld(localSample, Vector3.Zero, geometry.Rotation);
                Check(!original.Contains(sample) && geometry.Contains(sample), "The configured danger margin must still expand rotated shapes.");
                Check(ReferenceEquals(DangerMarginInflator.Inflate(template, 0f), template), "Zero margin must preserve the template.");
            }
            rule.Shape = VisualAoeShape.RayRectangle;
            var shortened = rule.CreateTemplate().Resolve(Vector3.Zero, 0f, 3f);
            Check(shortened.Contains(new Vector3(2f, 0f, 0f)) && !shortened.Contains(new Vector3(5f, 0f, 0f)),
                "Angle wrapping must forward host-provided length overrides.");
        });

        run("visual runtime registers timed previews, scopes suppression and cancels one cast", () =>
        {
            using var actors = new ActorTable();
            var doc = Document();
            doc.Rules[0].CasterDataId = 42;
            doc.Rules[0].DelaySeconds = 1.5f;
            doc.Rules[0].HitDurationSeconds = 2f;
            using var fight = new Fight(doc);
            var cast = Cast().Event;
            Check(fight.Script.SuppressesGenericDodge(cast), "Matching viable rule must suppress the generic duplicate.");
            Check(!fight.Script.SuppressesGenericDodge(cast with { SourceId = 2 }), "Another caster's same action must not be suppressed.");
            var before = DateTime.UtcNow;
            fight.Script.SendForTest(cast);
            var area = fight.Areas.Single();
            var window = area.DangerWindows.Single();
            Check((window.DangerFrom - before).TotalSeconds is >= 5.49 and < 6.5, "Danger must begin after cast time plus configured delay.");
            Check(Math.Abs((window.DangerUntil!.Value - window.DangerFrom).TotalSeconds - 2) < 0.01, "Hit duration must not be lost.");
            fight.Script.SendForTest(new LogSystemCancelCastEvent { SourceId = 2, ActionId = 123 });
            Check(fight.Areas.Count == 1, "Unrelated cancellation must preserve the AOE.");
            fight.Script.SendForTest(new LogSystemCancelCastEvent { SourceId = 1, ActionId = 123 });
            Check(fight.Areas.Count == 0, "Cancellation must remove matching pending AOEs immediately.");
        });

        run("visual repeated casts preserve earlier delayed hits when a later cast is interrupted", () =>
        {
            using var actors = new ActorTable();
            using var fight = new Fight(Document());
            var cast = Cast().Event;
            fight.Script.SendForTest(cast);
            fight.Script.SendForTest(cast);
            Check(fight.Areas.Count == 2, "Repeated cast events represent independent delayed AOEs.");
            fight.Script.SendForTest(new LogSystemInterruptCastEvent { SourceId = 1, ActionId = 123 });
            Check(fight.Areas.Count == 1, "Interrupt only the latest cast from this source.");
            fight.Script.EndBattleForTest();
            Check(fight.Areas.Count == 0, "Battle end must clear pending AOEs.");
        });

        run("visual immutable running snapshot and isolated stop survive simultaneous scripts", () =>
        {
            using var actors = new ActorTable();
            var doc = Document();
            using var first = new Fight(doc);
            doc.Id = Guid.NewGuid().ToString("D");
            using var second = new Fight(doc);
            second.Script.SafeZone.ShareManagerForTest(first.Script.SafeZone.Manager);
            doc.Rules[0].Enabled = false;
            var cast = Cast().Event;
            first.Script.SendForTest(cast);
            second.Script.SendForTest(cast);
            Check(first.Areas.Count == 2, "Both scripts must register in the same underlying manager, independent of draft edits.");
            first.Script.StopForTest();
            first.Script.StopForTest();
            first.Script.SendForTest(cast);
            Check(first.Areas.Count == 1 && second.Areas.Count == 1, "Stopped instance must not register or remove another instance's AOE from the shared manager.");
            Check(!first.Script.SuppressesGenericDodge(cast) && second.Script.SuppressesGenericDodge(cast), "Only active instances can suppress generic AOE detection.");
        });

        run("visual following resolves motion and removes the AOE when its actor disappears", () =>
        {
            using var actors = new ActorTable();
            var doc = Document();
            doc.Rules[0].Tracking = VisualAoeTracking.FollowPosition;
            using var fight = new Fight(doc);
            fight.Script.SendForTest(Cast().Event);
            Check(fight.Areas.Single().Geometry.Position == actors.Position, "Follow geometry must use current actor position.");
            actors.Position = new Vector3(90, 8, -15);
            Check(fight.Areas.Single().Geometry.Position == actors.Position, "Position-follow must resolve moving actors.");
            actors.Available = false;
            Check(fight.Areas.Count == 0, "Missing bound entity must remove, not freeze, the AOE.");
        });

        run("visual disabled and unresolved directional rules neither run nor suppress generic AOEs", () =>
        {
            using var actors = new ActorTable();
            var doc = Document();
            doc.Rules[0].Enabled = false;
            using var disabled = new Fight(doc);
            Check(!disabled.Script.SuppressesGenericDodge(Cast().Event), "Disabled rule must not suppress generic AOE detection.");
            disabled.Script.SendForTest(Cast().Event);
            Check(disabled.Areas.Count == 0, "Disabled rule must not register.");
            doc.Rules[0].Enabled = true;
            doc.Rules[0].Shape = VisualAoeShape.Fan;
            using var unresolved = new Fight(doc);
            Check(!unresolved.Script.SuppressesGenericDodge(Cast().Event), "Unavailable CastRotation must leave generic handling available.");
            unresolved.Script.SendForTest(Cast().Event);
            Check(unresolved.Areas.Count == 0 && unresolved.Script.Errors.Count == 1, "Failure must be reported without producing a wrong-facing AOE.");
        });
    }

    private static VisualScriptDefinition Document() => new() { TerritoryId = 1234, Rules = [new VisualMechanicRule { ActionId = 123 }] };
    private static VisualCast Cast() => new(new LogSystemCastStartEvent
    {
        SourceId = 1, TargetId = 2, ActionId = 123, CastTime = 4f,
        SourcePosition = new Vector3(10, 2, 20), SourceRotation = 0.3f,
        EffectPosition = new Vector3(-30, 4, 40), TargetPosition = new Vector3(-30, 4, 40),
    }, new VisualActor(1, 42, new Vector3(10, 2, 20), 0.3f), new VisualActor(2, 43, new Vector3(50, 6, 60), 0.8f), 1.2f);

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void CheckRotation(float actual, float expected, string message)
        => Check(MathF.Abs(MathF.IEEERemainder(actual - expected, MathF.Tau)) < 0.0001f, message);
    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException) { return; }
        throw new InvalidOperationException("Invalid or conflicting input was unexpectedly accepted.");
    }

    private sealed class Fight : IDisposable
    {
        internal VisualMechanicScript Script { get; }
        internal IReadOnlyList<DangerAreaSnapshot> Areas => Script.SafeZone.Manager.BuildSnapshot(DateTime.UtcNow).DangerAreas;
        internal Fight(VisualScriptDefinition doc) { Script = new VisualMechanicScript(doc); Script.StartForTest(); }
        public void Dispose() { Script.StopForTest(); Script.SafeZone.Dispose(); }
    }

    private sealed class ActorTable : IDisposable
    {
        private readonly PropertyInfo _objects = typeof(Svc).GetProperty(nameof(Svc.Objects))!;
        private readonly object? _original;
        internal Vector3 Position = new(10, 2, 20);
        internal float Rotation = 0.3f;
        internal bool Available = true;
        internal ActorTable()
        {
            _original = _objects.GetValue(null);
            IGameObject Actor(uint id) => ScriptServiceProxy.Create<IGameObject>((name, _) => name switch
            {
                "get_EntityId" => id, "get_BaseId" => id == 1 ? 42u : 43u,
                "get_Position" => id == 1 ? Position : new Vector3(50, 6, 60), "get_Rotation" => Rotation,
                _ => throw new NotSupportedException(name),
            });
            _objects.SetValue(null, ScriptServiceProxy.Create<IObjectTable>((name, args) => name switch
            {
                "get_Length" => 2,
                "get_Item" => Available && (int)args[0]! < 2 ? Actor((uint)(int)args[0]! + 1) : null,
                "SearchByEntityId" => Available && (uint)args[0]! is 1 or 2 ? Actor((uint)args[0]!) : null,
                _ => throw new NotSupportedException(name),
            }));
        }
        public void Dispose() => _objects.SetValue(null, _original);
    }
}
