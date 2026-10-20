# Knowledge Studio & Hybrid Retrieval (RAG 2.0)

Knowledge Studio turns the Knowledge Engine into a functional enterprise product surface. Users create collections, upload supported files, process them into governed chunks, approve and publish content, execute deterministic hybrid retrieval, and select published collections in Conversation Simulator and live agents.

---

## Supported Formats

PDF, DOCX, TXT, Markdown. Maximum upload size: 20 MB.

---

## Document Lifecycle

`Uploaded` → `Extracting` → `Chunking` → `Processed` → `PendingApproval` → `Approved` → `Published`.

Failed documents may be retried. Published documents are immutable. Confidential and Restricted documents require approval before publication. Deprecated and archived documents are excluded from retrieval.

---

## Storage & Chunking

Metadata and chunks are stored in the configured EF Core database. Original files are stored outside the web root using `IKnowledgeDocumentStorage`. Docker uses the `knowledge_documents` volume mounted at `/app/data/knowledge-documents`.

Chunks carry:
- Unique chunk ID and sequence index
- Source document ID and title
- Text content and token estimate
- Content classification (Public, Internal, Confidential, Restricted)

---

## Enterprise Hybrid Retrieval Engine (RAG 2.0)

ConvoLab features a hybrid retriever ([`HybridKnowledgeRetriever.cs`](../src/Infrastructure/ConvoLab.Infrastructure/KnowledgeStudio/HybridKnowledgeRetriever.cs)) implementing Reciprocal Rank Fusion (RRF) between lexical BM25 matching and hash-based vector similarity:

> [!NOTE]
> **Planned Upgrade**: Current vector retrieval uses deterministic 1024-dimensional hash-based vector clustering (FNV-1a) across domain concept clusters and character trigrams, which operates without external AI model dependencies. Upgrading to a real, learned embedding provider (e.g. OpenAI/Azure `text-embedding-3` or Gemini embeddings) backed by a dedicated vector database is a planned upgrade on the roadmap.

### 1. BM25 Lexical Keyword Matching
- Tokenizes query and document chunks, removing common stopwords.
- Computes Term Frequency (TF) and Inverse Document Frequency (IDF) with BM25 length normalization ($k_1 = 1.2$, $b = 0.75$).
- Exact multi-word phrase matches receive an additional relevance boost ($1.5\times$).

### 2. Hash-Based Vector Retrieval (Multi-Hash Clustering)
- Computes 1024-dimensional normalized vectors for queries and chunk content via multi-hash concept clustering (deterministic FNV-1a hashing across terms, character trigrams, and concept clusters).
- Measures vector similarity using Cosine Distance:
  $$\text{CosineSimilarity}(\vec{u}, \vec{v}) = \frac{\vec{u} \cdot \vec{v}}{\|\vec{u}\| \|\vec{v}\|}$$
- Clusters related domain concepts (e.g., vehicle, incident, claim terms) into coordinated hash buckets.

### 3. Reciprocal Rank Fusion (RRF)
- Blends the discrete rankings from BM25 and vector similarity search:
  $$\text{RRF Score}(d) = \sum_{m \in \{\text{BM25}, \text{Vector}\}} \frac{w_m}{k + \text{Rank}_m(d)}$$
  where $k = 60$, with configurable weights ($w_{\text{vector}} = 0.60$, $w_{\text{lexical}} = 0.40$).
- Deduplicates chunks across rankings and enforces maximum citation token budgets.
