import { lazy, useEffect, useState } from "react";
import { BrowserRouter, Route, Routes } from "react-router";
import { StudioShell } from "./components/StudioShell";
import { RouteBoundary } from "./components/AsyncStates";
import { designTimePlatformStatus, studioPages } from "./data/platform";
import { useTheme } from "./hooks/useTheme";
import type { PlatformStatus } from "./types/platform";
import { ProtectedRoute } from "./components/ProtectedRoute";
import { PlatformAdministratorRoute } from "./components/PlatformAdministratorRoute";
import { useAuth } from "./contexts/useAuth";
import "./App.css";
import "./functional-workspaces.css";
import "./workspace-identity.css";
import "./operations.css";
import "./plugin-center.css";
import "./settings-studio.css";
import "./analytics.css";

const CapabilityPage = lazy(() => import("./pages/CapabilityPage").then(module => ({ default: module.CapabilityPage })));
const DashboardPage = lazy(() => import("./pages/DashboardPage").then(module => ({ default: module.DashboardPage })));
const ConversationSimulatorPage = lazy(() => import("./pages/ConversationSimulatorPage").then(module => ({ default: module.ConversationSimulatorPage })));
const KnowledgeStudioPage = lazy(() => import("./pages/KnowledgeStudioPage").then(module => ({ default: module.KnowledgeStudioPage })));
const PromptStudioPage = lazy(() => import("./pages/PromptStudioPage").then(module => ({ default: module.PromptStudioPage })));
const WorkflowDesignerPage = lazy(() => import("./pages/WorkflowDesignerPage").then(module => ({ default: module.WorkflowDesignerPage })));
const IntelligenceCenterPage = lazy(() => import("./pages/IntelligenceCenterPage").then(module => ({ default: module.IntelligenceCenterPage })));
const EvaluationStudioPage = lazy(() => import("./pages/EvaluationStudioPage").then(module => ({ default: module.EvaluationStudioPage })));
const TraceExplorerPage = lazy(() => import("./pages/TraceExplorerPage").then(module => ({ default: module.TraceExplorerPage })));
const ReplayStudioPage = lazy(() => import("./pages/ReplayStudioPage").then(module => ({ default: module.ReplayStudioPage })));
const PolicyCenterPage = lazy(() => import("./pages/PolicyCenterPage").then(module => ({ default: module.PolicyCenterPage })));
const PluginCenterPage = lazy(() => import("./pages/PluginCenterPage").then(module => ({ default: module.PluginCenterPage })));
const NotFoundPage = lazy(() => import("./pages/NotFoundPage").then(module => ({ default: module.NotFoundPage })));
const DocumentationPage = lazy(() => import("./pages/DocumentationPage").then(module => ({ default: module.DocumentationPage })));
const QueryRouteOutlet = lazy(() => import("./components/QueryRouteOutlet").then(module => ({ default: module.QueryRouteOutlet })));
const LoginPage = lazy(() => import("./pages/LoginPage").then(module => ({ default: module.LoginPage })));
const WorkspacePage = lazy(() => import("./pages/WorkspacePage").then(module => ({ default: module.WorkspacePage })));
const SettingsPage = lazy(() => import("./pages/SettingsPage").then(module => ({ default: module.SettingsPage })));
const AnalyticsPage = lazy(() => import("./pages/AnalyticsPage").then(module => ({ default: module.AnalyticsPage })));
const OperationsPage = lazy(() => import("./pages/OperationsPage").then(module => ({ default: module.OperationsPage })));

function StudioRoutes() {
  const { theme, toggleTheme } = useTheme();
  const { session, loading, workspaceEpoch } = useAuth();
  const [platformStatus, setPlatformStatus] = useState<PlatformStatus>(designTimePlatformStatus);
  const [isFetchingStatus, setIsFetchingStatus] = useState(true);
  const [statusStale, setStatusStale] = useState(false);
  useEffect(() => {
    let controller = new AbortController();
    const load = () => {
      controller.abort();
      controller = new AbortController();
      setIsFetchingStatus(true);
      fetch("/api/platform/status", { signal: controller.signal, credentials: "include" })
        .then(response => response.ok
          ? response.json() as Promise<PlatformStatus>
          : Promise.reject(new Error(`Platform status failed (${response.status}).`)))
        .then(value => { setPlatformStatus(value); setStatusStale(false); })
        .catch(error => {
          if (error instanceof Error && error.name !== "AbortError") setStatusStale(true);
        })
        .finally(() => setIsFetchingStatus(false));
    };
    const visibility = () => {
      if (document.visibilityState === "visible") load();
    };
    const focus = () => load();
    load();
    const poll = window.setInterval(load, 45_000);
    window.addEventListener("convolab:platform-status", load);
    window.addEventListener("focus", focus);
    document.addEventListener("visibilitychange", visibility);
    return () => {
      controller.abort();
      window.clearInterval(poll);
      window.removeEventListener("convolab:platform-status", load);
      window.removeEventListener("focus", focus);
      document.removeEventListener("visibilitychange", visibility);
    };
  }, []);
  useEffect(() => {
    if (!loading)
      window.dispatchEvent(new Event("convolab:platform-status"));
  }, [loading, session?.userId, workspaceEpoch]);

  return (
    <Routes>
      <Route element={<RouteBoundary />}>
      <Route path="login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
       <Route
        element={
          <StudioShell
            theme={theme}
            onToggleTheme={toggleTheme}
            status={platformStatus}
            isFetching={isFetchingStatus}
            statusStale={statusStale}
          />
        }
      >
        <Route element={<RouteBoundary />}>
          <Route element={<QueryRouteOutlet />}>
          <Route index element={<DashboardPage status={platformStatus} />} />
          <Route path="conversations" element={<ConversationSimulatorPage />} />
          <Route path="knowledge" element={<KnowledgeStudioPage />} />
          <Route path="prompts" element={<PromptStudioPage />} />
          <Route path="workflows" element={<WorkflowDesignerPage />} />
          <Route path="intelligence" element={<IntelligenceCenterPage />} />
          <Route path="evaluation" element={<EvaluationStudioPage />} />
          <Route path="evaluations" element={<EvaluationStudioPage />} />
          <Route path="traces" element={<TraceExplorerPage />} />
          <Route path="replay" element={<ReplayStudioPage />} />
          <Route path="policies" element={<PolicyCenterPage />} />
          <Route path="plugins" element={<PluginCenterPage />} />
          <Route path="settings" element={<SettingsPage />} />
          <Route path="settings/:section" element={<SettingsPage />} />
          <Route path="analytics" element={<AnalyticsPage />} />
          <Route element={<PlatformAdministratorRoute />}>
            <Route path="operations" element={<OperationsPage />} />
          </Route>
          <Route path="workspace" element={<WorkspacePage />} />
          <Route path="workspace/select" element={<WorkspacePage selectionOnly />} />
          <Route path="documentation/:topic?" element={<DocumentationPage />} />
          {Object.entries(studioPages).filter(([key]) => !["conversations", "knowledge", "prompts", "workflows", "intelligence", "evaluations", "traces", "replay", "policies", "plugins", "settings", "analytics"].includes(key)).map(([key, definition]) => (
            <Route key={key} path={key} element={<CapabilityPage definition={definition} topic={key} />} />
          ))}
          <Route path="*" element={<NotFoundPage />} />
          </Route>
        </Route>
      </Route>
      </Route>
      </Route>
    </Routes>
  );
}

function App() {
  return (
    <BrowserRouter>
      <StudioRoutes />
    </BrowserRouter>
  );
}

export default App;
