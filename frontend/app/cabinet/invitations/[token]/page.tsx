import { AcceptInvitation } from "@/components/cabinet/AcceptInvitation";
import { SectionEyebrow } from "@/components/ui/SectionEyebrow";

export const dynamic = "force-dynamic";

export const metadata = {
  title: "Приглашение · Память рядом",
};

export default function AcceptInvitationPage({ params }: { params: { token: string } }) {
  return (
    <div className="mx-auto flex w-full max-w-hero flex-col gap-8">
      <div>
        <SectionEyebrow>Приглашение</SectionEyebrow>
        <h1 className="mt-3 font-display text-3xl font-normal text-ink-1">
          Вас пригласили к месту памяти
        </h1>
      </div>

      <AcceptInvitation token={decodeURIComponent(params.token)} />
    </div>
  );
}
