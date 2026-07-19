import { useEffect, useState } from "react";
import { BookOpen, FileUp, FolderPlus, Play, Search, ShieldCheck } from "lucide-react";
import * as api from "../services/knowledgeApi";
import { getApiErrorMessage } from "../services/apiClient";
import type { KnowledgeChunk, KnowledgeCollection, KnowledgeDocument, QueryResponse } from "../types/knowledge";

export function KnowledgeStudioPage(){
 const [collections,setCollections]=useState<KnowledgeCollection[]>([]); const [selected,setSelected]=useState<string>(); const [documents,setDocuments]=useState<KnowledgeDocument[]>([]); const [chunks,setChunks]=useState<KnowledgeChunk[]>([]); const [query,setQuery]=useState(""); const [results,setResults]=useState<QueryResponse>(); const [message,setMessage]=useState("");
 const current=collections.find(x=>x.id===selected);
 const refresh=async()=>{const c=await api.listCollections();setCollections(c);if(!selected&&c[0])setSelected(c[0].id)};
 // Initial API synchronization. State updates occur after the promise resolves.
 // eslint-disable-next-line react-hooks/set-state-in-effect, react-hooks/exhaustive-deps
 useEffect(()=>{void refresh().catch(e=>setMessage(getApiErrorMessage(e)))},[]);
 useEffect(()=>{if(selected)api.listDocuments(selected).then(setDocuments).catch(e=>setMessage(getApiErrorMessage(e)))},[selected]);
 async function create(){const name=prompt("Collection name","Claims Policies");if(!name)return;try{await api.createCollection({name,description:"Governed enterprise knowledge",owner:"Kevin",classification:"Internal"});await refresh()}catch(error){setMessage(getApiErrorMessage(error))}}
 async function upload(file?:File){if(!file||!selected)return;setMessage("Uploading...");try{const d=await api.uploadDocument(selected,file,"Kevin","Internal");setDocuments(await api.listDocuments(selected));setMessage(`${d.title} uploaded. Process it next.`)}catch(error){setMessage(getApiErrorMessage(error))}}
 async function act(d:KnowledgeDocument,action:string){setMessage(`${action}...`);try{if(action==="process")await api.processDocument(d.id);else await api.transitionDocument(d.id,action,d.revision);setDocuments(await api.listDocuments(selected!));setMessage("Done")}catch(error){setMessage(getApiErrorMessage(error))}}
 async function inspect(d:KnowledgeDocument){try{setChunks(await api.getChunks(d.id))}catch(error){setMessage(getApiErrorMessage(error))}}
 async function search(){if(!selected||!query.trim())return;try{setResults(await api.queryCollection(selected,query))}catch(error){setMessage(getApiErrorMessage(error))}}
 return <div className="page-stack knowledge-studio">
  <section className="page-heading"><div className="page-heading-icon"><BookOpen size={24}/></div><div className="page-heading-copy"><div className="page-heading-meta"><span>Knowledge Engine</span></div><h2>Knowledge Studio</h2><p>Upload, govern, publish and test real enterprise knowledge.</p></div><button className="primary-button" onClick={create}><FolderPlus size={16}/> New collection</button></section>
  {message&&<div className="panel knowledge-notice">{message}</div>}
  <section className="knowledge-grid">
   <aside className="panel knowledge-list"><div className="panel-header"><div><span className="panel-eyebrow">Collections</span><h3>{collections.length} collections</h3></div></div>{collections.map(c=><button key={c.id} className={`knowledge-row ${selected===c.id?"active":""}`} onClick={()=>setSelected(c.id)}><strong>{c.name}</strong><span>{c.documentCount} docs · {c.chunkCount} chunks</span><small>{c.classification} · {c.status}</small></button>)}{!collections.length&&<p className="muted">Create your first collection.</p>}</aside>
   <main className="panel knowledge-main"><div className="workspace-toolbar"><div><span className="panel-eyebrow">Collection</span><h3>{current?.name??"Select a collection"}</h3></div>{current&&<label className="primary-button file-button"><FileUp size={16}/> Upload document<input type="file" accept=".pdf,.docx,.txt,.md,.markdown" onChange={e=>upload(e.target.files?.[0])}/></label>}</div>
    <div className="document-table">{documents.map(d=><article key={d.id} className="document-card"><div><strong>{d.title}</strong><span>{d.originalFileName} · {(d.sizeBytes/1024).toFixed(1)} KB</span><small>{d.status} · v{d.version} · {d.classification}</small>{d.error&&<em>{d.error}</em>}</div><div className="document-actions">{["Uploaded","Failed"].includes(d.status)&&<button onClick={()=>act(d,"process")}><Play size={14}/> Process</button>}{d.status==="Processed"&&<><button onClick={()=>act(d,"submit")}>Submit</button><button onClick={()=>act(d,"publish")}>Publish</button></>}{d.status==="PendingApproval"&&<button onClick={()=>act(d,"approve")}><ShieldCheck size={14}/> Approve</button>}{d.status==="Approved"&&<button onClick={()=>act(d,"publish")}>Publish</button>}<button onClick={()=>inspect(d)}>Chunks</button></div></article>)}{current&&!documents.length&&<div className="empty-state compact"><FileUp size={28}/><h3>Upload a policy or guide</h3><p>PDF, DOCX, TXT and Markdown are supported.</p></div>}</div>
   </main>
  </section>
  <section className="knowledge-grid lower"><article className="panel retrieval-panel"><div className="panel-header"><div><span className="panel-eyebrow">Retrieval test</span><h3>Ask this collection</h3></div></div><div className="query-box"><input value={query} onChange={e=>setQuery(e.target.value)} placeholder="Can I claim for hail damage?"/><button className="primary-button" onClick={search}><Search size={15}/> Retrieve</button></div>{results?.results.map(r=><div className="result-card" key={r.chunkId}><div><strong>#{r.rank} {r.documentTitle}</strong><span>{Math.round(r.confidence*100)}% · {r.estimatedTokens} tokens</span></div><p>{r.text}</p><small>{r.section??"Document"}{r.pageNumber?` · page ${r.pageNumber}`:""} · matches: {r.matchingTerms.join(", ")}</small></div>)}{results&&!results.results.length&&<p className="muted">No published chunks matched this query.</p>}</article>
   <aside className="panel chunk-panel"><div className="panel-header"><div><span className="panel-eyebrow">Chunk inspector</span><h3>{chunks.length} chunks</h3></div></div>{chunks.slice(0,8).map(c=><div className="chunk-card" key={c.id}><strong>Chunk {c.sequence}</strong><span>{c.estimatedTokens} tokens {c.pageNumber?`· page ${c.pageNumber}`:""}</span><p>{c.text}</p></div>)}{!chunks.length&&<p className="muted">Select “Chunks” on a processed document.</p>}</aside>
  </section>
 </div>
}
