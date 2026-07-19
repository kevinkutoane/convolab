export type Classification = "Public" | "Internal" | "Confidential" | "Restricted";
export type CollectionStatus = "Active" | "Archived";
export type DocumentStatus = "Uploaded" | "Queued" | "Extracting" | "Chunking" | "Processed" | "PendingApproval" | "Approved" | "Published" | "Failed" | "Deprecated" | "Archived";
export interface KnowledgeCollection { id:string; name:string; description:string; owner:string; classification:Classification; status:CollectionStatus; documentCount:number; chunkCount:number; createdAt:string; updatedAt:string; revision:number; }
export interface KnowledgeDocument { id:string; collectionId:string; title:string; originalFileName:string; contentType:string; sizeBytes:number; status:DocumentStatus; classification:Classification; owner:string; category:string; tags:string[]; version:number; error?:string; updatedAt:string; publishedAt?:string; revision:number; }
export interface KnowledgeChunk { id:string; sequence:number; text:string; pageNumber?:number; section?:string; estimatedTokens:number; published:boolean; }
export interface SearchResult { chunkId:string; documentTitle:string; rank:number; confidence:number; text:string; pageNumber?:number; section?:string; matchingTerms:string[]; estimatedTokens:number; }
export interface QueryResponse { collectionId:string; query:string; tokenEstimate:number; results:SearchResult[]; exclusions:string[]; retrievedAt:string; }
