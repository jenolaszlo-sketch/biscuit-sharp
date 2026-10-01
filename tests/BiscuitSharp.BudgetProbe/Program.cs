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
using (var sampler = new WorkingSetPeakSampler(TimeSpan.FromMilliseconds(20)))
{
    sampler.Start();
    watch.Restart();
    try
    {
        Parallel.For(0, 4, _ => RequireEvaluationFailure(Growth(500).Authorize()));
    }
    finally
    {
        sampler.Stop();
    }
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        scenario = "growth-concurrent-default-budget",
        workers = 4,
        calls = 4,
        elapsed_ms = watch.Elapsed.TotalMilliseconds,
        working_set_sample_interval_ms = sampler.Interval.TotalMilliseconds,
        periodic_working_set_sample_count = sampler.PeriodicSampleCount,
        sampled_peak_working_set_bytes = sampler.PeakWorkingSetBytes,
        memory_measurement = "periodic working-set sample; observed peak, not a hard memory cap",
        sampled_peak_is_hard_cap = false
    }));
}
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

sealed class WorkingSetPeakSampler : IDisposable
{
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly CancellationTokenSource _stop = new();
    private Task? _samplingTask;
    private long _peakWorkingSetBytes;
    private int _periodicSampleCount;
    private int _stopped;

    public WorkingSetPeakSampler(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        Interval = interval;
    }

    public TimeSpan Interval { get; }
    public long PeakWorkingSetBytes => Interlocked.Read(ref _peakWorkingSetBytes);
    public int PeriodicSampleCount => Volatile.Read(ref _periodicSampleCount);

    public void Start()
    {
        if (_samplingTask is not null) throw new InvalidOperationException("Sampler already started.");
        Capture();
        _samplingTask = Task.Run(SampleLoopAsync);
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0) return;
        _stop.Cancel();
        try
        {
            _samplingTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        Capture();
    }

    public void Dispose()
    {
        try { Stop(); }
        finally
        {
            _process.Dispose();
            _stop.Dispose();
        }
    }

    private async Task SampleLoopAsync()
    {
        try
        {
            while (true)
            {
                await Task.Delay(Interval, _stop.Token).ConfigureAwait(false);
                Capture();
                Interlocked.Increment(ref _periodicSampleCount);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }

    private void Capture()
    {
        _process.Refresh();
        long current = _process.WorkingSet64;
        long seen;
        do
        {
            seen = Interlocked.Read(ref _peakWorkingSetBytes);
            if (current <= seen) return;
        }
        while (Interlocked.CompareExchange(ref _peakWorkingSetBytes, current, seen) != seen);
    }
}
