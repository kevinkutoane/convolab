import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { getSession, login as loginRequest, logout as logoutRequest, prepareAntiforgery, switchWorkspace as switchRequest, type AuthSession } from "../services/authApi";
import { AuthContext } from "./authState";

export function AuthProvider({children}:{children:ReactNode}) {
  const [session,setSession]=useState<AuthSession>(); const [loading,setLoading]=useState(true); const [error,setError]=useState<string>(); const [attempt,setAttempt]=useState(0); const [workspaceEpoch,setWorkspaceEpoch]=useState(0);
  useEffect(()=>{ let active=true; getSession().then(async value=>{ if(!active)return; setSession(value); setError(undefined); await prepareAntiforgery(); }).catch((reason:unknown)=>{ if(!active)return; setSession(undefined); if(!(typeof reason==="object"&&reason&&"status" in reason&&(reason as {status?:number}).status===401)) setError(reason instanceof Error?reason.message:"Session check failed."); }).finally(()=>{if(active)setLoading(false)}); return()=>{active=false}; },[attempt]);
  const login=useCallback(async(email:string,password:string)=>{ const value=await loginRequest(email,password); await prepareAntiforgery(); setSession(value); setError(undefined); },[]);
  const clearWorkspaceQueries=useCallback(async()=>{const detail:{tasks:Promise<unknown>[]}={tasks:[]};window.dispatchEvent(new CustomEvent("convolab:workspace-changing",{detail}));await Promise.all(detail.tasks)},[]);
  const logout=useCallback(async()=>{ await clearWorkspaceQueries(); await logoutRequest(); setSession(undefined); },[clearWorkspaceQueries]);
  const switchWorkspace=useCallback(async(id:string)=>{ await clearWorkspaceQueries(); const value=await switchRequest(id); setSession(value); setWorkspaceEpoch(value=>value+1); },[clearWorkspaceQueries]);
  const retry=useCallback(()=>{setLoading(true);setAttempt(value=>value+1)},[]);
  const value=useMemo(()=>({session,loading,error,workspaceEpoch,login,logout,switchWorkspace,retry}),[session,loading,error,workspaceEpoch,login,logout,switchWorkspace,retry]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
