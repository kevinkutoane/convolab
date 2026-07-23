import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useEffect } from "react";
import { Outlet } from "react-router-dom";
import { useAuth } from "../contexts/useAuth";

const queryClient = new QueryClient({defaultOptions:{queries:{staleTime:30_000,refetchOnWindowFocus:false}}});

export function QueryRouteOutlet() {
  const { workspaceEpoch } = useAuth();
  useEffect(()=>{const clear=(event:Event)=>{const detail=(event as CustomEvent<{tasks:Promise<unknown>[]}>).detail;detail.tasks.push(queryClient.cancelQueries().then(()=>queryClient.clear()))};window.addEventListener("convolab:workspace-changing",clear);return()=>window.removeEventListener("convolab:workspace-changing",clear)},[]);
  return <QueryClientProvider client={queryClient}><div key={workspaceEpoch}><Outlet /></div></QueryClientProvider>;
}
