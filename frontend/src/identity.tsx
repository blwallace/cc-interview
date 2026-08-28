import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { setIdentity } from './api/fetcher';

/**
 * Who the app is currently acting as. Stands in for an authenticated session — see docs/DESIGN.md §1.
 * Freely switchable precisely because it is a mock; real identity would arrive in a signed token.
 */

export type Building = { id: string; name: string };
export type SimulatedUser = { id: string; label: string };

export const BUILDINGS: Building[] = [
  { id: 'building-101', name: 'Maple Court' },
  { id: 'building-202', name: 'Harbourview Towers' },
];

export const USERS: SimulatedUser[] = [
  { id: 'resident-101', label: 'Resident A' },
  { id: 'resident-102', label: 'Resident B' },
  { id: 'admin-999', label: 'Manager' },
];

type IdentityValue = {
  tenantId: string;
  userId: string;
  setTenantId: (id: string) => void;
  setUserId: (id: string) => void;
};

const IdentityContext = createContext<IdentityValue | null>(null);

export function IdentityProvider({ children }: { children: ReactNode }) {
  const [tenantId, setTenantIdState] = useState(BUILDINGS[0].id);
  const [userId, setUserIdState] = useState(USERS[0].id);
  const queryClient = useQueryClient();

  // The generated query keys are just URLs — they don't include identity. Rather than fight that,
  // drop the cache whenever the caller changes, so one building's data can never be shown to
  // another. Cheap here, and it fails safe.
  const applyAndReset = useCallback(
    (next: { tenantId: string; userId: string }) => {
      setIdentity(next);
      queryClient.clear();
    },
    [queryClient],
  );

  const value = useMemo<IdentityValue>(
    () => ({
      tenantId,
      userId,
      setTenantId: (id) => {
        setTenantIdState(id);
        applyAndReset({ tenantId: id, userId });
      },
      setUserId: (id) => {
        setUserIdState(id);
        applyAndReset({ tenantId, userId: id });
      },
    }),
    [tenantId, userId, applyAndReset],
  );

  return <IdentityContext.Provider value={value}>{children}</IdentityContext.Provider>;
}

export function useIdentity() {
  const ctx = useContext(IdentityContext);
  if (!ctx) throw new Error('useIdentity must be used inside IdentityProvider');
  return ctx;
}
