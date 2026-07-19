import type { CapabilityStatus } from "../types/platform";

export function statusLabel(status: CapabilityStatus): string {
  switch (status) {
    case "stable":
      return "Stable";
    case "active":
      return "Active";
    case "foundation":
      return "Foundation";
    case "planned":
      return "Planned";
  }
}
