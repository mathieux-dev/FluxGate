import { DashboardLayout } from '@/components/shared/dashboard-layout';
import { merchantNavItems } from '@/components/shared/sidebar';

export default function MerchantLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return <DashboardLayout navItems={merchantNavItems}>{children}</DashboardLayout>;
}
