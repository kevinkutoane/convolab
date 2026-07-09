# ConvoLab Frontend

The **Frontend** is a modern React 19 application built with TypeScript, Vite, and Tailwind CSS. It provides a responsive, type-safe user interface for the ConvoLab application.

## Overview

The frontend is a single-page application (SPA) that communicates with the backend API through HTTP requests using Axios. It includes:

- **React 19**: Latest React with hooks and concurrent features
- **TypeScript**: Full type safety across the application
- **Vite**: Lightning-fast development and build experience
- **Tailwind CSS**: Utility-first CSS framework for styling
- **React Router**: Client-side routing and navigation
- **TanStack Query**: Powerful server state management
- **Axios**: HTTP client for API communication

## Project Structure

```
web/
├── src/
│   ├── components/         # Reusable React components
│   ├── pages/              # Page-level components
│   ├── hooks/              # Custom React hooks
│   ├── lib/                # Utility functions and helpers
│   ├── App.tsx             # Main application component
│   ├── main.tsx            # Application entry point
│   └── index.css            # Global styles
├── public/                 # Static assets
├── index.html              # HTML template
├── package.json            # Dependencies and scripts
├── tsconfig.json           # TypeScript configuration
├── vite.config.ts          # Vite configuration
└── README.md               # This file
```

## Getting Started

### Prerequisites

- **Node.js 18+** - [Download](https://nodejs.org/)
- **npm** or **pnpm** - Package manager

### Installation

```bash
# Navigate to frontend directory
cd web

# Install dependencies
npm install
# or
pnpm install
```

### Development

```bash
# Start development server (runs on http://localhost:3000)
npm run dev
# or
pnpm dev
```

The development server includes:
- Hot Module Replacement (HMR) for instant updates
- Fast refresh for React components
- TypeScript checking
- Tailwind CSS compilation

### Building for Production

```bash
# Build production bundle
npm run build
# or
pnpm build

# Output in dist/ directory
```

### Preview Production Build

```bash
# Preview the production build locally
npm run preview
# or
pnpm preview
```

## Technology Stack

| Technology | Version | Purpose |
|---|---|---|
| React | 19+ | UI framework |
| TypeScript | 5.0+ | Type-safe JavaScript |
| Vite | Latest | Build tool and dev server |
| Tailwind CSS | 4.0+ | Utility-first CSS |
| React Router | 6+ | Client-side routing |
| TanStack Query | 5+ | Server state management |
| Axios | Latest | HTTP client |

## Key Features

### 1. Type Safety

All components and hooks are written in TypeScript, providing compile-time type checking and better IDE support.

```tsx
interface UserProps {
  id: number;
  name: string;
  email: string;
}

export function UserCard({ id, name, email }: UserProps) {
  return (
    <div className="p-4 border rounded">
      <h2>{name}</h2>
      <p>{email}</p>
    </div>
  );
}
```

### 2. Component-Based Architecture

Components are organized by feature and reusability:

```
components/
├── Common/           # Shared components (Header, Footer, etc.)
├── Features/         # Feature-specific components
└── UI/               # Atomic UI components
```

### 3. Server State Management with TanStack Query

Manage server state efficiently with automatic caching, synchronization, and background updates:

```tsx
import { useQuery } from '@tanstack/react-query';
import axios from 'axios';

export function UserList() {
  const { data: users, isLoading, error } = useQuery({
    queryKey: ['users'],
    queryFn: async () => {
      const response = await axios.get('/api/users');
      return response.data;
    }
  });

  if (isLoading) return <div>Loading...</div>;
  if (error) return <div>Error: {error.message}</div>;

  return (
    <ul>
      {users?.map(user => (
        <li key={user.id}>{user.name}</li>
      ))}
    </ul>
  );
}
```

### 4. HTTP Communication with Axios

Axios provides a simple, promise-based HTTP client:

```tsx
import axios from 'axios';

const api = axios.create({
  baseURL: 'http://localhost:5000/api',
  timeout: 10000
});

// Add request interceptor
api.interceptors.request.use(config => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export default api;
```

### 5. Routing with React Router

Navigate between pages with client-side routing:

```tsx
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import Home from './pages/Home';
import UserDetail from './pages/UserDetail';
import NotFound from './pages/NotFound';

export function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="/users/:id" element={<UserDetail />} />
        <Route path="*" element={<NotFound />} />
      </Routes>
    </BrowserRouter>
  );
}
```

### 6. Styling with Tailwind CSS

Utility-first CSS framework for rapid UI development:

```tsx
export function Button({ children, variant = 'primary' }) {
  const baseStyles = 'px-4 py-2 rounded font-medium transition';
  const variants = {
    primary: 'bg-blue-500 text-white hover:bg-blue-600',
    secondary: 'bg-gray-200 text-gray-800 hover:bg-gray-300'
  };

  return (
    <button className={`${baseStyles} ${variants[variant]}`}>
      {children}
    </button>
  );
}
```

## Development Guidelines

### 1. Component Naming

- Use PascalCase for component files and names
- Use descriptive names that reflect the component's purpose
- Suffix with `.tsx` for TypeScript React components

### 2. Props Interface

Define props interfaces for all components:

```tsx
interface CardProps {
  title: string;
  description?: string;
  children: React.ReactNode;
}

export function Card({ title, description, children }: CardProps) {
  return (
    <div className="border rounded p-4">
      <h2>{title}</h2>
      {description && <p>{description}</p>}
      {children}
    </div>
  );
}
```

### 3. Custom Hooks

Extract reusable logic into custom hooks:

```tsx
export function useLocalStorage<T>(key: string, initialValue: T) {
  const [storedValue, setStoredValue] = useState<T>(() => {
    try {
      const item = window.localStorage.getItem(key);
      return item ? JSON.parse(item) : initialValue;
    } catch (error) {
      console.error(error);
      return initialValue;
    }
  });

  const setValue = (value: T) => {
    try {
      setStoredValue(value);
      window.localStorage.setItem(key, JSON.stringify(value));
    } catch (error) {
      console.error(error);
    }
  };

  return [storedValue, setValue] as const;
}
```

### 4. Error Handling

Handle errors gracefully in components:

```tsx
export function UserList() {
  const { data, isLoading, error } = useQuery({
    queryKey: ['users'],
    queryFn: fetchUsers
  });

  if (isLoading) return <LoadingSpinner />;
  if (error) return <ErrorMessage error={error} />;
  if (!data?.length) return <EmptyState />;

  return <ul>{data.map(user => <UserItem key={user.id} user={user} />)}</ul>;
}
```

### 5. Performance Optimization

- Use `React.memo` for expensive components
- Use `useMemo` and `useCallback` for expensive computations
- Lazy load routes with `React.lazy` and `Suspense`
- Optimize images and assets

```tsx
const UserDetail = React.lazy(() => import('./pages/UserDetail'));

export function App() {
  return (
    <Suspense fallback={<LoadingSpinner />}>
      <Routes>
        <Route path="/users/:id" element={<UserDetail />} />
      </Routes>
    </Suspense>
  );
}
```

## Testing

### Unit Tests

```bash
# Run tests
npm run test

# Run with coverage
npm run test:coverage

# Watch mode
npm run test:watch
```

Example test:

```tsx
import { render, screen } from '@testing-library/react';
import { Button } from './Button';

describe('Button', () => {
  it('renders with text', () => {
    render(<Button>Click me</Button>);
    expect(screen.getByText('Click me')).toBeInTheDocument();
  });

  it('handles click events', () => {
    const handleClick = jest.fn();
    render(<Button onClick={handleClick}>Click me</Button>);
    screen.getByText('Click me').click();
    expect(handleClick).toHaveBeenCalled();
  });
});
```

## Environment Variables

Create a `.env` file in the `web` directory:

```env
VITE_API_BASE_URL=http://localhost:5000/api
VITE_APP_NAME=ConvoLab
```

Access in code:

```tsx
const apiUrl = import.meta.env.VITE_API_BASE_URL;
const appName = import.meta.env.VITE_APP_NAME;
```

## Performance Tips

1. **Code Splitting**: Lazy load routes and heavy components
2. **Image Optimization**: Use modern formats (WebP) and responsive images
3. **Bundle Analysis**: Use `vite-plugin-visualizer` to analyze bundle size
4. **Caching**: Configure proper cache headers for assets
5. **Minification**: Vite automatically minifies production builds

## Deployment

### Build for Production

```bash
npm run build
```

The production build is optimized and minified in the `dist/` directory.

### Deploy to Static Hosting

```bash
# Deploy dist/ directory to:
# - Vercel
# - Netlify
# - GitHub Pages
# - AWS S3
# - Any static hosting service
```

### Environment Configuration

Create environment-specific files:

- `.env` - Default environment variables
- `.env.development` - Development overrides
- `.env.production` - Production overrides

## Troubleshooting

### Port Already in Use

```bash
# Change port in vite.config.ts
export default {
  server: {
    port: 3001
  }
}
```

### Module Not Found

```bash
# Clear node_modules and reinstall
rm -rf node_modules
npm install
```

### TypeScript Errors

```bash
# Check TypeScript
npm run check

# Fix formatting
npm run format
```

## Best Practices

1. **Keep Components Small**: Each component should have a single responsibility
2. **Use Composition**: Compose components instead of creating large monolithic components
3. **Avoid Prop Drilling**: Use context or state management for deeply nested props
4. **Handle Loading States**: Always show loading indicators for async operations
5. **Error Boundaries**: Wrap components with error boundaries for error handling
6. **Accessibility**: Use semantic HTML and ARIA attributes
7. **Performance**: Monitor and optimize bundle size and rendering performance

## Related Documentation

- See `ARCHITECTURE.md` for overall architecture
- See `src/Api/README.md` for API documentation
- See `package.json` for available scripts and dependencies
