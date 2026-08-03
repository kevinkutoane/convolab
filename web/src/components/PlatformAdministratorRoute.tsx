import { Navigate, Outlet } from "react-router";
import { useAuth } from "../contexts/useAuth";

export function PlatformAdministratorRoute() {
  const { session } = useAuth();
  return session?.isPlatformAdministrator ? <Outlet /> : <Navigate to="/" replace />;
}
