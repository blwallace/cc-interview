import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { App } from './App';
import { IdentityProvider } from './identity';
import { ToastProvider } from './toasts';
import './styles.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Availability is contended by definition — a stale grid is how a user picks a taken slot.
      staleTime: 0,
      retry: false,
    },
  },
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <IdentityProvider>
        <ToastProvider>
          <App />
        </ToastProvider>
      </IdentityProvider>
    </QueryClientProvider>
  </StrictMode>,
);
