# Knowledge Studio

Knowledge Studio turns the Knowledge Engine into a functional product surface. Users create collections, upload supported files, process them into governed chunks, approve and publish content, test deterministic retrieval, and select published collections in Conversation Simulator.

## Supported formats

PDF, DOCX, TXT, Markdown. Maximum upload size: 20 MB.

## Lifecycle

Uploaded → Extracting → Chunking → Processed → PendingApproval → Approved → Published.

Failed documents may be retried. Published documents are immutable. Confidential and Restricted documents require approval before publication. Deprecated and archived documents are excluded from retrieval.

## Storage

Metadata and chunks are stored in the configured EF Core database. Original files are stored outside the web root using `IKnowledgeDocumentStorage`. Docker uses the `knowledge_documents` volume mounted at `/app/data/knowledge-documents`.

## Retrieval

V1 uses deterministic keyword retrieval. It searches only published chunks in the selected collection, ranks exact phrase and term matches, applies minimum confidence, maximum results and token-budget constraints, and returns source citations.
