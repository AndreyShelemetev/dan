import { SiteHeader } from "@/components/site/SiteHeader";
import { listServicePackages } from "@/lib/api/catalog";
import { SiteFooter } from "@/components/site/SiteFooter";
import { Hero } from "@/components/home/Hero";
import { PackagesSection } from "@/components/home/PackagesSection";
import { OrderStatusSection } from "@/components/home/OrderStatusSection";
import { TrustSection } from "@/components/home/TrustSection";

export const dynamic = "force-dynamic";

/**
 * Public homepage — direction A «Тихий сад», per
 * `three wariants design/direction-a.dc.html` and `ui_kits/public-site`.
 *
 * Packages now come from the catalogue. A failed fetch degrades to hiding that one section
 * rather than failing the page: the homepage is also the entry point for someone who already
 * has an account, and losing it because reference data hiccuped would be the worse trade.
 *
 * The order-status stepper is still design copy — orders (V3) do not exist yet.
 */
export default async function HomePage() {
  const packages = await listServicePackages().catch(() => []);

  return (
    <>
      <SiteHeader />
      <main id="main" className="flex-1">
        <Hero />
        <PackagesSection packages={packages} />
        <OrderStatusSection />
        <TrustSection />
      </main>
      <SiteFooter />
    </>
  );
}
