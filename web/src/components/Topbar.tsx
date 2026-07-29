import {
  Bell,
  CheckCheck,
  ClipboardCheck,
  Command,
  Menu,
  Moon,
  Search,
  ShieldAlert,
  Sun,
  LogOut,
  Building2,
} from "lucide-react";
import { useEffect, useState } from "react";
import { Link, useLocation } from "react-router";
import { navigationItems } from "../data/platform";
import type { PlatformStatus } from "../types/platform";
import { useAuth } from "../contexts/useAuth";
import { useEnvironment } from "../contexts/EnvironmentContext";

interface TopbarProps {
  theme: "dark" | "light";
  onToggleTheme: () => void;
  onOpenPalette: () => void;
  onOpenMobile: () => void;
  status?: PlatformStatus;
}

export function Topbar({
  theme,
  onToggleTheme,
  onOpenPalette,
  onOpenMobile,
}: TopbarProps) {
  const location = useLocation();
  const current = navigationItems.find(item =>
    item.path === "/"
      ? location.pathname === "/"
      : location.pathname === item.path || location.pathname.startsWith(`${item.path}/`),
  );
  const [notificationsOpen, setNotificationsOpen] = useState(false);
  const [hasUnread, setHasUnread] = useState(true);
  const [userOpen, setUserOpen] = useState(false);
  const auth = useAuth();
  const environment = useEnvironment();
  const activeWorkspace = auth.session?.workspaces.find(item => item.id === auth.session?.activeWorkspaceId);
  const workspaces = auth.session?.workspaces ?? [];
  const activeEnvironments = environment.environments.filter(item => item.status === "Active");

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
        <div>
          <span className="topbar-eyebrow">Build · test · govern</span>
          <h1>{current?.label ?? "ConvoLab Studio"}</h1>
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
          <button className="icon-button" aria-label="Notifications" aria-expanded={notificationsOpen} onClick={() => setNotificationsOpen(value => !value)}>
            <Bell size={18} />
            {hasUnread && <span className="notification-dot" />}
          </button>
          {notificationsOpen && <section className="notification-popover panel" role="dialog" aria-label="Platform notifications">
            <div className="notification-heading"><div><span className="panel-eyebrow">Platform updates</span><h3>Notifications</h3></div><button className="text-button" onClick={() => setHasUnread(false)}><CheckCheck size={14} /> Mark read</button></div>
            <Link className="notification-item" to="/evaluation" onClick={() => { setNotificationsOpen(false); setHasUnread(false); }}><ClipboardCheck size={17} /><span><strong>Evaluation Studio is stable</strong><small>Versioned scorecards, reviews, batches, and comparisons are ready.</small></span></Link>
            <Link className="notification-item" to="/policies" onClick={() => { setNotificationsOpen(false); setHasUnread(false); }}><ShieldAlert size={17} /><span><strong>Governed execution is active</strong><small>Policy, Trace, and Replay workspaces are available.</small></span></Link>
          </section>}
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
