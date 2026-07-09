import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

const queryClient = new QueryClient();

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <Router>
        <div className="min-h-screen bg-gray-50 flex flex-col items-center justify-center p-4">
          <header className="mb-8 text-center">
            <h1 className="text-5xl font-extrabold text-slate-900 tracking-tight mb-2">ConvoLab</h1>
            <p className="text-xl text-slate-600">Enterprise React + .NET 10 Foundation</p>
          </header>
          
          <main className="w-full max-w-2xl">
            <Routes>
              <Route path="/" element={<Home />} />
            </Routes>
          </main>
          
          <footer className="mt-auto py-6 text-slate-400 text-sm">
            Sprint 0 - Architecture Foundation
          </footer>
        </div>
      </Router>
    </QueryClientProvider>
  );
}

function Home() {
  return (
    <div className="bg-white p-8 rounded-2xl shadow-xl border border-slate-100">
      <h2 className="text-2xl font-bold text-slate-800 mb-4">Architecture Verified</h2>
      <ul className="space-y-3">
        <li className="flex items-center text-slate-600">
          <span className="w-2 h-2 bg-green-500 rounded-full mr-3"></span>
          React 19 + TypeScript + Vite
        </li>
        <li className="flex items-center text-slate-600">
          <span className="w-2 h-2 bg-green-500 rounded-full mr-3"></span>
          Tailwind CSS 4.0
        </li>
        <li className="flex items-center text-slate-600">
          <span className="w-2 h-2 bg-green-500 rounded-full mr-3"></span>
          TanStack Query + Axios
        </li>
        <li className="flex items-center text-slate-600">
          <span className="w-2 h-2 bg-green-500 rounded-full mr-3"></span>
          React Router
        </li>
      </ul>
    </div>
  );
}

export default App;
