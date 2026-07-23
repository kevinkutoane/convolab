import { createContext } from "react";
import type { AuthSession } from "../services/authApi";

export interface AuthContextValue { session?:AuthSession; loading:boolean; error?:string; workspaceEpoch:number; login:(email:string,password:string)=>Promise<void>; logout:()=>Promise<void>; switchWorkspace:(id:string)=>Promise<void>; retry:()=>void }
export const AuthContext=createContext<AuthContextValue|undefined>(undefined);
