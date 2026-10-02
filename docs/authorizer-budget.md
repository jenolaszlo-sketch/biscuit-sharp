# Authorization budget measurements

## Typed failure follow-up — development preview.2

The wrapper now exposes typed fact/iteration/time exhaustion, expression errors,
unexpected query cardinality and other failures. Hufu should consume
EvaluationFailureReason, never parse display text. Only the three limit-exceeded
values map to AuthorizationBudgetExceeded.

The reason reports which upstream limit ended evaluation, not a per-budget
usage counter, cancellation guarantee or hard memory cap. Existing measurements
below are historical and are not a Hufu policy workload qualification.

Measured 2026-10-02 on Windows x64, .NET 8, Release managed build and the pinned
Rust 1.89.0 release bridge. Command: dotnet run -c Release --project
tests/BiscuitSharp.BudgetProbe --framework net8.0, with BISCUITSHARP_NATIVE_PATH
pointing to native/target/release/biscuitsharp_native.dll.

| Scenario | Calls | Result | Median / maximum elapsed |
| --- | --- | --- | --- |
| Ordinary right/operation policy | 20 | Allow | 0.081 / 6.345 ms (maximum includes first-call warm-up) |
| Ordinary policy with 100 ambient facts | 20 | Allow | 0.181 / 0.687 ms |
| Ordinary requests, four workers | 40 | All Allow | 9.876 ms total wall time |
| Cartesian rule over 500 seeds, defaults | 1 | evaluation_failure | 279.317 ms |
| Cartesian rule over 100 seeds, 200 facts / 100 iterations / 50 ms | 4 | evaluation_failure | 7.099 / 7.215 ms |
| Small-budget growth, four workers | 4 | All evaluation_failure | 9.481 ms total wall time |

The growth rule derives pair($x, $y) from seed($x), seed($y). The default case
exceeds the 100,000-fact budget; no exhausted evaluation becomes an Allow.
Working set after default growth was 40,947,712 bytes versus 38,608,896 before;
these are endpoint process observations, not peak native allocations or a memory
cap. The five-second limit bounds evaluation, not parsing, cryptographic work,
response marshalling or an end-to-end request deadline.

These synthetic cases support retaining the documented preview defaults while
keeping UpstreamDefault and explicit WithLimits available. They do not substitute
for Hufu-specific policy/concurrency measurements. CI repeats the functional
allow/fail-closed checks and prints timing observations without brittle latency
thresholds. Native dev CI timings must not be compared directly with this release
measurement.


## Second-audit concurrency follow-up

Four concurrent 500-seed Cartesian workloads now run under Default on both
TFMs. Every call must return evaluation_failure. A 20 ms sampler records process
working set and observation count, and is cancelled/joined before disposal.

Local Windows x64, pinned release bridge:

| TFM | Calls | Wall time | Sampled peak working set | Periodic samples |
| --- | --- | --- | --- | --- |
| net8.0 | 4 | 422 ms | 252,477,440 bytes (240.8 MiB) | 12 |
| net10.0 | 4 | 374 ms | 274,534,400 bytes (261.8 MiB) | 11 |

The sampled maximum may miss short peaks and includes all process memory.
It is neither a native-allocation peak nor a hard cap. CI prints these
observations and validates fail-closed behavior on both frameworks without
latency/memory thresholds. Hufu must measure its own policy and concurrency
requirements and choose explicit budgets; the synthetic evidence does not
establish a universal request profile.
