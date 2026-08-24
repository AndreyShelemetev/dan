import { SiteHeader } from "@/components/site/SiteHeader";
import { SiteFooter } from "@/components/site/SiteFooter";
import { Hero } from "@/components/home/Hero";
import { PackagesSection } from "@/components/home/PackagesSection";
import { OrderStatusSection } from "@/components/home/OrderStatusSection";
import { TrustSection } from "@/components/home/TrustSection";

/**
 * Public homepage — direction A «Тихий сад», per
 * `three wariants design/direction-a.dc.html` and `ui_kits/public-site`.
 *
 * Static: nothing here fetches yet, so there is no loading/empty/error state
 * to render. Package prices and status labels are design copy held in the
 * section components until the catalogue and orders APIs exist.
 */
export default function HomePage() {
  return (
    <>
      <SiteHeader />
      <main id="main" className="flex-1">
        <Hero />
        <PackagesSection />
        <OrderStatusSection />
        <TrustSection />
      </main>
      <SiteFooter />
    </>
  );
}
