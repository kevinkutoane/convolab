import {
  Activity,
  AlertTriangle,
  Bell,
  Building2,
  CheckCheck,
  CheckCircle2,
  CloudOff,
  Command,
  LoaderCircle,
  LogOut,
  Menu,
  Moon,
  Search,
  Server,
  ShieldAlert,
  Sparkles,
  Sun,
} from "lucide-react";
import { useEffect, useState, type ReactNode } from "react";
import { Link, useLocation } from "react-router";
import { navigationItems } from "../data/platform";
import type { PlatformStatus } from "../types/platform";
import type { RuntimeEnvironment } from "../types/settings";
import type { WorkspaceChoice } from "../services/authApi";
import { useAuth } from "../contexts/useAuth";
import { useEnvironment, type EnvironmentContextValue } from "../contexts/EnvironmentContext";
import { useLiveNotifications, type LiveNotification } from "../hooks/useLiveNotifications";
import { StatusPill } from "./StatusPill";

type ApiState = "checking" | "online" | "offline";

function resolveApiState(isFetching: boolean, isOnline: boolean): ApiState {
  if (isFetching) return "checking";
  return isOnline ? "online" : "offline";
}

function getApiTitle(apiState: ApiState): string {
  if (apiState === "online") return "The Platform API is responding normally.";
  if (apiState === "checking") return "Checking Platform API connectivity.";
  return "The Platform API is unavailable; displayed data may be stale.";
}

function renderApiIcon(apiState: ApiState, apiOnline: boolean): ReactNode {
  if (apiState === "checking") {
    return <LoaderCircle className="api-connectivity-spinner" size={14} />;
  }
  if (apiOnline) {
    return <Server size={14} />;
  }
  return <CloudOff size={14} />;
}

function renderNotificationIcon(item: LiveNotification): ReactNode {
  if (item.severity === "error") return <AlertTriangle size={17} />;
  if (item.severity === "warning") return <ShieldAlert size={17} />;
  if (item.severity === "success") return <Sparkles size={17} />;
  if (item.category === "environment") return <Activity size={17} />;
  return <Bell size={17} />;
}

interface WorkspaceControlProps {
  readonly workspaces: readonly WorkspaceChoice[];
  readonly activeWorkspaceId?: string;
  readonly onSwitchWorkspace: (id: string) => void;
}

function WorkspaceControl({
  workspaces,
  activeWorkspaceId,
  onSwitchWorkspace,
}: Readonly<WorkspaceControlProps>): ReactNode {
  if (workspaces.length > 1) {
    return (
      <select
        className="workspace-switcher"
        aria-label="Switch workspace"
        value={activeWorkspaceId ?? ""}
        onChange={event => onSwitchWorkspace(event.target.value)}
      >
        {workspaces.map(item => (
          <option key={item.id} value={item.id}>{item.name}</option>
        ))}
      </select>
    );
  }

  const active = workspaces.find(w => w.id === activeWorkspaceId);
  if (active) {
    return (
      <span className="topbar-context-chip" title="Active workspace">
        <Building2 size={14} />
        <span>{active.name}</span>
      </span>
    );
  }

  return null;
}

interface EnvironmentControlProps {
  readonly activeEnvironments: readonly RuntimeEnvironment[];
  readonly environment: EnvironmentContextValue;
}

function EnvironmentControl({
  activeEnvironments,
  environment,
}: Readonly<EnvironmentControlProps>): ReactNode {
  if (activeEnvironments.length === 0) {
    return (
      <span className="environment-chip">
        <span className="environment-dot" /> No environment
      </span>
    );
  }

  const envTypeClass = environment.activeEnvironment?.environmentType.toLowerCase() ?? "development";

  return (
    <span className={`environment-chip environment-${envTypeClass}`}>
      <span className="environment-dot" />
      {activeEnvironments.length > 1 ? (
        <select
          className="environment-switcher"
          aria-label="Switch environment"
          value={environment.activeEnvironmentId ?? ""}
          onChange={event => void environment.setActiveEnvironmentId(event.target.value)}
          disabled={environment.isLoading || environment.isSwitching}
        >
          {activeEnvironments.map(item => (
            <option key={item.id} value={item.id}>
              {item.name}{item.isDefault ? " (default)" : ""}
            </option>
          ))}
        </select>
      ) : (
        <strong title="Active environment">{activeEnvironments[0].name}</strong>
      )}
    </span>
  );
}

interface TopbarProps {
  readonly theme: "dark" | "light";
  readonly onToggleTheme: () => void;
  readonly onOpenPalette: () => void;
  readonly onOpenMobile: () => void;
  readonly status?: PlatformStatus;
  readonly isFetchingStatus: boolean;
  readonly statusStale: boolean;
}

export function Topbar({
  theme,
  onToggleTheme,
  onOpenPalette,
  onOpenMobile,
  status,
  isFetchingStatus,
  statusStale,
}: Readonly<TopbarProps>) {
  const location = useLocation();
  const navigationPath = location.pathname === "/evaluations" ? "/evaluation" : location.pathname;
  const current = navigationItems.find(item =>
    item.path === "/"
      ? navigationPath === "/"
      : navigationPath === item.path || navigationPath.startsWith(`${item.path}/`),
  );

  const [notificationsOpen, setNotificationsOpen] = useState(false);
  const [userOpen, setUserOpen] = useState(false);
  const auth = useAuth();
  const environment = useEnvironment();

  const workspaces = auth.session?.workspaces ?? [];
  const activeEnvironments = environment.environments.filter(item => item.status === "Active");
  const apiOnline = !statusStale && (status?.apiHealth === "Healthy" || status?.apiHealth === "Responding");
  const apiState = resolveApiState(isFetchingStatus, apiOnline);

  const {
    notifications,
    hasUnread,
    unreadCount,
    markAllAsRead,
    markAsRead,
    clearAll,
  } = useLiveNotifications({
    status,
    apiOnline,
    environment,
    isAuthenticated: !!auth.session,
  });

  useEffect(() => {
    if (!notificationsOpen) return;
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setNotificationsOpen(false);
    };
    window.addEventListener("keydown", closeOnEscape);
    return () => window.removeEventListener("keydown", closeOnEscape);
  }, [notificationsOpen]);

  const unreadCountSuffix = unreadCount > 0 ? ` (${unreadCount} unread)` : "";
  const notificationsButtonLabel = `Notifications${unreadCountSuffix}`;
  const unreadHeadingCount = unreadCount > 0 ? ` (${unreadCount})` : "";
  const userInitials = auth.session?.displayName
    .split(" ")
    .map(value => value[0])
    .slice(0, 2)
    .join("")
    .toUpperCase() ?? "CL";

  return (
    <header className="topbar">
      <div className="topbar-title-area">
        <button
          className="icon-button mobile-menu-button"
          onClick={onOpenMobile}
          aria-label="Open navigation"
        >
          <Menu size={20} />
        </button>
        <div className="topbar-page-identity">
          <span className="topbar-eyebrow">Build · test · govern</span>
          <div className="topbar-heading-row">
            <h1>{current?.label ?? "ConvoLab Studio"}</h1>
            {current?.status && <StatusPill status={current.status} compact />}
          </div>
        </div>
      </div>

      <button className="global-search" onClick={onOpenPalette}>
        <Search size={17} aria-hidden="true" />
        <span>Search Studio or run a command</span>
        <kbd>
          <Command size={12} /> K
        </kbd>
      </button>

      <div className="topbar-actions">
        <span
          className={`api-connectivity api-${apiState}`}
          role="status"
          aria-live="polite"
          aria-label={`Platform API ${apiState}`}
          data-testid="api-connectivity"
          title={getApiTitle(apiState)}
        >
          {renderApiIcon(apiState, apiOnline)}
          <span>API {apiState}</span>
        </span>

        <WorkspaceControl
          workspaces={workspaces}
          activeWorkspaceId={auth.session?.activeWorkspaceId}
          onSwitchWorkspace={id => auth.switchWorkspace(id)}
        />

        <EnvironmentControl
          activeEnvironments={activeEnvironments}
          environment={environment}
        />

        <div className="notification-control">
          <button
            className="icon-button"
            aria-label={notificationsButtonLabel}
            aria-expanded={notificationsOpen}
            onClick={() => setNotificationsOpen(value => !value)}
          >
            <Bell size={18} />
            {hasUnread && <span className="notification-dot" />}
          </button>
          {notificationsOpen && (
            <section className="notification-popover panel" aria-label="Platform notifications">
              <div className="notification-heading">
                <div>
                  <span className="panel-eyebrow">Live platform updates</span>
                  <h3>Notifications{unreadHeadingCount}</h3>
                </div>
                <div className="notification-heading-actions">
                  {hasUnread && (
                    <button className="text-button" onClick={markAllAsRead} title="Mark all as read">
                      <CheckCheck size={14} /> Mark read
                    </button>
                  )}
                  {notifications.length > 0 && (
                    <button className="text-button" onClick={clearAll} title="Clear all notifications">
                      Clear
                    </button>
                  )}
                </div>
              </div>
              <div className="notification-list">
                {notifications.length === 0 ? (
                  <div className="notification-empty">
                    <CheckCircle2 size={24} />
                    <strong>All caught up</strong>
                    <small>No unread notifications. All systems operational.</small>
                  </div>
                ) : (
                  notifications.map(item => (
                    <Link
                      key={item.id}
                      className={`notification-item severity-${item.severity}${item.read ? "" : " unread"}`}
                      to={item.link}
                      onClick={() => {
                        markAsRead(item.id);
                        setNotificationsOpen(false);
                      }}
                    >
                      {renderNotificationIcon(item)}
                      <span>
                        <strong>{item.title}</strong>
                        <small>{item.description}</small>
                        <span className="notification-time">{item.timeLabel}</span>
                      </span>
                    </Link>
                  ))
                )}
              </div>
            </section>
          )}
        </div>

        <button
          className="icon-button"
          aria-label={`Switch to ${theme === "dark" ? "light" : "dark"} theme`}
          onClick={onToggleTheme}
        >
          {theme === "dark" ? <Sun size={18} /> : <Moon size={18} />}
        </button>

        <div className="user-control">
          <button
            className="avatar"
            aria-label="Open user menu"
            aria-expanded={userOpen}
            onClick={() => setUserOpen(value => !value)}
          >
            {userInitials}
          </button>
          {userOpen && (
            <section className="user-popover panel" aria-label="User menu">
              <div>
                <strong>{auth.session?.displayName}</strong>
                <small>{auth.session?.email}</small>
              </div>
              <Link to="/workspace" onClick={() => setUserOpen(false)}>
                <Building2 size={15} />Workspace settings
              </Link>
              <button onClick={() => auth.logout()}>
                <LogOut size={15} />Sign out
              </button>
            </section>
          )}
        </div>
      </div>
    </header>
  );
}
