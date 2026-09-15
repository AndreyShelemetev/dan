"use client";

import { useMemo, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { Field } from "@/components/ui/Field";
import { ApiError } from "@/lib/api/client";
import {
  ROLES,
  ROLE_LABEL,
  USER_STATUS_LABEL,
  adminUsers,
  type AdminUser,
} from "@/lib/api/adminUsers";
import { cn } from "@/lib/ui/cn";

const inputClass =
  "min-h-hit rounded-input border border-border-strong bg-surface-raised px-3.5 py-3 text-base text-ink-1 outline-none transition-colors duration-ds ease-ds focus-visible:shadow-focus disabled:opacity-60";

function StatusBadge({ status }: { status: string }) {
  const tone =
    status === "active"
      ? "border-success bg-success-soft text-success"
      : status === "blocked"
        ? "border-danger bg-danger-soft text-danger"
        : "border-border-strong bg-surface text-ink-2";

  return (
    <span className={cn("inline-flex items-center rounded-pill border px-3 py-1 text-xs font-semibold", tone)}>
      {USER_STATUS_LABEL[status] ?? status}
    </span>
  );
}

function formatDate(value: string | null): string {
  if (!value) return "—";
  return new Date(value).toLocaleString("ru-RU", { dateStyle: "medium", timeStyle: "short" });
}

/**
 * Staff and executor accounts. Outside Development there is no `DevAccountSeeder`, so this screen
 * is the only way to give someone a role other than `client` — OTP self-registration always
 * creates a client.
 */
export function UsersManager({ users }: { users: AdminUser[] }) {
  const router = useRouter();
  const [roleFilter, setRoleFilter] = useState<string>("all");
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<number | null>(null);

  const filtered = useMemo(
    () => (roleFilter === "all" ? users : users.filter((u) => u.role === roleFilter)),
    [users, roleFilter],
  );

  async function run(id: number, action: () => Promise<unknown>) {
    setError(null);
    setBusyId(id);
    try {
      await action();
      router.refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Не удалось выполнить действие.");
    } finally {
      setBusyId(null);
    }
  }

  if (creating) {
    return (
      <CreateUserForm
        onDone={() => {
          setCreating(false);
          router.refresh();
        }}
        onCancel={() => setCreating(false)}
      />
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-normal text-ink-1">Пользователи</h1>
          <p className="mt-1 text-sm text-ink-2">
            Сотрудники и исполнители. Клиент заводит себя сам входом по коду — здесь заводят
            только роли, которые не может выдать себе регистрация.
          </p>
        </div>
        <Button onClick={() => setCreating(true)}>Новый пользователь</Button>
      </div>

      <div className="flex flex-col gap-1.5 sm:w-64">
        <label htmlFor="role-filter" className="text-sm font-semibold text-ink-1">
          Роль
        </label>
        <select
          id="role-filter"
          value={roleFilter}
          onChange={(e) => setRoleFilter(e.target.value)}
          className={inputClass}
        >
          <option value="all">Все роли</option>
          {ROLES.map((role) => (
            <option key={role} value={role}>
              {ROLE_LABEL[role]}
            </option>
          ))}
        </select>
      </div>

      {error ? (
        <div role="alert" className="rounded-card border border-danger bg-danger-soft px-4 py-3 text-sm text-danger">
          {error}
        </div>
      ) : null}

      {filtered.length === 0 ? (
        <Card>
          <p className="text-sm text-ink-2">
            {users.length === 0 ? "Пользователей пока нет." : "По этой роли никого не найдено."}
          </p>
        </Card>
      ) : (
        <Card className="flex flex-col gap-4">
          <div className="-mx-2 overflow-x-auto">
            <table className="w-full min-w-[48rem] border-collapse text-sm">
              <thead>
                <tr className="border-b border-border text-left text-xs uppercase tracking-caps text-ink-2">
                  <th className="px-2 py-2 font-semibold">Пользователь</th>
                  <th className="px-2 py-2 font-semibold">Роль</th>
                  <th className="px-2 py-2 font-semibold">Статус</th>
                  <th className="px-2 py-2 font-semibold">MFA</th>
                  <th className="px-2 py-2 font-semibold">Последний вход</th>
                  <th className="px-2 py-2 font-semibold">Действия</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((user) => (
                  <tr key={user.id} className="border-b border-border last:border-0">
                    <td className="px-2 py-3">
                      <div className="text-ink-1">{user.displayName || "Без имени"}</div>
                      <div className="text-xs text-ink-2">{user.email ?? user.phone ?? "—"}</div>
                    </td>
                    <td className="px-2 py-3">
                      <label className="sr-only" htmlFor={`role-${user.id}`}>
                        Роль пользователя {user.displayName || user.email || user.id}
                      </label>
                      <select
                        id={`role-${user.id}`}
                        value={user.role}
                        disabled={busyId === user.id}
                        onChange={(e) => {
                          const nextRole = e.target.value;
                          if (nextRole === user.role) return;
                          if (
                            !window.confirm(
                              `Сменить роль на «${ROLE_LABEL[nextRole] ?? nextRole}»? Это действие сразу меняет доступ пользователя.`,
                            )
                          ) {
                            return;
                          }
                          void run(user.id, () => adminUsers.changeRole(user.id, nextRole));
                        }}
                        className={cn(inputClass, "min-h-0 px-3 py-2 text-sm")}
                      >
                        {ROLES.map((role) => (
                          <option key={role} value={role}>
                            {ROLE_LABEL[role]}
                          </option>
                        ))}
                      </select>
                    </td>
                    <td className="px-2 py-3">
                      <StatusBadge status={user.status} />
                    </td>
                    <td className="px-2 py-3 text-ink-2">{user.mfaEnabled ? "Включена" : "Нет"}</td>
                    <td className="px-2 py-3 text-ink-2">{formatDate(user.lastLoginAt)}</td>
                    <td className="px-2 py-3">
                      {user.status === "active" ? (
                        <Button
                          variant="ghost"
                          size="sm"
                          disabled={busyId === user.id}
                          onClick={() => {
                            if (
                              !window.confirm(
                                `Деактивировать «${user.displayName || user.email || "пользователя"}»? Все его текущие сессии будут завершены.`,
                              )
                            ) {
                              return;
                            }
                            void run(user.id, () => adminUsers.deactivate(user.id));
                          }}
                          className="text-danger hover:bg-danger-soft hover:text-danger"
                        >
                          Деактивировать
                        </Button>
                      ) : (
                        <Button
                          variant="ghost"
                          size="sm"
                          disabled={busyId === user.id}
                          onClick={() => void run(user.id, () => adminUsers.activate(user.id))}
                        >
                          Активировать
                        </Button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      )}
    </div>
  );
}

function CreateUserForm({ onDone, onCancel }: { onDone: () => void; onCancel: () => void }) {
  const [email, setEmail] = useState("");
  const [role, setRole] = useState<string>("dispatcher");
  const [displayName, setDisplayName] = useState("");

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldError, setFieldError] = useState<Record<string, string>>({});

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setFieldError({});
    setBusy(true);

    try {
      await adminUsers.create({
        email: email.trim(),
        role,
        displayName: displayName.trim() || undefined,
      });
      onDone();
    } catch (err) {
      if (err instanceof ApiError) {
        const field =
          err.details && typeof err.details === "object" && "field" in err.details
            ? String((err.details as { field: unknown }).field)
            : null;
        if (field) setFieldError({ [field]: err.message });
        else setError(err.message);
      } else {
        setError("Не удалось создать пользователя.");
      }
      setBusy(false);
    }
  }

  return (
    <Card className="flex flex-col gap-6">
      <h2 className="font-display text-xl font-normal text-ink-1">Новый пользователь</h2>
      <p className="text-sm text-ink-2">
        Учётная запись создаётся сразу с ролью. Пароль не нужен — человек входит по коду на этот
        email, как обычный клиент.
      </p>

      <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-6">
        <Field
          id="new-user-email"
          label="Email"
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          error={fieldError.email}
          required
        />

        <div className="flex flex-col gap-1.5">
          <label htmlFor="new-user-role" className="text-sm font-semibold text-ink-1">
            Роль
          </label>
          <select
            id="new-user-role"
            value={role}
            onChange={(e) => setRole(e.target.value)}
            className={inputClass}
          >
            {ROLES.map((r) => (
              <option key={r} value={r}>
                {ROLE_LABEL[r]}
              </option>
            ))}
          </select>
          {fieldError.role ? <p className="text-xs text-danger">{fieldError.role}</p> : null}
        </div>

        <Field
          id="new-user-name"
          label="Отображаемое имя"
          hint="Необязательно"
          value={displayName}
          onChange={(e) => setDisplayName(e.target.value)}
          error={fieldError.displayName}
        />

        {error ? (
          <div role="alert" className="rounded-card border border-danger bg-danger-soft px-4 py-3 text-sm text-danger">
            {error}
          </div>
        ) : null}

        <div className="flex flex-wrap gap-3 border-t border-border pt-5">
          <Button type="submit" disabled={busy}>
            {busy ? "Создаём…" : "Создать"}
          </Button>
          <Button type="button" variant="secondary" onClick={onCancel}>
            Отмена
          </Button>
        </div>
      </form>
    </Card>
  );
}
