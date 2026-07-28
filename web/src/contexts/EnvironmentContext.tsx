import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import type { RuntimeEnvironment } from "../types/settings";
import { useAuth } from "./useAuth";

interface EnvironmentContextValue {
  environments: RuntimeEnvironment[];
  activeEnvironment?: RuntimeEnvironment;
  activeEnvironmentId?: string;
  setActiveEnvironmentId: (id: string) => void;
  isLoading: boolean;
  refetch: () => void;
}

const EnvironmentContext = createContext<EnvironmentContextValue | undefined>(undefined);

const storageKey = (workspaceId: string) => `convolab.environment.${workspaceId}`;

/**
 * Provides the per-workspace runtime environment list and the user's selected
 * active environment. The selection persists per workspace in localStorage and
 * falls back to the default environment when the stored one disappears.
 *
 * Deliberately uses plain fetch (not react-query, not the axios client) so the
 * Studio shell's initial JS chunk stays within the bundle budget; the settings
 * API service module is only pulled in by lazily-loaded route pages.
 */
export function EnvironmentProvider({ children }: { children: ReactNode }) {
  const auth = useAuth();
  const workspaceId = auth.session?.activeWorkspaceId;
  const [state, setState] = useState<{ workspaceId?: string; environments: RuntimeEnvironment[]; isLoading: boolean }>({
    environments: [],
    isLoading: true,
  });
  // Track selections per workspace so switching workspaces restores the stored choice.
  const [selections, setSelections] = useState<Record<string, string | undefined>>({});
  const epochRef = useRef(0);

  const load = useCallback(() => {
    const epoch = ++epochRef.current;
    if (!workspaceId) {
      queueMicrotask(() => {
        if (epoch === epochRef.current) setState({ environments: [], isLoading: false });
      });
      return;
    }
    queueMicrotask(() => {
      if (epoch === epochRef.current) setState(current => ({ ...current, isLoading: true }));
    });
    fetch(`/api/workspaces/${workspaceId}/environments`, { credentials: "include" })
      .then(response => (response.ok ? (response.json() as Promise<RuntimeEnvironment[]>) : Promise.reject(new Error(`Environments failed (${response.status}).`))))
      .then(environments => {
        if (epoch !== epochRef.current) return;
        setState({ workspaceId, environments, isLoading: false });
      })
      .catch(() => {
        if (epoch !== epochRef.current) return;
        setState({ workspaceId, environments: [], isLoading: false });
      });
  }, [workspaceId]);

  useEffect(() => {
    load();
  }, [load]);

  // Refresh the list when settings pages broadcast environment mutations.
  useEffect(() => {
    const handler = () => load();
    window.addEventListener("convolab:environments-changed", handler);
    return () => window.removeEventListener("convolab:environments-changed", handler);
  }, [load]);

  const environments = useMemo(
    () => state.environments.filter(environment => environment.status !== "Archived"),
    [state.environments],
  );

  const selectedId = workspaceId
    ? selections[workspaceId] ?? localStorage.getItem(storageKey(workspaceId)) ?? undefined
    : undefined;

  const activeEnvironment = useMemo(() => {
    if (!environments.length) return undefined;
    return (
      environments.find(environment => environment.id === selectedId) ??
      environments.find(environment => environment.isDefault) ??
      environments[0]
    );
  }, [environments, selectedId]);

  const setActiveEnvironmentId = useCallback(
    (id: string) => {
      if (!workspaceId) return;
      setSelections(current => ({ ...current, [workspaceId]: id }));
      localStorage.setItem(storageKey(workspaceId), id);
    },
    [workspaceId],
  );

  const value = useMemo<EnvironmentContextValue>(
    () => ({
      environments,
      activeEnvironment,
      activeEnvironmentId: activeEnvironment?.id,
      setActiveEnvironmentId,
      isLoading: state.isLoading,
      refetch: load,
    }),
    [environments, activeEnvironment, setActiveEnvironmentId, state.isLoading, load],
  );

  return <EnvironmentContext.Provider value={value}>{children}</EnvironmentContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useEnvironment(): EnvironmentContextValue {
  const context = useContext(EnvironmentContext);
  if (!context) throw new Error("useEnvironment must be used within EnvironmentProvider");
  return context;
}
