import { api } from "./apiClient";
import type { PromptComparison,PromptDetail,PromptSectionInput,PromptSummary,PromptVersion,RenderedPrompt } from "../types/prompt";
export const listPrompts=async()=> (await api.get<PromptSummary[]>("/api/prompts")).data;
export const getPrompt=async(id:string)=> (await api.get<PromptDetail>(`/api/prompts/${id}`)).data;
export const createPrompt=async(input:{name:string;description:string;owner:string;category:string;tags:string[]})=> (await api.post<PromptDetail>("/api/prompts",input)).data;
export const createPromptVersion=async(id:string,input:{version:string;changeSummary:string;sections:PromptSectionInput[];expectedPromptRevision?:number})=> (await api.post<PromptVersion>(`/api/prompts/${id}/versions`,input)).data;
export const transitionPromptVersion=async(id:string,action:string,expectedRevision?:number)=> (await api.post<PromptVersion>(`/api/prompts/versions/${id}/${action}`,{actor:"Studio user",reason:`${action} from ConvoLab Studio`,expectedRevision})).data;
export const renderPrompt=async(versionId:string,variables:Record<string,string>)=> (await api.post<RenderedPrompt>("/api/prompts/render",{versionId,variables})).data;
export const comparePromptVersions=async(left:string,right:string)=> (await api.get<PromptComparison>("/api/prompts/compare",{params:{left,right}})).data;
