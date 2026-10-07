import { api } from "./apiClient";

export interface ApiNotification {
  id: string;
  workspaceId?: string | null;
  title: string;
  message: string;
  severity: "info" | "success" | "warning" | "error";
  category: "system" | "budget" | "policy" | "execution" | "simulation" | "evaluation" | "knowledge" | "environment";
  actionUrl: string;
  createdAt: string;
  isRead: boolean;
  isDismissed: boolean;
  metadata?: Record<string, string>;
}

export interface UnreadCountResponse {
  count: number;
}

export async function listNotifications(unreadOnly = false, limit = 50): Promise<ApiNotification[]> {
  const response = await api.get<ApiNotification[]>("/api/notifications", {
    params: { unreadOnly, limit },
  });
  return response.data;
}

export async function getUnreadNotificationCount(): Promise<number> {
  const response = await api.get<UnreadCountResponse>("/api/notifications/unread-count");
  return response.data.count;
}

export async function markNotificationAsRead(id: string): Promise<void> {
  await api.post(`/api/notifications/${encodeURIComponent(id)}/read`);
}

export async function markAllNotificationsAsRead(): Promise<void> {
  await api.post("/api/notifications/read-all");
}

export async function dismissNotification(id: string): Promise<void> {
  await api.delete(`/api/notifications/${encodeURIComponent(id)}`);
}

export async function clearAllNotifications(): Promise<void> {
  await api.delete("/api/notifications");
}
