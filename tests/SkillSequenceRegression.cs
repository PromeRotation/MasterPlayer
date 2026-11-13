using MasterPlayer.Combat.Sequences;

internal static class SkillSequenceRegression
{
    private static readonly DateTime Start = new(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        run("Skill Sequence status gates and confirmed progression", () =>
        {
            var profile = new SkillSequenceProfile { JobId = 19, Entries = [new() { Enabled = false, SkillId = 1 }, new() { SkillId = 2, SkipIfHasStatusId = 10 }, new() { SkillId = 3, RequireStatusId = 20 }, new() { SkillId = 4 }, new() { SkillId = 5 }] };
            var runner = new SkillSequenceRunner();
            runner.Start(profile, Start);
            Check(runner.Tick(Start, new HashSet<uint> { 10 })?.SkillId == 4 && runner.StepIndex == 3, "Skip disabled and unmet status steps.");
            Check(runner.Tick(Start.AddSeconds(5), new HashSet<uint>())?.SkillId == 4, "An attempt alone must not advance the sequence.");
            runner.Confirm();
            Check(runner.Tick(Start.AddSeconds(6), new HashSet<uint>())?.SkillId == 5, "Advance only after confirmation.");
        });
        run("Skill Sequence completion command delay and once per loop", () =>
        {
            var runner = new SkillSequenceRunner();
            runner.Start(new() { JobId = 19, LoopExecution = true, CompletionCommand = "/echo done", CompletionCommandDelayMs = 600, Entries = [new() { SkillId = 1 }] }, Start);
            runner.Tick(Start, new HashSet<uint>());
            runner.Confirm();
            runner.Tick(Start, new HashSet<uint>());
            runner.Tick(Start.AddMilliseconds(599), new HashSet<uint>());
            Check(runner.TakeCompletionCommand() == null, "Respect completion delay.");
            runner.Tick(Start.AddMilliseconds(600), new HashSet<uint>());
            Check(runner.TakeCompletionCommand() == "/echo done" && runner.TakeCompletionCommand() == null, "Consume each completion command exactly once.");
            Check(runner.Tick(Start.AddMilliseconds(601), new HashSet<uint>())?.SkillId == 1, "Restart the loop after the completion command.");
            runner.Stop();
            Check(!runner.Running && runner.Tick(Start.AddSeconds(1), new HashSet<uint>()) == null && runner.TakeCompletionCommand() == null, "Stop clears actions and delayed commands.");
        });
        run("Skill Sequence edits do not mutate a running snapshot", () =>
        {
            var profile = new SkillSequenceProfile { JobId = 19, Entries = [new() { SkillId = 1 }] };
            var runner = new SkillSequenceRunner();
            runner.Start(profile, Start);
            profile.Entries.Clear();
            Check(runner.Tick(Start, new HashSet<uint>())?.SkillId == 1, "Edits apply to the next combat, not the active run.");
            runner.Confirm();
            runner.Tick(Start, new HashSet<uint>());
            runner.Tick(Start.AddSeconds(1), new HashSet<uint>());
            Check(!runner.Running, "Non-looping profiles finish.");
        });
        run("Skill Sequence XSZ exchange preserves conditions and rejects invalid input", () =>
        {
            var profile = new SkillSequenceProfile { JobId = 30, PresetName = "忍者", Entries = [new() { SkillId = 2271, TargetSelf = true, RequireStatusId = 10, SkipIfHasStatusId = 20, Note = "中文备注" }] };
            var decoded = SkillSequenceExchange.Import(SkillSequenceExchange.Export(profile));
            Check(decoded.PresetId != profile.PresetId && decoded.JobId == 30 && decoded.Entries[0].Note == "中文备注"
                && decoded.Entries[0].RequireStatusId == 10 && decoded.Entries[0].SkipIfHasStatusId == 20 && decoded.Entries[0].TargetSelf, "Round-trip all supported legacy fields with a new identity.");
            var legacy = SkillSequenceExchange.Import("{\"JobId\":19,\"StepIntervalMs\":150,\"Entries\":[{\"SkillId\":9}]}");
            Check(legacy.Entries.Single().SkillId == 9, "Accept old raw profile JSON.");
            foreach (var bad in new[] { "XSZSEQ1:bad", "{}", "{\"Version\":2,\"Profile\":{\"JobId\":19}}" })
            {
                try { SkillSequenceExchange.Import(bad); }
                catch (Exception) { continue; }
                throw new InvalidOperationException("Invalid exchange data must be rejected.");
            }
        });
        run("Skill Sequence ninjutsu chains retain mudra order", () =>
        {
            Check(NinjaSequence.Expand(2271).SequenceEqual(new uint[] { 2259, 2261, 2263, 2271 }), "Suiton mudra order.");
            Check(NinjaSequence.Expand(16492).SequenceEqual(new uint[] { 2259, 2263, 16492 }), "Hyosho mudra order.");
            Check(NinjaSequence.Expand(123).SequenceEqual(new uint[] { 123 }) && NinjaSequence.IsMudra(18807), "Regular skills and adjusted mudra IDs.");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
