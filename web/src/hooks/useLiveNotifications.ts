import { useState, useEffect, useCallback, useMemo } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  listNotifications,
  markNotificationAsRead,
  markAllNotificationsAsRead,
  clearAllNotifications,
  type ApiNotification,
} from "../services/notificationsApi";
import type { PlatformStatus } from "../types/platform";
import type { EnvironmentContextValue } from "../contexts/EnvironmentContext";

export type NotificationSeverity = "info" | "success" | "warning" | "error";
export type NotificationCategory = "system" | "budget" | "policy" | "execution" | "simulation" | "evaluation" | "knowledge" | "environment";

export interface LiveNotification {
  id: string;
  title: string;
  description: string;
  timestamp: string;
  timeLabel: string;
  severity: NotificationSeverity;
  category: NotificationCategory;
  link: string;
  read: boolean;
}

export interface InAppNotificationEvent {
  id?: string;
  title: string;
  description: string;
  severity?: NotificationSeverity;
  category?: NotificationCategory;
  link?: string;
  timestamp?: string;
}

/**
 * Strict actionable filter:
 * 1. Failures & Anomalies: error & warning severities (budget exhaustion, policy blocks, model failures, circuit openings).
 * 2. System Status: category === 'system' or 'environment' (safe mode, API offline, environment changes).
 * 3. Task Completions: milestone completions across simulation, evaluation, knowledge indexing, and workflow completion.
 *
 * Filters out: routine intermediate successes, token-level calls, and telemetry noise.
 */
export function isActionableNotification(notification: {
  severity: NotificationSeverity;
  category: NotificationCategory;
  title: string;
}): boolean {
  // 1. Failures and Warnings are always actionable
  if (notification.severity === "error" || notification.severity === "warning") {
    return true;
  }

  // 2. System and Environment operational health alerts
  if (notification.category === "system" || notification.category === "environment") {
    return true;
  }

  // 3. Asynchronous milestone task completions
  if (
    notification.category === "simulation" ||
    notification.category === "evaluation" ||
    notification.category === "knowledge"
  ) {
    return true;
  }

  // 4. Terminal workflow/job completions only
  if (notification.severity === "success") {
    return /completed|finished|succeeded|done/i.test(notification.title);
  }

  // Filter out routine successes or unclassified info notices
  return false;
}

function formatRelativeTime(dateString: string): string {
  try {
    const diffSeconds = Math.floor((Date.now() - new Date(dateString).getTime()) / 1000);
    if (Number.isNaN(diffSeconds) || diffSeconds < 30) return "Just now";
    if (diffSeconds < 60) return `${diffSeconds}s ago`;
    const diffMinutes = Math.floor(diffSeconds / 60);
    if (diffMinutes < 60) return `${diffMinutes}m ago`;
    const diffHours = Math.floor(diffMinutes / 60);
    if (diffHours < 24) return `${diffHours}h ago`;
    const diffDays = Math.floor(diffHours / 24);
    return `${diffDays}d ago`;
  } catch {
    return "Recent";
  }
}

/**
 * Dispatch an in-app live notification from anywhere in ConvoLab Studio.
 */
export function notify(event: InAppNotificationEvent): void {
  window.dispatchEvent(new CustomEvent("convolab:notify", { detail: event }));
}

function createNotificationId(detailId?: string): string {
  if (detailId) return detailId;
  const uniqueSuffix = typeof crypto !== "undefined" && typeof crypto.randomUUID === "function"
    ? crypto.randomUUID()
    : `${Date.now()}`;
  return `event-${Date.now()}-${uniqueSuffix}`;
}

function buildInAppNotifications(
  inAppList: readonly LiveNotification[],
  dismissedIds: ReadonlySet<string>,
  readIds: ReadonlySet<string>,
  filterActionableOnly: boolean,
): LiveNotification[] {
  const result: LiveNotification[] = [];
  for (const item of inAppList) {
    if (dismissedIds.has(item.id)) continue;
    if (filterActionableOnly && !isActionableNotification(item)) continue;

    result.push({
      ...item,
      read: item.read || readIds.has(item.id),
      timeLabel: formatRelativeTime(item.timestamp),
    });
  }
  return result;
}

function buildSystemHealthNotifications(
  status: PlatformStatus | undefined,
  apiOnline: boolean,
  dismissedIds: ReadonlySet<string>,
  readIds: ReadonlySet<string>,
): LiveNotification[] {
  const result: LiveNotification[] = [];

  if (status?.safeMode) {
    const id = "system-safe-mode-alert";
    if (!dismissedIds.has(id)) {
      result.push({
        id,
        title: "Platform Safe Mode Active",
        description: "External AI execution and exports are restricted by platform policy.",
        timestamp: status.generatedAt ?? new Date().toISOString(),
        timeLabel: formatRelativeTime(status.generatedAt ?? new Date().toISOString()),
        severity: "warning",
        category: "system",
        link: "/operations",
        read: readIds.has(id),
      });
    }
  }

  if (!apiOnline) {
    const id = "system-api-offline-alert";
    if (!dismissedIds.has(id)) {
      result.push({
        id,
        title: "Platform API Offline",
        description: "The Platform API is unreachable. Checking connection...",
        timestamp: new Date().toISOString(),
        timeLabel: "Active",
        severity: "error",
        category: "system",
        link: "/operations",
        read: readIds.has(id),
      });
    }
  }

  return result;
}

function buildBackendNotifications(
  backendList: readonly ApiNotification[],
  dismissedIds: ReadonlySet<string>,
  readIds: ReadonlySet<string>,
  filterActionableOnly: boolean,
): LiveNotification[] {
  const result: LiveNotification[] = [];
  for (const item of backendList) {
    if (item.isDismissed || dismissedIds.has(item.id)) continue;
    if (filterActionableOnly && !isActionableNotification(item)) continue;

    result.push({
      id: item.id,
      title: item.title,
      description: item.message,
      timestamp: item.createdAt,
      timeLabel: formatRelativeTime(item.createdAt),
      severity: item.severity,
      category: item.category,
      link: item.actionUrl,
      read: item.isRead || readIds.has(item.id),
    });
  }
  return result;
}

function buildEnvironmentNotification(
  environment: EnvironmentContextValue,
  dismissedIds: ReadonlySet<string>,
  readIds: ReadonlySet<string>,
): LiveNotification | null {
  if (!environment.activeEnvironment) return null;
  const env = environment.activeEnvironment;
  const id = `env-status-${env.id}`;
  if (dismissedIds.has(id)) return null;

  return {
    id,
    title: `Environment: ${env.name}`,
    description: `Active ${env.environmentType} workspace environment.`,
    timestamp: env.createdAt,
    timeLabel: "Current",
    severity: "info",
    category: "environment",
    link: "/operations",
    read: readIds.has(id),
  };
}

interface UseLiveNotificationsProps {
  readonly status?: PlatformStatus;
  readonly apiOnline: boolean;
  readonly environment: EnvironmentContextValue;
  readonly isAuthenticated: boolean;
  readonly filterActionableOnly?: boolean;
}

export function useLiveNotifications({
  status,
  apiOnline,
  environment,
  isAuthenticated,
  filterActionableOnly = true,
}: Readonly<UseLiveNotificationsProps>) {
  const queryClient = useQueryClient();
  const [localReadIds, setLocalReadIds] = useState<Set<string>>(new Set());
  const [localDismissedIds, setLocalDismissedIds] = useState<Set<string>>(new Set());
  const [inAppNotifications, setInAppNotifications] = useState<LiveNotification[]>([]);

  useEffect(() => {
    const handler = (event: Event) => {
      const customEvent = event as CustomEvent<InAppNotificationEvent>;
      const detail = customEvent.detail;
      if (!detail?.title) return;

      const timestamp = detail.timestamp ?? new Date().toISOString();
      const id = createNotificationId(detail.id);

      const newNotification: LiveNotification = {
        id,
        title: detail.title,
        description: detail.description,
        timestamp,
        timeLabel: "Just now",
        severity: detail.severity ?? "info",
        category: detail.category ?? "system",
        link: detail.link ?? "/operations",
        read: false,
      };

      setInAppNotifications(prev => [newNotification, ...prev.slice(0, 19)]);
    };

    window.addEventListener("convolab:notify", handler);
    return () => window.removeEventListener("convolab:notify", handler);
  }, []);

  const { data: backendNotifications = [] } = useQuery<ApiNotification[]>({
    queryKey: ["workspace-notifications"],
    queryFn: () => listNotifications(false, 50),
    staleTime: 10_000,
    refetchInterval: 20_000,
    retry: false,
    enabled: isAuthenticated && apiOnline,
  });

  const notifications = useMemo(() => {
    const inApp = buildInAppNotifications(inAppNotifications, localDismissedIds, localReadIds, filterActionableOnly);
    const systemHealth = buildSystemHealthNotifications(status, apiOnline, localDismissedIds, localReadIds);
    const backend = buildBackendNotifications(backendNotifications, localDismissedIds, localReadIds, filterActionableOnly);

    const list = [...inApp, ...systemHealth, ...backend];

    if (!list.some(n => n.category === "environment")) {
      const envNotice = buildEnvironmentNotification(environment, localDismissedIds, localReadIds);
      if (envNotice) {
        list.push(envNotice);
      }
    }

    return list;
  }, [inAppNotifications, status, apiOnline, backendNotifications, environment, localReadIds, localDismissedIds, filterActionableOnly]);

  const hasUnread = useMemo(() => {
    return notifications.some(item => !item.read);
  }, [notifications]);

  const unreadCount = useMemo(() => {
    return notifications.filter(item => !item.read).length;
  }, [notifications]);

  const markAllAsRead = useCallback(() => {
    const next = new Set(localReadIds);
    for (const item of notifications) {
      next.add(item.id);
    }
    setLocalReadIds(next);

    if (isAuthenticated && apiOnline) {
      markAllNotificationsAsRead().catch(() => {});
      void queryClient.invalidateQueries({ queryKey: ["workspace-notifications"] });
    }
  }, [notifications, localReadIds, isAuthenticated, apiOnline, queryClient]);

  const markAsRead = useCallback((id: string) => {
    if (localReadIds.has(id)) return;
    const next = new Set(localReadIds);
    next.add(id);
    setLocalReadIds(next);

    if (isAuthenticated && apiOnline) {
      markNotificationAsRead(id).catch(() => {});
      void queryClient.invalidateQueries({ queryKey: ["workspace-notifications"] });
    }
  }, [localReadIds, isAuthenticated, apiOnline, queryClient]);

  const clearAll = useCallback(() => {
    const next = new Set(localDismissedIds);
    for (const item of notifications) {
      next.add(item.id);
    }
    setLocalDismissedIds(next);

    if (isAuthenticated && apiOnline) {
      clearAllNotifications().catch(() => {});
      void queryClient.invalidateQueries({ queryKey: ["workspace-notifications"] });
    }
  }, [notifications, localDismissedIds, isAuthenticated, apiOnline, queryClient]);

  return {
    notifications,
    hasUnread,
    unreadCount,
    markAllAsRead,
    markAsRead,
    clearAll,
  };
}
