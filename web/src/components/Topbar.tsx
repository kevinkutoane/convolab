import {
  Bell,
  Command,
  Menu,
  Moon,
  Search,
  Sun,
} from "lucide-react";
import { useLocation } from "react-router-dom";
import { navigationItems } from "../data/platform";

interface TopbarProps {
  theme: "dark" | "light";
  onToggleTheme: () => void;
  onOpenPalette: () => void;
  onOpenMobile: () => void;
}

export function Topbar({
  theme,
  onToggleTheme,
  onOpenPalette,
  onOpenMobile,
}: TopbarProps) {
  const location = useLocation();
  const current = navigationItems.find(item => item.path === location.pathname);

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
        <button className="icon-button" aria-label="Notifications">
          <Bell size={18} />
          <span className="notification-dot" />
        </button>
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
