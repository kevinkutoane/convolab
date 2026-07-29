import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App";
import { AuthProvider } from "./contexts/AuthContext";
import "./index.css";

const staleDeploymentReloadKey = "convolab.stale-deployment-reload";
const staleDeploymentReloadWindowMs = 15_000;

window.addEventListener("vite:preloadError", event => {
  event.preventDefault();
  const now = Date.now();
  const previousReload = Number(sessionStorage.getItem(staleDeploymentReloadKey) ?? "0");
  if (now - previousReload < staleDeploymentReloadWindowMs) return;
  sessionStorage.setItem(staleDeploymentReloadKey, String(now));
  window.location.reload();
});

window.setTimeout(() => {
  sessionStorage.removeItem(staleDeploymentReloadKey);
}, staleDeploymentReloadWindowMs);

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AuthProvider><App /></AuthProvider>
  </StrictMode>
);
