# ConvoLab Studio

> Visual engineering workspace for the ConvoLab Conversational AI Platform.

ConvoLab Studio is a React 19 application built with TypeScript, Vite, and TailwindCSS. It provides engineering, product, and operations teams with dedicated capability workspaces for designing, testing, governing, evaluating, and monitoring intelligent conversations.

---

## 1. Quick Start

### Prerequisites

- **Node.js**: `>=22.22.0`
- **Backend API**: Running on `http://localhost:5000` (or configured via reverse proxy)

### Development Server

```bash
# Install dependencies
npm ci

# Start Vite development server
npm run dev
```

The application will be accessible at [http://localhost:3000](http://localhost:3000). The Vite dev server automatically proxies `/api` and `/health` requests to `http://localhost:5000`.

### Available Scripts

| Script | Command | Purpose |
|---|---|---|
| `dev` | `vite` | Starts local development server with Hot Module Replacement (HMR). |
| `build` | `tsc -b && vite build && node scripts/check-bundle-budget.mjs` | Type-checks, builds production client bundle, and enforces bundle size budgets. |
| `build:app` | `tsc -b && vite build` | Type-checks and bundles without running budget verification script. |
| `lint` | `eslint .` | Runs ESLint across all TypeScript and React source files. |
| `preview` | `vite preview` | Previews the local production build on `http://localhost:4173`. |
| `test` | `node scripts/run-tests.mjs && node scripts/audit-interactions.mjs` | Runs frontend unit tests and accessibility/interaction audit. |
| `test:browser` | `playwright test` | Runs Playwright end-to-end browser tests against running environment. |

---

## 2. Directory Structure

```
web/
├── src/
│   ├── assets/              # Static SVG marks, branding assets, styles
│   ├── components/          # Reusable UI primitives, Shell, Navigation, StatusPill
│   ├── contexts/            # React context providers (AuthContext, ThemeContext, etc.)
│   ├── data/                # Static platform metadata, capability status definitions (platform.ts)
│   ├── hooks/               # Custom React hooks (useAuth, usePlatformStatus, useDebounce)
│   ├── layouts/             # Studio shell, root layouts, header, sidebar
│   ├── pages/               # Lazy-loaded capability workspaces & views:
│   │   ├── AnalyticsPage.tsx             # Executive FinOps, TCO/ROI in ZAR, spend attribution
│   │   ├── ConversationSimulatorPage.tsx # Multi-turn persona simulator & execution inspection
│   │   ├── DashboardPage.tsx             # System capability matrix & active health
│   │   ├── EvaluationStudioPage.tsx      # Versioned scorecards & Golden Dataset CI/CD
│   │   ├── IntelligenceCenterPage.tsx    # Multi-provider routing & budget controls
│   │   ├── KnowledgeStudioPage.tsx       # Hybrid RAG 2.0 (BM25 + Dense Semantic RRF)
│   │   ├── OperationsPage.tsx            # ALM environment promotion, DR & health checks
│   │   ├── PluginCenterPage.tsx          # Plugin registry & lifecycle management
│   │   ├── PolicyCenterPage.tsx          # Runtime policies, PII redaction, prompt shields
│   │   ├── PromptStudioPage.tsx          # Governed prompt templates & approvals
│   │   ├── ReplayStudioPage.tsx          # Snapshot replay & regression comparison
│   │   ├── TraceExplorerPage.tsx         # OpenTelemetry distributed trace waterfall
│   │   └── WorkflowDesignerPage.tsx      # Visual workflow state machine editor
│   ├── services/            # API client layer (Axios / TanStack Query client)
│   ├── types/               # TypeScript domain interfaces and DTOs
│   ├── App.tsx              # Application route tree and route-level error boundaries
│   ├── index.css            # Tailored CSS design tokens, glassmorphism, responsive utilities
│   └── main.tsx             # React DOM entry point
├── scripts/                 # Build budget checks, baseline verifiers, test runners
├── vite.config.ts           # Vite bundler configuration, manual chunks, dev proxy
└── package.json             # Dependencies and build scripts
```

---

## 3. Technology Stack & Design System

- **Framework**: React 19 (`react`, `react-dom`)
- **Routing**: React Router v8 (`react-router`)
- **State & Caching**: TanStack React Query v5 (`@tanstack/react-query`)
- **Icons**: Lucide React (`lucide-react`)
- **Styling**: Vanilla CSS custom properties & TailwindCSS v4
- **Build Tooling**: Vite 8 with TypeScript 6 and Rolldown/ESBuild

### Design Principles

ConvoLab Studio adheres to a modern, dark-themed, glassmorphic design system:
- **Typography**: Inter / Outfit fonts with clear typographic hierarchy.
- **Micro-Interactions**: Smooth transitions, loading skeletons, responsive hover states.
- **Accessibility**: Semantic HTML, full keyboard navigation, screen-reader attributes (`aria-label`, `aria-hidden`).
- **Responsive Layout**: Fluid desktop sidebar with mobile drawer and responsive data tables.

---

## 4. Status Badges & Release Alignment

Each capability page in ConvoLab Studio features a status badge managed in `src/data/platform.ts`:
- **`stable` (Green)**: Fully implemented, thoroughly tested, production-ready.
- **`active` (Blue)**: Working and accessible, receiving active enhancements.
- **`foundation` (Purple)**: Core abstractions scaffolded and undergoing completion.
- **`planned` (Grey)**: On the development roadmap.

See [`RELEASES.md`](../RELEASES.md) for maintenance procedures during release cuts.
