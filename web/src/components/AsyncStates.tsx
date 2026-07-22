import { Component, Suspense, type ErrorInfo, type ReactNode } from "react";
import { AlertTriangle, CheckCircle2, Inbox, LoaderCircle, RefreshCw, WifiOff } from "lucide-react";
import { Outlet } from "react-router-dom";

export function LoadingState({ label = "Loading workspace…", compact = false }: { label?: string; compact?: boolean }) {
  return <div className={`async-state async-loading${compact ? " async-compact" : ""}`} role="status" aria-live="polite"><LoaderCircle className="spin" size={24} /><span>{label}</span></div>;
}

export function EmptyState({ title, description, action }: { title: string; description: string; action?: ReactNode }) {
  return <div className="async-state async-empty"><Inbox size={26} /><strong>{title}</strong><p>{description}</p>{action}</div>;
}

export function ErrorState({ title = "This workspace could not be loaded", message, onRetry }: { title?: string; message: string; onRetry?: () => void }) {
  return <div className="async-state async-error" role="alert"><AlertTriangle size={26} /><strong>{title}</strong><p>{message}</p>{onRetry && <button className="secondary-button" onClick={onRetry}><RefreshCw size={15} /> Retry</button>}</div>;
}

export function MutationProgress({ label }: { label: string }) {
  return <div className="async-mutation" role="status" aria-live="polite"><LoaderCircle className="spin" size={16} /><span>{label}</span></div>;
}

export function SuccessState({ title, message }: { title: string; message?: string }) {
  return <div className="async-state async-success async-compact" role="status" aria-live="polite"><CheckCircle2 size={26} /><strong>{title}</strong>{message && <p>{message}</p>}</div>;
}

export function OfflineState({ message = "The ConvoLab API is unavailable.", onRetry }: { message?: string; onRetry?: () => void }) {
  return <div className="async-state async-offline" role="alert"><WifiOff size={26} /><strong>Studio is offline</strong><p>{message}</p>{onRetry && <button className="secondary-button" onClick={onRetry}><RefreshCw size={15} /> Retry connection</button>}</div>;
}

class RouteErrorBoundary extends Component<{ children: ReactNode }, { error?: Error }> {
  state: { error?: Error } = {};
  static getDerivedStateFromError(error: Error) { return { error }; }
  componentDidCatch(error: Error, info: ErrorInfo) { console.error("Route rendering failed", error, info.componentStack); }
  render() {
    if (this.state.error) return <ErrorState message={this.state.error.message} onRetry={() => this.setState({ error: undefined })} />;
    return this.props.children;
  }
}

export function RouteBoundary() {
  return <RouteErrorBoundary><Suspense fallback={<LoadingState label="Loading Studio workspace…" />}><Outlet /></Suspense></RouteErrorBoundary>;
}
