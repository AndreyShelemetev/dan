import { Card } from "@/components/ui/Card";
import { QaReview } from "@/components/admin/QaReview";
import { MEDIA_OWNER, byPhase, listMedia } from "@/lib/api/media";
import { qa } from "@/lib/api/visits";
import { getSessionCookieHeader } from "@/lib/auth/serverCookie";

export const dynamic = "force-dynamic";

export default async function QaQueuePage() {
  const cookieHeader = getSessionCookieHeader();
  const queue = await qa.queue(cookieHeader);

  const withPhotos = await Promise.all(
    queue.map(async (visit) => ({
      visit,
      photos: await listMedia(MEDIA_OWNER.visit, visit.id, cookieHeader).catch(() => []),
    })),
  );

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="font-display text-2xl font-normal text-ink-1">Проверка отчётов</h1>
        <p className="mt-1 max-w-prose text-sm text-ink-2">
          Клиент видит отчёт только после проверки. Всё, что проходит отсюда, становится
          доказательством выполненной работы — и основанием закрыть заказ.
        </p>
      </div>

      {withPhotos.length === 0 ? (
        <Card>
          <p className="text-sm text-ink-2">Отчётов на проверке нет.</p>
        </Card>
      ) : (
        withPhotos.map(({ visit, photos }) => (
          <QaReview
            key={visit.id}
            visit={visit}
            before={byPhase(photos, "before")}
            after={byPhase(photos, "after")}
          />
        ))
      )}
    </div>
  );
}
