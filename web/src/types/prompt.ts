export type PromptStatus="Draft"|"PendingApproval"|"Approved"|"Published"|"Deprecated"|"Archived";
export type PromptSectionKind="System"|"Developer"|"Knowledge"|"Conversation"|"User"|"Output";
export interface PromptSection { id:string; kind:PromptSectionKind; name:string; content:string; sequence:number; required:boolean }
export interface PromptVersion { id:string; promptId:string; version:string; status:PromptStatus; changeSummary:string; sections:PromptSection[]; variables:string[]; estimatedTokens:number; createdAt:string; updatedAt:string; publishedAt?:string|null; revision:number }
export interface PromptSummary { id:string; name:string; description:string; owner:string; category:string; tags:string[]; status:PromptStatus; latestVersion:string; versionCount:number; updatedAt:string; revision:number }
export interface PromptDetail extends Omit<PromptSummary,"latestVersion"|"versionCount"> { versions:PromptVersion[]; createdAt:string }
export interface RenderedPrompt { promptId:string; versionId:string; promptName:string; version:string; renderedText:string; missingVariables:string[]; estimatedTokens:number }
export interface PromptComparison { left:PromptVersion; right:PromptVersion; tokenDelta:number; addedVariables:string[]; removedVariables:string[] }
export interface PromptSectionInput { kind:PromptSectionKind; name:string; content:string; sequence:number; required:boolean }
