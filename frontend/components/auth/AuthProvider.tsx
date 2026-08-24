"use client";

import { createContext, useCallback, useContext, useState, type ReactNode } from "react";
import { logout as apiLogout, type AuthUser } from "@/lib/api/auth";

interface AuthContextValue {
  user: AuthUser | null;
  isAuthenticated: boolean;
  /** Sets the current user directly, e.g. right after a successful OTP verify. */
  setUser: (user: AuthUser | null) => void;
  /** Calls the backend logout endpoint and clears local state regardless of outcome. */
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return ctx;
}

/**
 * App-wide auth context. Hydrated from `initialUser`, which the root layout
 * fetches server-side via lib/auth/server.ts#getServerUser — deliberately
 * NOT re-fetched here on mount, so client components have the current user
 * on first paint with no extra client-side waterfall request.
 *
 * This provider renders no markup of its own; the visible auth surfaces it
 * feeds are components/site/SiteHeader.tsx (the «Войти» / «Выйти» control)
 * and app/login/page.tsx, both styled with the design system in components/ui.
 */
export function AuthProvider({
  initialUser,
  children,
}: {
  initialUser: AuthUser | null;
  children: ReactNode;
}) {
  const [user, setUser] = useState<AuthUser | null>(initialUser);

  const logout = useCallback(async () => {
    try {
      await apiLogout();
    } finally {
      // Clear local state even if the request failed — the user asked to
      // log out and expects the UI to reflect that either way.
      setUser(null);
    }
  }, []);

  return (
    <AuthContext.Provider
      value={{ user, isAuthenticated: user !== null, setUser, logout }}
    >
      {children}
    </AuthContext.Provider>
  );
}
