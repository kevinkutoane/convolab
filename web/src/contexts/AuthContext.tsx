import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { AuthApiError, getSession, login as loginRequest, logout as logoutRequest, prepareAntiforgery, switchWorkspace as switchRequest, type AuthSession } from "../services/authApi";
import { AuthContext } from "./authState";

const bootstrapRetryDelays = [250, 750, 1_500, 3_000];

function isTransientBootstrapFailure(reason: unknown) {
  if (!(reason instanceof AuthApiError)) return true;
  return reason.status === 408 || reason.status === 425 || reason.status === 429 || reason.status >= 500;
}

export function AuthProvider({children}:{children:ReactNode}) {
  const [session,setSession]=useState<AuthSession>(); const [loading,setLoading]=useState(true); const [error,setError]=useState<string>(); const [attempt,setAttempt]=useState(0); const [workspaceEpoch,setWorkspaceEpoch]=useState(0);
  useEffect(() => {
    let active = true;
    let retryTimer: number | undefined;
    const wait = (delay: number) => new Promise<void>(resolve => {
      retryTimer = window.setTimeout(resolve, delay);
    });
    const bootstrap = async () => {
      setError(undefined);
      for (let index = 0; active; index += 1) {
        try {
          const value = await getSession();
          await prepareAntiforgery();
          if (!active) return;
          setSession(value);
          setError(undefined);
          return;
        } catch (reason: unknown) {
          if (!active) return;
          if (reason instanceof AuthApiError && reason.status === 401) {
            setSession(undefined);
            setError(undefined);
            return;
          }
          const delay = bootstrapRetryDelays[index];
          if (delay !== undefined && isTransientBootstrapFailure(reason)) {
            await wait(delay);
            continue;
          }
          setSession(undefined);
          setError(reason instanceof Error ? reason.message : "Session check failed.");
          return;
        }
      }
    };
    void bootstrap().finally(() => {
      if (active) setLoading(false);
    });
    return () => {
      active = false;
      if (retryTimer !== undefined) window.clearTimeout(retryTimer);
    };
  }, [attempt]);
  const login=useCallback(async(email:string,password:string)=>{ const value=await loginRequest(email,password); await prepareAntiforgery(); setSession(value); setError(undefined); },[]);
  const clearWorkspaceQueries=useCallback(async()=>{const detail:{tasks:Promise<unknown>[]}={tasks:[]};window.dispatchEvent(new CustomEvent("convolab:workspace-changing",{detail}));await Promise.all(detail.tasks)},[]);
  const logout=useCallback(async()=>{ await clearWorkspaceQueries(); await logoutRequest(); setSession(undefined); },[clearWorkspaceQueries]);
  const switchWorkspace=useCallback(async(id:string)=>{ await clearWorkspaceQueries(); const value=await switchRequest(id); setSession(value); setWorkspaceEpoch(value=>value+1); },[clearWorkspaceQueries]);
  const retry=useCallback(()=>{setError(undefined);setLoading(true);setAttempt(value=>value+1)},[]);
  const value=useMemo(()=>({session,loading,error,workspaceEpoch,login,logout,switchWorkspace,retry}),[session,loading,error,workspaceEpoch,login,logout,switchWorkspace,retry]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
