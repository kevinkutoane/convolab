import { useEffect,useMemo,useState } from "react";
import { Braces,CheckCircle2,Code2,GitCompare,Plus,Send,ShieldCheck,Sparkles } from "lucide-react";
import * as api from "../services/promptApi";
import { getApiErrorMessage } from "../services/apiClient";
import { CreateResourceDialog, type CreateResourceField } from "../components/CreateResourceDialog";
import { ErrorState, LoadingState } from "../components/AsyncStates";
import type { PromptDetail,PromptSectionInput,PromptSectionKind,PromptSummary,RenderedPrompt } from "../types/prompt";
import "../App.css";
import "../functional-workspaces.css";

const defaultSections:PromptSectionInput[]=[
 {kind:"System",name:"System instructions",sequence:1,required:true,content:"You are a careful enterprise claims assistant. Answer only from governed knowledge and clearly explain uncertainty."},
 {kind:"Knowledge",name:"Governed knowledge",sequence:2,required:true,content:"<governed_knowledge>\n{{knowledgePackage}}\n</governed_knowledge>"},
 {kind:"Conversation",name:"Conversation context",sequence:3,required:false,content:"{{conversationHistory}}"},
 {kind:"User",name:"Customer message",sequence:4,required:true,content:"{{customerMessage}}"},
 {kind:"Output",name:"Output contract",sequence:5,required:true,content:"Provide a concise answer and cite the relevant source. Never promise claim approval."},
];

const promptFields: CreateResourceField[] = [
 {name:"name",label:"Prompt name",placeholder:"Claims Assistant"},
 {name:"owner",label:"Owner",placeholder:"Conversation team"},
 {name:"category",label:"Category",placeholder:"Claims"},
 {name:"tags",label:"Tags",placeholder:"claims, grounded",required:false},
 {name:"description",label:"Description",type:"textarea",placeholder:"What this prompt governs and where it is used"},
];

const initialPromptDraft = {name:"Claims Assistant",description:"Governed claims assistant prompt",owner:"Kevin",category:"Claims",tags:"claims, grounded"};

export function PromptStudioPage(){
 const [items,setItems]=useState<PromptSummary[]>([]),[selectedId,setSelectedId]=useState<string>(),[detail,setDetail]=useState<PromptDetail>(),[selectedVersion,setSelectedVersion]=useState<string>(),[sections,setSections]=useState(defaultSections),[version,setVersion]=useState("1.0.0"),[preview,setPreview]=useState<RenderedPrompt>(),[message,setMessage]=useState(""),[compareId,setCompareId]=useState<string>();
 const [createOpen,setCreateOpen]=useState(false),[creating,setCreating]=useState(false),[createError,setCreateError]=useState(""),[promptDraft,setPromptDraft]=useState<Record<string,string>>(initialPromptDraft);
 const [initialLoading,setInitialLoading]=useState(true),[initialError,setInitialError]=useState("");
 const current=detail?.versions.find(v=>v.id===selectedVersion)??detail?.versions[0];
 const variables=useMemo(()=>({customerMessage:"Can I claim for hail damage?",knowledgePackage:"[1] Motor Policy — Hail damage: Comprehensive cover may include hail damage subject to schedule, excess and exclusions.",conversationHistory:"user: My vehicle was damaged in yesterday's storm.",workflow:"Claims Intake",knowledgeCollection:"Claims Policies",promptVersion:current?`${detail?.name} v${current.version}`:"Draft"}),[current,detail]);
 async function refresh(preferred?:string){const data=await api.listPrompts();setItems(data);const id=preferred??selectedId??data[0]?.id;if(id){setSelectedId(id);const d=await api.getPrompt(id);setDetail(d);setSelectedVersion(d.versions[0]?.id)}}
 const load=async()=>{setInitialLoading(true);setInitialError("");try{await refresh()}catch(error){setInitialError(getApiErrorMessage(error))}finally{setInitialLoading(false)}};
 useEffect(()=>{const timer=window.setTimeout(()=>void load(),0);return()=>window.clearTimeout(timer)},[]); // eslint-disable-line react-hooks/exhaustive-deps
 async function create(){setCreating(true);setCreateError("");try{const d=await api.createPrompt({name:promptDraft.name.trim(),description:promptDraft.description.trim(),owner:promptDraft.owner.trim(),category:promptDraft.category.trim(),tags:promptDraft.tags.split(",").map(value=>value.trim()).filter(Boolean)});setMessage("Prompt created. Add its first version.");await refresh(d.id);setCreateOpen(false);setPromptDraft(initialPromptDraft)}catch(error){setCreateError(getApiErrorMessage(error))}finally{setCreating(false)}}
 async function saveVersion(){if(!selectedId)return;try{const v=await api.createPromptVersion(selectedId,{version,changeSummary:"Created in Prompt Studio",sections});setMessage(`Version ${v.version} created.`);await refresh(selectedId);setSelectedVersion(v.id)}catch(error){setMessage(getApiErrorMessage(error))}}
 async function transition(action:string){if(!current)return;try{await api.transitionPromptVersion(current.id,action,current.revision);setMessage(`Version ${action} complete.`);await refresh(selectedId)}catch(error){setMessage(getApiErrorMessage(error))}}
 async function render(){if(!current)return;try{setPreview(await api.renderPrompt(current.id,variables))}catch(error){setMessage(getApiErrorMessage(error))}}
 function editSection(index:number,field:keyof PromptSectionInput,value:string|number|boolean){setSections(s=>s.map((x,i)=>i===index?{...x,[field]:value}:x))}
 if(initialLoading)return <LoadingState label="Loading Prompt Studio…"/>;
 if(initialError&&!items.length)return <ErrorState message={initialError} onRetry={()=>void load()}/>;
 return <div className="page-stack prompt-studio-page">
  <section className="page-heading"><div className="page-heading-icon"><Sparkles size={24}/></div><div className="page-heading-copy"><div className="page-heading-meta"><span>Prompt Engine</span><span>Versioned · governed · reusable</span></div><h2>Prompt Studio</h2><p>Compose, validate, approve and publish prompts that the Conversation Simulator can execute.</p></div><button className="primary-button" onClick={()=>setCreateOpen(true)}><Plus size={16}/> New prompt</button></section>
  <CreateResourceDialog open={createOpen} title="New prompt" description="Create a governed prompt definition. Versions and lifecycle controls become available after creation." submitLabel="Create prompt" fields={promptFields} values={promptDraft} busy={creating} error={createError} onChange={(name,value)=>setPromptDraft(current=>({...current,[name]:value}))} onClose={()=>!creating&&setCreateOpen(false)} onSubmit={create}/>
  {message&&<div className="panel knowledge-notice" role="status" aria-live="polite">{message}</div>}
  <section className="prompt-layout">
   <aside className="panel prompt-library"><div className="panel-header"><div><span className="panel-eyebrow">Library</span><h3>{items.length} prompts</h3></div></div>{items.map(p=><button key={p.id} className={`prompt-list-item ${p.id===selectedId?"active":""}`} onClick={()=>{setSelectedId(p.id);api.getPrompt(p.id).then(d=>{setDetail(d);setSelectedVersion(d.versions[0]?.id)}).catch(error=>setMessage(getApiErrorMessage(error)))}}><strong>{p.name}</strong><span>{p.latestVersion} · {p.versionCount} versions</span><small>{p.status} · {p.owner}</small></button>)}{!items.length&&<div className="empty-state compact"><Code2 size={28}/><h3>Create your first prompt</h3></div>}</aside>
   <main className="panel prompt-editor"><div className="workspace-toolbar"><div><span className="panel-eyebrow">Prompt definition</span><h3>{detail?.name??"No prompt selected"}</h3></div>{detail&&<div className="version-actions"><input value={version} onChange={e=>setVersion(e.target.value)} aria-label="Version"/><button className="primary-button" onClick={saveVersion}><Plus size={15}/> Create version</button></div>}</div>
    {detail&&<><div className="prompt-section-list">{sections.map((section,index)=><article className="prompt-section-card" key={`${section.kind}-${index}`}><div className="prompt-section-heading"><select value={section.kind} onChange={e=>editSection(index,"kind",e.target.value as PromptSectionKind)}>{["System","Developer","Knowledge","Conversation","User","Output"].map(k=><option key={k}>{k}</option>)}</select><input value={section.name} onChange={e=>editSection(index,"name",e.target.value)}/><span>{section.content.length} chars</span></div><textarea value={section.content} onChange={e=>editSection(index,"content",e.target.value)} rows={section.kind==="System"?5:3}/></article>)}</div><button className="secondary-button" onClick={()=>setSections(s=>[...s,{kind:"Developer",name:"New section",content:"",sequence:s.length+1,required:false}])}><Plus size={15}/> Add section</button></>}
   </main>
   <aside className="panel prompt-inspector"><div className="panel-header"><div><span className="panel-eyebrow">Version inspector</span><h3>{current?`v${current.version}`:"No version"}</h3></div>{current&&<span className={`status-pill status-${current.status.toLowerCase()}`}>{current.status}</span>}</div>{current&&<><div className="prompt-metrics"><div><span>Tokens</span><strong>{current.estimatedTokens}</strong></div><div><span>Variables</span><strong>{current.variables.length}</strong></div><div><span>Sections</span><strong>{current.sections.length}</strong></div></div><div className="variable-cloud">{current.variables.map(v=><code key={v}>{`{{${v}}}`}</code>)}</div><div className="lifecycle-actions">{current.status==="Draft"&&<button onClick={()=>transition("submit")}><Send size={14}/> Submit</button>}{current.status==="PendingApproval"&&<button onClick={()=>transition("approve")}><ShieldCheck size={14}/> Approve</button>}{current.status==="Approved"&&<button onClick={()=>transition("publish")}><CheckCircle2 size={14}/> Publish</button>}{current.status==="Published"&&<button onClick={()=>transition("deprecate")}>Deprecate</button>}<button onClick={render}><Braces size={14}/> Render preview</button></div><label className="compare-select">Compare with<select value={compareId??""} onChange={e=>setCompareId(e.target.value)}><option value="">Select version</option>{detail?.versions.filter(v=>v.id!==current.id).map(v=><option key={v.id} value={v.id}>v{v.version}</option>)}</select></label>{compareId&&<button className="secondary-button" onClick={async()=>{try{const c=await api.comparePromptVersions(current.id,compareId);setMessage(`Comparison: ${c.tokenDelta>=0?"+":""}${c.tokenDelta} tokens; added variables: ${c.addedVariables.join(", ")||"none"}.`)}catch(error){setMessage(getApiErrorMessage(error))}}}><GitCompare size={14}/> Compare</button>}</>}
   </aside>
  </section>
  <section className="panel prompt-preview"><div className="panel-header"><div><span className="panel-eyebrow">Rendered snapshot</span><h3>{preview?`${preview.promptName} v${preview.version}`:"Render a published or draft version"}</h3></div>{preview&&<span>{preview.estimatedTokens} tokens</span>}</div>{preview?<pre>{preview.renderedText}</pre>:<div className="empty-state compact"><Braces size={26}/><p>Preview resolves test conversation and knowledge variables exactly as the runtime will.</p></div>}</section>
 </div>
}
