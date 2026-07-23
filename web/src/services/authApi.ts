export class AuthApiError extends Error { readonly status:number;readonly correlationId?:string;constructor(message:string,status:number,correlationId?:string){super(correlationId?`${message} Correlation: ${correlationId}.`:message);this.status=status;this.correlationId=correlationId} }
let antiforgeryRequestToken:string|undefined;
async function request<T>(path:string, init:RequestInit={}){const headers=new Headers(init.headers);if(init.body)headers.set("Content-Type","application/json");if(antiforgeryRequestToken&&!(["GET","HEAD","OPTIONS"].includes((init.method??"GET").toUpperCase())))headers.set("X-XSRF-TOKEN",antiforgeryRequestToken);const response=await fetch(path,{...init,headers,credentials:"include"});if(!response.ok){const problem=await response.json().catch(()=>({}));throw new AuthApiError(problem.detail??`Request failed (${response.status}).`,response.status,problem.correlationId)}if(response.status===204)return undefined as T;return await response.json() as T}

export interface WorkspaceChoice { id:string; organisationId:string; name:string; role:string }
export interface AuthSession { userId:string; email:string; displayName:string; isPlatformAdministrator:boolean; expiresAt:string; activeWorkspaceId?:string; workspaces:WorkspaceChoice[] }

export async function getSession() { return request<AuthSession>("/api/auth/session"); }
export async function login(email:string,password:string) { return request<AuthSession>("/api/auth/login",{method:"POST",body:JSON.stringify({email,password})}); }
export async function prepareAntiforgery() { const value=await request<{token:string}>("/api/auth/antiforgery");antiforgeryRequestToken=value.token; }
export async function logout() { await request("/api/auth/logout",{method:"POST"}); }
export async function switchWorkspace(workspaceId:string) { return request<AuthSession>("/api/auth/workspace",{method:"POST",body:JSON.stringify({workspaceId})}); }

export interface WorkspaceDetail { id:string; organisationId:string; name:string; slug:string; description:string; status:string; revision:number; createdAt:string; updatedAt:string }
export interface WorkspaceMember { id:string; userId:string; email:string; displayName:string; role:string; status:string; revision:number; createdAt:string }
export interface ServiceAccount { id:string; name:string; scopesJson:string; status:string; expiresAt?:string; lastUsedAt?:string; revision:number; createdAt:string }
export interface AuditEvent { id:string; actorDisplay:string; actorType:string; action:string; resourceType:string; resourceId?:string; outcome:string; correlationId:string; occurredAt:string }
export async function getWorkspace(id:string) { return request<WorkspaceDetail>(`/api/workspaces/${id}`); }
export async function getMembers(id:string) { return request<WorkspaceMember[]>(`/api/workspaces/${id}/members`); }
export async function getServiceAccounts(id:string) { return request<ServiceAccount[]>(`/api/workspaces/${id}/service-accounts`); }
export async function getAudit(id:string) { return request<AuditEvent[]>(`/api/workspaces/${id}/audit`); }
export async function inviteMember(id:string,input:{email:string;displayName:string;role:string}) { return request(`/api/workspaces/${id}/members`,{method:"POST",body:JSON.stringify(input)}); }
export async function createServiceAccount(id:string,input:{name:string;scopes:string[];expiresAt?:string}) { return request<{credential:string}>(`/api/workspaces/${id}/service-accounts`,{method:"POST",body:JSON.stringify(input)}); }
