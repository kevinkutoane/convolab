# Analytics cost and budget model

All analytics money is expressed in South African rand (ZAR).

| Classification | Meaning |
| --- | --- |
| `Actual` | A provider explicitly reported billed ZAR cost. |
| `Estimated` | Token counts were priced using the execution's governed pricing snapshot. |
| `Unavailable` | Usage or pricing evidence was incomplete; the cost is unknown, never zero. |

Estimated cost is:

```text
(input tokens / 1,000 × input price ZAR)
+ (output tokens / 1,000 × output price ZAR)
```

The configuration revision and pricing revision preserve the basis of each estimate. Simulator inputs are validated execution overrides, not a substitute for provider billing evidence.

Policy-prevented calls record zero input tokens, zero output tokens, zero provider cost, and `ProviderInvocationPrevented=true`. This zero means no provider invocation occurred; it is distinct from unavailable pricing.

## Budget semantics

Monthly consumption is calculated month-to-date, independent of the visible dashboard date range. Each provider execution contributes actual cost when available, otherwise estimated cost. Unknown-cost executions are counted separately.

The budget view reports the governed monthly limit, consumed and remaining ZAR, warning and hard-stop thresholds, and a clearly labelled estimated month-end projection. Engineers can view Development/Test cost but not Production cost. Cost and token fields receive the same restriction in dashboards, events, correlations, and exports.
