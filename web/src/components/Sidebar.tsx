import { ChevronLeft, ChevronRight, Hexagon, X } from "lucide-react";
import { NavLink } from "react-router-dom";
import { navigationItems } from "../data/platform";
import { StatusPill } from "./StatusPill";

interface SidebarProps {
  collapsed: boolean;
  mobileOpen: boolean;
  onToggle: () => void;
  onCloseMobile: () => void;
}

export function Sidebar({
  collapsed,
  mobileOpen,
  onToggle,
  onCloseMobile,
}: SidebarProps) {
  return (
    <>
      {mobileOpen && (
        <button
          className="mobile-backdrop"
          aria-label="Close navigation"
          onClick={onCloseMobile}
        />
      )}
      <aside
        className={`sidebar${collapsed ? " sidebar-collapsed" : ""}${
          mobileOpen ? " sidebar-mobile-open" : ""
        }`}
      >
        <div className="sidebar-brand">
          <div className="brand-mark" aria-hidden="true">
            <Hexagon size={22} strokeWidth={1.8} />
            <span />
          </div>
          {!collapsed && (
            <div className="brand-copy">
              <strong>ConvoLab</strong>
              <span>Studio</span>
            </div>
          )}
          <button
            className="icon-button sidebar-mobile-close"
            onClick={onCloseMobile}
            aria-label="Close navigation"
          >
            <X size={18} />
          </button>
        </div>

        <nav className="sidebar-nav" aria-label="Studio navigation">
          <span className="sidebar-section-label">{collapsed ? "" : "Workspace"}</span>
          {navigationItems.map(item => {
            const Icon = item.icon;
            return (
              <NavLink
                key={item.path}
                to={item.path}
                end={item.path === "/"}
                onClick={onCloseMobile}
                className={({ isActive }) =>
                  `sidebar-link${isActive ? " sidebar-link-active" : ""}`
                }
                title={collapsed ? item.label : undefined}
              >
                <Icon size={19} strokeWidth={1.7} aria-hidden="true" />
                {!collapsed && (
                  <>
                    <span className="sidebar-link-copy">
                      <strong>{item.label}</strong>
                      <small>{item.description}</small>
                    </span>
                    {item.status && <StatusPill status={item.status} compact />}
                  </>
                )}
              </NavLink>
            );
          })}
        </nav>

        <div className="sidebar-footer">
          {!collapsed && (
            <div className="workspace-card">
              <span className="workspace-kicker">Platform Core</span>
              <strong>v1.0.0-alpha.11</strong>
              <span>Policy, Trace, Replay, and Evaluation v1 integrated.</span>
            </div>
          )}
          <button
            className="sidebar-toggle"
            onClick={onToggle}
            aria-label={collapsed ? "Expand navigation" : "Collapse navigation"}
          >
            {collapsed ? <ChevronRight size={17} /> : <ChevronLeft size={17} />}
            {!collapsed && <span>Collapse</span>}
          </button>
        </div>
      </aside>
    </>
  );
}
