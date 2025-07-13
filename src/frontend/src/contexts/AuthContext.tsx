"use client";

import React, {
  createContext,
  useContext,
  useState,
  useEffect,
  ReactNode,
} from "react";
import { authApi } from "@/lib/api";
import { User } from "@/types";

interface AuthContextType {
  user: User | null;
  isLoading: boolean;
  login: (
    userName: string,
    password: string,
    rememberMe?: boolean
  ) => Promise<void>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    checkAuthStatus();
  }, []);

  const checkAuthStatus = async () => {
    try {
      const currentUser = await authApi.getCurrentUser();
      if (currentUser && currentUser.isAuthenticated) {
        setUser(currentUser);
      } else {
        setUser(null);
      }
    } catch {
      setUser(null);
    } finally {
      setIsLoading(false);
    }
  };

  const login = async (
    userName: string,
    password: string,
    rememberMe = true
  ) => {
    await authApi.login({ userName: userName, password, rememberMe });
    setUser({ userName: userName, isAuthenticated: true });
  };

  const logout = async () => {
    await authApi.logout();
    setUser(null);
  };

  const refreshUser = async () => {
    try {
      const currentUser = await authApi.getCurrentUser();
      if (currentUser && currentUser.isAuthenticated) {
        setUser(currentUser);
      } else {
        setUser(null);
      }
    } catch {
      setUser(null);
    }
  };

  const value = {
    user,
    isLoading,
    login,
    logout,
    refreshUser,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}
