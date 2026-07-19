import { Activity, CircleCheck, CloudOff, GitBranch, Server } from "lucide-react";
import type { PlatformStatus } from "../types/platform";

interface StatusBarProps {
  status?: PlatformStatus;
  isFetching: boolean;
}

export function StatusBar({ status, isFetching }: StatusBarProps) {
  const apiOnline = status?.apiHealth === "Healthy";
  return (
    <footer className="status-bar">
      <div className="status-bar-group">
        <span>
          <GitBranch size={13} /> main
        </span>
        <span>
          <CircleCheck size={13} /> architecture healthy
        </span>
        <span>
          <Activity size={13} /> {status?.capabilities.length ?? 0} capabilities
        </span>
      </div>
      <div className="status-bar-group">
        <span className={apiOnline ? "status-online" : "status-muted"}>
          {apiOnline ? <Server size={13} /> : <CloudOff size={13} />}
          {isFetching ? "checking API" : apiOnline ? "API connected" : "design-time mode"}
        </span>
        <span>{status?.version ?? "1.0.0-alpha"}</span>
      </div>
    </footer>
  );
}
