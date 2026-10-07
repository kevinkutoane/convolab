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
import { useEffect, useState } from "react";
import { Link, useLocation } from "react-router";
import { navigationItems } from "../data/platform";
import type { PlatformStatus } from "../types/platform";
import { useAuth } from "../contexts/useAuth";
import { useEnvironment } from "../contexts/EnvironmentContext";
import { useLiveNotifications } from "../hooks/useLiveNotifications";
import { StatusPill } from "./StatusPill";

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
  const navigationPath = location.pathname === "/evaluations"
    ? "/evaluation"
    : location.pathname;
  const current = navigationItems.find(item =>
    item.path === "/"
      ? navigationPath === "/"
      : navigationPath === item.path || navigationPath.startsWith(`${item.path}/`),
  );
  const [notificationsOpen, setNotificationsOpen] = useState(false);
  const [userOpen, setUserOpen] = useState(false);
  const auth = useAuth();
  const environment = useEnvironment();
  const activeWorkspace = auth.session?.workspaces.find(item => item.id === auth.session?.activeWorkspaceId);
  const workspaces = auth.session?.workspaces ?? [];
  const activeEnvironments = environment.environments.filter(item => item.status === "Active");
  const apiOnline = !statusStale
    && (status?.apiHealth === "Healthy" || status?.apiHealth === "Responding");
  const apiState = isFetchingStatus ? "checking" : apiOnline ? "online" : "offline";

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
          title={apiState === "online"
            ? "The Platform API is responding normally."
            : apiState === "checking"
              ? "Checking Platform API connectivity."
              : "The Platform API is unavailable; displayed data may be stale."}
        >
          {apiState === "checking"
            ? <LoaderCircle className="api-connectivity-spinner" size={14} />
            : apiOnline
              ? <Server size={14} />
              : <CloudOff size={14} />}
          <span>API {apiState}</span>
        </span>
        {workspaces.length > 1 ? (
          <select className="workspace-switcher" aria-label="Switch workspace" value={activeWorkspace?.id ?? ""} onChange={event => auth.switchWorkspace(event.target.value)}>{workspaces.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select>
        ) : activeWorkspace ? (
          <span className="topbar-context-chip" title="Active workspace"><Building2 size={14} /><span>{activeWorkspace.name}</span></span>
        ) : null}
        {activeEnvironments.length > 0 ? (
          <span className={`environment-chip environment-${environment.activeEnvironment?.environmentType.toLowerCase() ?? "development"}`}>
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
                  <option key={item.id} value={item.id}>{item.name}{item.isDefault ? " (default)" : ""}</option>
                ))}
              </select>
            ) : <strong title="Active environment">{activeEnvironments[0].name}</strong>}
          </span>
        ) : (
          <span className="environment-chip">
            <span className="environment-dot" /> No environment
          </span>
        )}
        <div className="notification-control">
          <button
            className="icon-button"
            aria-label={`Notifications${unreadCount > 0 ? ` (${unreadCount} unread)` : ""}`}
            aria-expanded={notificationsOpen}
            onClick={() => setNotificationsOpen(value => !value)}
          >
            <Bell size={18} />
            {hasUnread && <span className="notification-dot" />}
          </button>
          {notificationsOpen && (
            <section className="notification-popover panel" role="dialog" aria-label="Platform notifications">
              <div className="notification-heading">
                <div>
                  <span className="panel-eyebrow">Live platform updates</span>
                  <h3>Notifications {unreadCount > 0 ? `(${unreadCount})` : ""}</h3>
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
                      {item.severity === "error" ? (
                        <AlertTriangle size={17} />
                      ) : item.severity === "warning" ? (
                        <ShieldAlert size={17} />
                      ) : item.severity === "success" ? (
                        <Sparkles size={17} />
                      ) : item.category === "environment" ? (
                        <Activity size={17} />
                      ) : (
                        <Bell size={17} />
                      )}
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
        <div className="user-control"><button className="avatar" aria-label="Open user menu" aria-expanded={userOpen} onClick={() => setUserOpen(value => !value)}>{auth.session?.displayName.split(" ").map(value => value[0]).slice(0, 2).join("").toUpperCase() ?? "CL"}</button>{userOpen && <section className="user-popover panel" role="dialog" aria-label="User menu"><div><strong>{auth.session?.displayName}</strong><small>{auth.session?.email}</small></div><Link to="/workspace" onClick={() => setUserOpen(false)}><Building2 size={15}/>Workspace settings</Link><button onClick={() => auth.logout()}><LogOut size={15}/>Sign out</button></section>}</div>
      </div>
    </header>
  );
}
