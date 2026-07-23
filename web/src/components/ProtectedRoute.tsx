import { Navigate, Outlet, useLocation } from "react-router-dom";
import { ErrorState, LoadingState } from "./AsyncStates";
import { useAuth } from "../contexts/useAuth";

export function ProtectedRoute(){const auth=useAuth();const location=useLocation();if(auth.loading)return <LoadingState label="Checking secure session…"/>;if(auth.error)return <ErrorState title="Session unavailable" message={auth.error} onRetry={auth.retry}/>;if(!auth.session)return <Navigate to="/login" replace state={{from:location.pathname}}/>;if(!auth.session.activeWorkspaceId&&location.pathname!=="/workspace/select")return <Navigate to="/workspace/select" replace/>;return <Outlet/>}
