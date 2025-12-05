'use client';

import { Sidebar, NavItem } from './sidebar';
import { Header } from './header';
import { ModeIndicator } from './mode-indicator';
import { MobileSidebar } from './mobile-sidebar';

interface DashboardLayoutProps {
  children: React.ReactNode;
  navItems: NavItem[];
}

export function DashboardLayout({ children, navItems }: DashboardLayoutProps) {
  return (
    <div className="flex min-h-screen">
      <ModeIndicator />
      
      <div className="hidden lg:block">
        <Sidebar items={navItems} />
      </div>

      <div className="flex-1 flex flex-col">
        <div className="lg:hidden border-b p-4 flex items-center justify-between">
          <h1 className="text-xl font-bold text-palmeiras-green">FluxPay</h1>
          <MobileSidebar items={navItems} />
        </div>

        <Header />

        <main className="flex-1 p-6 bg-muted/30">{children}</main>
      </div>
    </div>
  );
}
