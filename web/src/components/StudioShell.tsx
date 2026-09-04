import { useEffect, useState } from "react";
import { Outlet } from "react-router";
import type { PlatformStatus } from "../types/platform";
import { EnvironmentProvider } from "../contexts/EnvironmentContext";
import { CommandPalette } from "./CommandPalette";
import { Sidebar } from "./Sidebar";
import { StatusBar } from "./StatusBar";
import { Topbar } from "./Topbar";

interface StudioShellProps {
  theme: "dark" | "light";
  onToggleTheme: () => void;
  status?: PlatformStatus;
  isFetching: boolean;
  statusStale?: boolean;
}

export function StudioShell({
  theme,
  onToggleTheme,
  status,
  isFetching,
  statusStale = false,
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
    <EnvironmentProvider>
    <div className={`studio-shell${sidebarCollapsed ? " shell-sidebar-collapsed" : ""}`}>
      <Sidebar
        collapsed={sidebarCollapsed}
        mobileOpen={mobileOpen}
        version={status?.version ? `v${status.version}` : undefined}
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
          isFetchingStatus={isFetching}
          statusStale={statusStale}
        />
        {status?.safeMode && (
          <div className="safe-mode-banner" role="alert">
            Platform safe mode is active. External execution and plugin activation are blocked.
            {statusStale ? " Status refresh is temporarily unavailable; this is the last known state." : ""}
          </div>
        )}
        <main className="studio-content">
          <Outlet />
        </main>
        <StatusBar status={status} isFetching={isFetching} statusStale={statusStale} />
      </div>
      <CommandPalette open={paletteOpen} onClose={() => setPaletteOpen(false)} />
    </div>
    </EnvironmentProvider>
  );
}
