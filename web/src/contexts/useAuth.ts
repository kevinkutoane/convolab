import { useContext } from "react";
import { AuthContext } from "./authState";

export function useAuth(){const value=useContext(AuthContext);if(!value)throw new Error("useAuth must be used inside AuthProvider");return value}
