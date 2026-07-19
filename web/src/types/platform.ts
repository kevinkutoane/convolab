import type { LucideIcon } from "lucide-react";

export type CapabilityStatus = "stable" | "active" | "foundation" | "planned";

export interface PlatformCapability {
  id: string;
  name: string;
  description: string;
  status: CapabilityStatus;
  version: string;
  domainEvents: number;
}

export interface PlatformStatus {
  platformName: string;
  productName: string;
  version: string;
  environment: string;
  architectureHealth: "Healthy" | "Attention" | "Unknown";
  apiHealth: "Healthy" | "Offline" | "Unknown";
  capabilities: PlatformCapability[];
  generatedAt: string;
  source: "api" | "design-time snapshot";
}

export interface NavigationItem {
  label: string;
  path: string;
  icon: LucideIcon;
  description: string;
  status?: CapabilityStatus;
}

export interface StudioPageMetric {
  label: string;
  value: string;
  detail: string;
}

export interface StudioPageDefinition {
  title: string;
  eyebrow: string;
  description: string;
  icon: LucideIcon;
  status: CapabilityStatus;
  metrics: StudioPageMetric[];
  activities: string[];
  emptyTitle: string;
  emptyDescription: string;
  primaryAction: string;
}
