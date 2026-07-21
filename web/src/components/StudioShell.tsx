import { useEffect, useState } from "react";
import { Outlet } from "react-router-dom";
import type { PlatformStatus } from "../types/platform";
import { CommandPalette } from "./CommandPalette";
import { Sidebar } from "./Sidebar";
import { StatusBar } from "./StatusBar";
import { Topbar } from "./Topbar";

interface StudioShellProps {
  theme: "dark" | "light";
  onToggleTheme: () => void;
  status?: PlatformStatus;
  isFetching: boolean;
}

export function StudioShell({
  theme,
  onToggleTheme,
  status,
  isFetching,
}: StudioShellProps) {
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [paletteOpen, setPaletteOpen] = useState(false);

  useEffect(() => {
    const handler = (event: KeyboardEvent) => {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        setPaletteOpen(open => !open);
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, []);

  return (
    <div className={`studio-shell${sidebarCollapsed ? " shell-sidebar-collapsed" : ""}`}>
      <Sidebar
        collapsed={sidebarCollapsed}
        mobileOpen={mobileOpen}
        onToggle={() => setSidebarCollapsed(value => !value)}
        onCloseMobile={() => setMobileOpen(false)}
      />
      <div className="studio-main">
        <Topbar
          theme={theme}
          onToggleTheme={onToggleTheme}
          onOpenPalette={() => setPaletteOpen(true)}
          onOpenMobile={() => setMobileOpen(true)}
          status={status}
        />
        <main className="studio-content">
          <Outlet />
        </main>
        <StatusBar status={status} isFetching={isFetching} />
      </div>
      <CommandPalette open={paletteOpen} onClose={() => setPaletteOpen(false)} />
    </div>
  );
}
