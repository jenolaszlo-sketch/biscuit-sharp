using System.Diagnostics;
using System.Text.Json;
using BiscuitSharp;

using var root = BiscuitPrivateKey.Generate(BiscuitKeyAlgorithm.Ed25519);
BiscuitToken token = BiscuitTokenBuilder.Create().AddFact("""right("workspace.main", "read")""").Build(root);
Measure("ordinary", 20, () => Ordinary().Authorize(), true);
Measure("ordinary-100-facts", 20, () =>
{
    var authorizer = Ordinary();
    for (int i = 0; i < 100; i++) authorizer.AddFact($"ambient({i})");
    return authorizer.Authorize();
}, true);
var watch = Stopwatch.StartNew();
Parallel.For(0, 4, _ =>
{
    for (int i = 0; i < 10; i++)
        if (!Ordinary().Authorize().IsAuthorized) throw new InvalidOperationException("Concurrent ordinary request denied");
});
Console.WriteLine(JsonSerializer.Serialize(new { scenario = "ordinary-concurrent", workers = 4, calls = 40, elapsed_ms = watch.Elapsed.TotalMilliseconds }));
Measure("cartesian-growth-default", 1, () => Growth(500).Authorize(), false);
Measure("cartesian-growth-explicit-small-budget", 4, () =>
    Growth(100).WithLimits(new(200, 100, TimeSpan.FromMilliseconds(50))).Authorize(), false);
watch.Restart();
Parallel.For(0, 4, _ =>
{
    var result = Growth(100).WithLimits(new(200, 100, TimeSpan.FromMilliseconds(50))).Authorize();
    RequireEvaluationFailure(result);
});
Console.WriteLine(JsonSerializer.Serialize(new { scenario = "growth-concurrent-small-budget", workers = 4, calls = 4, elapsed_ms = watch.Elapsed.TotalMilliseconds }));

BiscuitAuthorizer Ordinary() => BiscuitAuthorizer.For(token).AddFact("""operation("read")""")
    .AddPolicy("""allow if right("workspace.main", "read");""");
BiscuitAuthorizer Growth(int count)
{
    var authorizer = BiscuitAuthorizer.For(token).AddRule("pair($x, $y) <- seed($x), seed($y);")
        .AddPolicy("allow if pair(0, 0);");
    for (int i = 0; i < count; i++) authorizer.AddFact($"seed({i})");
    return authorizer;
}
void Measure(string scenario, int count, Func<BiscuitAuthorizationResult> run, bool expectAllow)
{
    var durations = new List<double>();
    long before = Process.GetCurrentProcess().WorkingSet64;
    for (int i = 0; i < count; i++)
    {
        var timer = Stopwatch.StartNew();
        var result = run();
        durations.Add(timer.Elapsed.TotalMilliseconds);
        if (expectAllow && !result.IsAuthorized) throw new InvalidOperationException($"{scenario} denied");
        if (!expectAllow) RequireEvaluationFailure(result);
    }
    durations.Sort();
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        scenario, calls = count, decision = expectAllow ? "allow" : "evaluation_failure",
        median_ms = durations[durations.Count / 2], max_ms = durations[^1],
        working_set_before_bytes = before, working_set_after_bytes = Process.GetCurrentProcess().WorkingSet64
    }));
}
static void RequireEvaluationFailure(BiscuitAuthorizationResult result)
{
    if (result.IsAuthorized || !result.Errors.Any(e => e.Code == "evaluation_failure"))
        throw new InvalidOperationException("Hostile growth must fail closed with evaluation_failure");
}
