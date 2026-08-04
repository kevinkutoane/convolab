import { useEffect } from "react";
import { Outlet } from "react-router";
import { QueryClientProvider } from "@tanstack/react-query";
import { useAuth } from "../contexts/useAuth";
import { queryClient } from "../queryClient";

export function QueryRouteOutlet() {
  const { workspaceEpoch } = useAuth();
  useEffect(()=>{const clear=(event:Event)=>{const detail=(event as CustomEvent<{tasks:Promise<unknown>[]}>).detail;detail.tasks.push(queryClient.cancelQueries().then(()=>queryClient.clear()))};window.addEventListener("convolab:workspace-changing",clear);window.addEventListener("convolab:environment-changing",clear);return()=>{window.removeEventListener("convolab:workspace-changing",clear);window.removeEventListener("convolab:environment-changing",clear)}},[]);
  return <QueryClientProvider client={queryClient}><div key={workspaceEpoch}><Outlet /></div></QueryClientProvider>;
}
