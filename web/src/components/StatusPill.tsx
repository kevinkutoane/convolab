import type { CapabilityStatus } from "../types/platform";
import { statusLabel } from "../lib/status";

interface StatusPillProps {
  status: CapabilityStatus;
  compact?: boolean;
}

export function StatusPill({ status, compact = false }: StatusPillProps) {
  return (
    <span className={`status-pill status-${status}${compact ? " status-compact" : ""}`}>
      <span className="status-dot" aria-hidden="true" />
      {statusLabel(status)}
    </span>
  );
}
