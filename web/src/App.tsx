import { QueryClient, QueryClientProvider, useQuery } from "@tanstack/react-query";
import { BrowserRouter, Route, Routes } from "react-router-dom";
import { StudioShell } from "./components/StudioShell";
import { designTimePlatformStatus, studioPages } from "./data/platform";
import { useTheme } from "./hooks/useTheme";
import { CapabilityPage } from "./pages/CapabilityPage";
import { DashboardPage } from "./pages/DashboardPage";
import { ConversationSimulatorPage } from "./pages/ConversationSimulatorPage";
import { KnowledgeStudioPage } from "./pages/KnowledgeStudioPage";
import { PromptStudioPage } from "./pages/PromptStudioPage";
import { WorkflowDesignerPage } from "./pages/WorkflowDesignerPage";
import { IntelligenceCenterPage } from "./pages/IntelligenceCenterPage";
import { EvaluationStudioPage } from "./pages/EvaluationStudioPage";
import { NotFoundPage } from "./pages/NotFoundPage";
import { DocumentationPage } from "./pages/DocumentationPage";
import { getPlatformStatus } from "./services/platformApi";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
    },
  },
});

function StudioRoutes() {
  const { theme, toggleTheme } = useTheme();
  const platformQuery = useQuery({
    queryKey: ["platform-status"],
    queryFn: getPlatformStatus,
  });
  const platformStatus = platformQuery.data ?? designTimePlatformStatus;

  return (
    <Routes>
      <Route
        element={
          <StudioShell
            theme={theme}
            onToggleTheme={toggleTheme}
            status={platformStatus}
            isFetching={platformQuery.isFetching}
          />
        }
      >
        <Route index element={<DashboardPage status={platformStatus} />} />
        <Route path="conversations" element={<ConversationSimulatorPage />} />
        <Route path="knowledge" element={<KnowledgeStudioPage />} />
        <Route path="prompts" element={<PromptStudioPage />} />
        <Route path="workflows" element={<WorkflowDesignerPage />} />
        <Route path="intelligence" element={<IntelligenceCenterPage />} />
        <Route path="evaluation" element={<EvaluationStudioPage />} />
        <Route path="evaluations" element={<EvaluationStudioPage />} />
        <Route path="documentation/:topic?" element={<DocumentationPage />} />
        {Object.entries(studioPages).filter(([key]) => !["conversations", "knowledge", "prompts", "workflows", "intelligence", "evaluations"].includes(key)).map(([key, definition]) => (
          <Route
            key={key}
            path={key}
            element={<CapabilityPage definition={definition} topic={key} />}
          />
        ))}
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <StudioRoutes />
      </BrowserRouter>
    </QueryClientProvider>
  );
}

export default App;
