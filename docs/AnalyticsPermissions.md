# Analytics permissions

Permissions are fixed server-side contracts.

| Role | Analytics access |
| --- | --- |
| Administrator | All workspace/environment categories, actor detail, Production cost, events, and exports. |
| Engineer | Usage, quality, performance and events; Development/Test cost only; no actor identity or Production cost/token fields. |
| Reviewer | Quality, governance and adoption/review evidence; no cost or actor identity. |
| Operator | Overview, usage, performance, governance, budget and operational cost; no actor identity. |
| Viewer | Aggregated overview only; no cost, actor detail, events, correlations, or exports. |

Named permissions are:

```text
CanViewWorkspaceAnalytics
CanViewEnvironmentAnalytics
CanViewCostAnalytics
CanViewQualityAnalytics
CanViewGovernanceAnalytics
CanViewAdoptionAnalytics
CanViewActorAnalytics
CanExportAnalytics
CanViewPlatformAnalytics (reserved)
```

Route permission is necessary but not sufficient. `AnalyticsFieldVisibility` also redacts actor, cost, token, provider, and source fields according to role, environment type, and requested resource. Production cost restrictions apply to the cost/budget routes, event list/detail, correlation timeline, and CSV export.
