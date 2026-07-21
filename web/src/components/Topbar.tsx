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
} from "lucide-react";
import { useEffect, useState } from "react";
import { Link, useLocation } from "react-router-dom";
import { navigationItems } from "../data/platform";
import type { PlatformStatus } from "../types/platform";

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
  status,
}: TopbarProps) {
  const location = useLocation();
  const current = navigationItems.find(item => item.path === location.pathname);
  const [notificationsOpen, setNotificationsOpen] = useState(false);
  const [hasUnread, setHasUnread] = useState(true);
  const foundationCount = status?.capabilities.filter(item => item.status === "foundation").length ?? 0;

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
          <span className="topbar-eyebrow">Engineering workspace</span>
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
        <span className="environment-chip">
          <span className="environment-dot" /> Development
        </span>
        <div className="notification-control">
          <button className="icon-button" aria-label="Notifications" aria-expanded={notificationsOpen} onClick={() => setNotificationsOpen(value => !value)}>
            <Bell size={18} />
            {hasUnread && <span className="notification-dot" />}
          </button>
          {notificationsOpen && <section className="notification-popover panel" role="dialog" aria-label="Platform notifications">
            <div className="notification-heading"><div><span className="panel-eyebrow">Platform updates</span><h3>Notifications</h3></div><button className="text-button" onClick={() => setHasUnread(false)}><CheckCheck size={14} /> Mark read</button></div>
            <Link className="notification-item" to="/evaluation" onClick={() => { setNotificationsOpen(false); setHasUnread(false); }}><ClipboardCheck size={17} /><span><strong>Evaluation Studio is stable</strong><small>Scorecards, quality gates, and documentation are ready.</small></span></Link>
            <Link className="notification-item" to="/policies" onClick={() => { setNotificationsOpen(false); setHasUnread(false); }}><ShieldAlert size={17} /><span><strong>{foundationCount} foundations remain</strong><small>Policy Center is the recommended next capability.</small></span></Link>
          </section>}
        </div>
        <button
          className="icon-button"
          aria-label={`Switch to ${theme === "dark" ? "light" : "dark"} theme`}
          onClick={onToggleTheme}
        >
          {theme === "dark" ? <Sun size={18} /> : <Moon size={18} />}
        </button>
        <div className="avatar" title="Kevin Kutoane">
          KK
        </div>
      </div>
    </header>
  );
}
