import { apiFetch } from "./client";

export interface AdminUser {
  id: number;
  role: string;
  status: string;
  displayName: string | null;
  email: string | null;
  phone: string | null;
  mfaEnabled: boolean;
  lastLoginAt: string | null;
  createdAt: string;
}

export interface CreateAdminUser {
  email: string;
  role: string;
  displayName?: string;
}

const base = "/admin/users";

function headers(cookieHeader?: string): HeadersInit | undefined {
  return cookieHeader ? { Cookie: cookieHeader } : undefined;
}

export const adminUsers = {
  list: (role: string | undefined, cookieHeader?: string) =>
    apiFetch<AdminUser[]>(`${base}${role ? `?role=${encodeURIComponent(role)}` : ""}`, {
      headers: headers(cookieHeader),
    }),

  create: (body: CreateAdminUser) => apiFetch<AdminUser>(base, { method: "POST", body }),

  changeRole: (id: number, role: string) =>
    apiFetch<AdminUser>(`${base}/${id}/role`, { method: "PATCH", body: { role } }),

  deactivate: (id: number) => apiFetch<AdminUser>(`${base}/${id}/deactivate`, { method: "POST" }),

  activate: (id: number) => apiFetch<AdminUser>(`${base}/${id}/activate`, { method: "POST" }),
};

/** Same order as `UserRoles.All` in `Models/Auth/User.cs`. */
export const ROLES = [
  "client",
  "executor",
  "dispatcher",
  "qa",
  "support",
  "finance",
  "admin",
  "superadmin",
] as const;

export const ROLE_LABEL: Record<string, string> = {
  client: "Клиент",
  executor: "Исполнитель",
  dispatcher: "Диспетчер",
  qa: "Контроль качества",
  support: "Поддержка",
  finance: "Финансы",
  admin: "Администратор",
  superadmin: "Суперадминистратор",
};

export const USER_STATUS_LABEL: Record<string, string> = {
  active: "Активен",
  blocked: "Деактивирован",
  deleted: "Удалён",
};
