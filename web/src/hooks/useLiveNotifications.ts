import { useState, useEffect, useCallback, useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { listIntelligenceExecutions } from "../services/intelligenceApi";
import type { PlatformStatus } from "../types/platform";
import type { EnvironmentContextValue } from "../contexts/EnvironmentContext";

export interface LiveNotification {
  id: string;
  title: string;
  description: string;
  timestamp: string;
  timeLabel: string;
  severity: "info" | "success" | "warning" | "error";
  category: "system" | "execution" | "policy" | "environment";
  link: string;
  read: boolean;
}

export interface InAppNotificationEvent {
  id?: string;
  title: string;
  description: string;
  severity?: "info" | "success" | "warning" | "error";
  category?: "system" | "execution" | "policy" | "environment";
  link?: string;
  timestamp?: string;
}

const READ_STORAGE_KEY = "convolab_read_notifications";
const DISMISSED_STORAGE_KEY = "convolab_dismissed_notifications";

function getStoredIds(key: string): Set<string> {
  try {
    const raw = localStorage.getItem(key);
    return raw ? new Set(JSON.parse(raw)) : new Set();
  } catch {
    return new Set();
  }
}

function saveStoredIds(key: string, ids: Set<string>): void {
  try {
    localStorage.setItem(key, JSON.stringify(Array.from(ids).slice(-100)));
  } catch {
    // Storage quota or privacy mode
  }
}

function formatRelativeTime(dateString: string): string {
  try {
    const diffSeconds = Math.floor((Date.now() - new Date(dateString).getTime()) / 1000);
    if (isNaN(diffSeconds) || diffSeconds < 30) return "Just now";
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

interface UseLiveNotificationsProps {
  readonly status?: PlatformStatus;
  readonly apiOnline: boolean;
  readonly environment: EnvironmentContextValue;
  readonly isAuthenticated: boolean;
}

export function useLiveNotifications({
  status,
  apiOnline,
  environment,
  isAuthenticated,
}: UseLiveNotificationsProps) {
  const [readIds, setReadIds] = useState<Set<string>>(() => getStoredIds(READ_STORAGE_KEY));
  const [dismissedIds, setDismissedIds] = useState<Set<string>>(() => getStoredIds(DISMISSED_STORAGE_KEY));
  const [inAppNotifications, setInAppNotifications] = useState<LiveNotification[]>([]);

  // Listen for custom in-app notifications
  useEffect(() => {
    const handler = (event: Event) => {
      const customEvent = event as CustomEvent<InAppNotificationEvent>;
      const detail = customEvent.detail;
      if (!detail || !detail.title) return;

      const timestamp = detail.timestamp ?? new Date().toISOString();
      const id = detail.id ?? `event-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`;

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

  // Poll for recent live AI executions when authenticated
  const { data: executions } = useQuery({
    queryKey: ["live-notifications-executions"],
    queryFn: () => listIntelligenceExecutions(5),
    staleTime: 15_000,
    refetchInterval: 30_000,
    retry: false,
    enabled: isAuthenticated,
  });

  // Assemble notifications from live system and operational state
  const notifications = useMemo(() => {
    const list: LiveNotification[] = [];

    // 1. In-app dynamic notifications
    for (const item of inAppNotifications) {
      if (!dismissedIds.has(item.id)) {
        list.push({
          ...item,
          read: readIds.has(item.id),
          timeLabel: formatRelativeTime(item.timestamp),
        });
      }
    }

    // 2. SafeMode alert
    if (status?.safeMode) {
      const id = "system-safe-mode-alert";
      if (!dismissedIds.has(id)) {
        list.push({
          id,
          title: "Platform Safe Mode active",
          description: "External AI execution and exports are blocked by platform policy.",
          timestamp: status.generatedAt ?? new Date().toISOString(),
          timeLabel: formatRelativeTime(status.generatedAt ?? new Date().toISOString()),
          severity: "warning",
          category: "system",
          link: "/operations",
          read: readIds.has(id),
        });
      }
    }

    // 3. API Connectivity issue
    if (!apiOnline) {
      const id = "system-api-offline-alert";
      if (!dismissedIds.has(id)) {
        list.push({
          id,
          title: "Platform API Offline",
          description: "The Platform API is unreachable. Check container or server status.",
          timestamp: new Date().toISOString(),
          timeLabel: "Active",
          severity: "error",
          category: "system",
          link: "/operations",
          read: readIds.has(id),
        });
      }
    }

    // 4. Recent executions (live runs)
    if (executions && executions.length > 0) {
      for (const exec of executions) {
        const id = `exec-${exec.runId}`;
        if (dismissedIds.has(id)) continue;

        const isFailed = exec.status === "Failed";
        list.push({
          id,
          title: isFailed
            ? `AI Run Failed: ${exec.model}`
            : `AI Run Completed: ${exec.model}`,
          description: isFailed
            ? (exec.failureReason ?? `Model execution for ${exec.simulationTitle} encountered an error.`)
            : `${exec.simulationTitle} · ${exec.totalTokens} tokens · ${exec.durationMs}ms`,
          timestamp: exec.createdAt,
          timeLabel: formatRelativeTime(exec.createdAt),
          severity: isFailed ? "error" : "success",
          category: "execution",
          link: "/intelligence",
          read: readIds.has(id),
        });
      }
    }

    // 5. Active environment notice
    if (environment.activeEnvironment) {
      const env = environment.activeEnvironment;
      const id = `env-status-${env.id}`;
      if (!dismissedIds.has(id)) {
        list.push({
          id,
          title: `Environment: ${env.name}`,
          description: `Active ${env.environmentType} workspace environment.`,
          timestamp: env.createdAt,
          timeLabel: "Current",
          severity: "info",
          category: "environment",
          link: "/operations",
          read: readIds.has(id),
        });
      }
    }

    return list;
  }, [inAppNotifications, status, apiOnline, executions, environment.activeEnvironment, readIds, dismissedIds]);

  const hasUnread = useMemo(() => {
    return notifications.some(item => !item.read);
  }, [notifications]);

  const unreadCount = useMemo(() => {
    return notifications.filter(item => !item.read).length;
  }, [notifications]);

  const markAllAsRead = useCallback(() => {
    const next = new Set(readIds);
    for (const item of notifications) {
      next.add(item.id);
    }
    setReadIds(next);
    saveStoredIds(READ_STORAGE_KEY, next);
  }, [notifications, readIds]);

  const markAsRead = useCallback((id: string) => {
    if (readIds.has(id)) return;
    const next = new Set(readIds);
    next.add(id);
    setReadIds(next);
    saveStoredIds(READ_STORAGE_KEY, next);
  }, [readIds]);

  const clearAll = useCallback(() => {
    const next = new Set(dismissedIds);
    for (const item of notifications) {
      next.add(item.id);
    }
    setDismissedIds(next);
    saveStoredIds(DISMISSED_STORAGE_KEY, next);
  }, [notifications, dismissedIds]);

  return {
    notifications,
    hasUnread,
    unreadCount,
    markAllAsRead,
    markAsRead,
    clearAll,
  };
}
