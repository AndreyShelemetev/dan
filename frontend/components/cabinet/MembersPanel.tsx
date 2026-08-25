"use client";

import { useState, type FormEvent } from "react";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { Field } from "@/components/ui/Field";
import { ApiError } from "@/lib/api/client";
import {
  PERMISSION,
  inviteMember,
  revokeMember,
  type BurialSiteMember,
} from "@/lib/api/burialSites";

const PERMISSION_LABEL: Record<string, string> = {
  [PERMISSION.view]: "Только смотрит",
  [PERMISSION.order]: "Может заказывать уход",
  [PERMISSION.manage]: "Управляет карточкой",
};

/**
 * Family access to one record.
 *
 * The invitation link is shown once and never again, because only its hash is
 * stored server-side. That is a real constraint of the design, not an oversight,
 * so the UI says so plainly and makes the link easy to copy while it is on screen.
 */
export function MembersPanel({
  siteId,
  initialMembers,
  canManage,
  isOwner,
}: {
  siteId: number;
  initialMembers: BurialSiteMember[];
  canManage: boolean;
  isOwner: boolean;
}) {
  const [members, setMembers] = useState(initialMembers);
  const [isInviting, setInviting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [freshLink, setFreshLink] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  async function handleInvite(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setFreshLink(null);
    setCopied(false);

    const form = event.currentTarget;
    const data = new FormData(form);
    const contact = String(data.get("contact") ?? "").trim();
    const permission = String(data.get("permission") ?? PERMISSION.view);

    if (!contact) {
      setError("Укажите email родственника.");
      return;
    }

    setInviting(true);
    try {
      const created = await inviteMember(siteId, contact, permission);
      setMembers((current) => [...current, created.member]);

      const origin = typeof window !== "undefined" ? window.location.origin : "";
      setFreshLink(`${origin}/app/invitations/${encodeURIComponent(created.token)}`);
      form.reset();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Не удалось отправить приглашение.");
    } finally {
      setInviting(false);
    }
  }

  async function handleRevoke(memberId: number) {
    setError(null);
    try {
      await revokeMember(siteId, memberId);
      setMembers((current) => current.filter((m) => m.id !== memberId));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Не удалось отозвать доступ.");
    }
  }

  async function copyLink() {
    if (!freshLink) return;
    try {
      await navigator.clipboard.writeText(freshLink);
      setCopied(true);
    } catch {
      // Clipboard access can be denied; the link is visible and selectable anyway.
      setCopied(false);
    }
  }

  return (
    <Card className="flex flex-col gap-6">
      <div>
        <h2 className="font-display text-xl font-normal text-ink-1">Доступ родственников</h2>
        <p className="mt-2 text-sm text-ink-2">
          Забота не должна зависеть от памяти одного человека. Пригласите родных — они
          увидят место и историю визитов.
        </p>
      </div>

      {members.length > 0 ? (
        <ul className="flex list-none flex-col gap-3 p-0">
          {members.map((member) => (
            <li
              key={member.id}
              className="flex flex-wrap items-center justify-between gap-3 border-t border-border pt-3"
            >
              <div>
                <p className="text-base text-ink-1">{member.contact ?? "Участник"}</p>
                <p className="text-xs text-ink-2">
                  {PERMISSION_LABEL[member.permission] ?? member.permission}
                  {member.accepted ? " · принял приглашение" : " · приглашение отправлено"}
                </p>
              </div>
              {canManage ? (
                <Button variant="ghost" size="sm" onClick={() => void handleRevoke(member.id)}>
                  Отозвать
                </Button>
              ) : null}
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-sm text-ink-2">Пока доступ есть только у вас.</p>
      )}

      {canManage ? (
        <form onSubmit={handleInvite} noValidate className="flex flex-col gap-4 border-t border-border pt-5">
          <Field
            id="contact"
            name="contact"
            type="email"
            label="Email родственника"
            placeholder="mother@example.com"
            autoComplete="off"
          />

          <div className="flex flex-col gap-1.5">
            <label htmlFor="permission" className="text-sm font-semibold text-ink-1">
              Что он сможет
            </label>
            <select
              id="permission"
              name="permission"
              defaultValue={PERMISSION.view}
              aria-describedby="permission-hint"
              className="min-h-hit rounded-input border border-border-strong bg-surface-raised px-3.5 py-3 text-base text-ink-1 outline-none transition-colors duration-ds ease-ds focus-visible:shadow-focus"
            >
              <option value={PERMISSION.view}>{PERMISSION_LABEL[PERMISSION.view]}</option>
              <option value={PERMISSION.order}>{PERMISSION_LABEL[PERMISSION.order]}</option>
              {isOwner ? (
                <option value={PERMISSION.manage}>{PERMISSION_LABEL[PERMISSION.manage]}</option>
              ) : null}
            </select>
            <p id="permission-hint" className="text-xs text-ink-2">
              По умолчанию — только просмотр. Право заказывать уход означает возможность
              тратить деньги, поэтому выдаётся отдельно.
            </p>
          </div>

          {error ? (
            <div role="alert" className="rounded-card border border-danger bg-danger-soft px-4 py-3 text-sm text-danger">
              {error}
            </div>
          ) : null}

          <div>
            <Button type="submit" variant="secondary" size="sm" disabled={isInviting}>
              {isInviting ? "Создаём…" : "Пригласить"}
            </Button>
          </div>
        </form>
      ) : null}

      {freshLink ? (
        <div className="flex flex-col gap-3 rounded-card border border-accent bg-accent-soft px-4 py-4">
          <p className="text-sm font-semibold text-accent-deep">
            Ссылка-приглашение готова. Она показывается один раз
          </p>
          <p className="text-sm text-ink-2">
            Мы храним только её отпечаток, поэтому показать повторно не сможем. Передайте
            ссылку родственнику любым удобным способом.
          </p>
          <code className="block overflow-x-auto rounded-input border border-border bg-surface-raised px-3 py-2 text-xs text-ink-1">
            {freshLink}
          </code>
          <div>
            <Button variant="secondary" size="sm" onClick={() => void copyLink()}>
              {copied ? "Скопировано" : "Скопировать ссылку"}
            </Button>
          </div>
        </div>
      ) : null}
    </Card>
  );
}
