'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn } from '@/lib/utils';
import {
  LayoutDashboard,
  CreditCard,
  Webhook,
  Key,
  Link2,
  Repeat,
  Settings,
  TestTube,
  Users,
  BarChart3,
  FileText,
  Shield,
} from 'lucide-react';

export interface NavItem {
  href: string;
  label: string;
  icon: React.ReactNode;
}

interface SidebarProps {
  items: NavItem[];
}

export function Sidebar({ items }: SidebarProps) {
  const pathname = usePathname();

  return (
    <aside className="w-64 bg-palmeiras-green text-white min-h-screen">
      <div className="p-6">
        <h1 className="text-2xl font-bold">FluxPay</h1>
      </div>
      <nav className="px-4 space-y-1">
        {items.map((item) => (
          <Link
            key={item.href}
            href={item.href}
            className={cn(
              'flex items-center space-x-3 px-4 py-3 rounded-lg transition-colors',
              pathname === item.href
                ? 'bg-palmeiras-green-light'
                : 'hover:bg-palmeiras-green-light/50'
            )}
          >
            {item.icon}
            <span>{item.label}</span>
          </Link>
        ))}
      </nav>
    </aside>
  );
}

export const merchantNavItems: NavItem[] = [
  {
    href: '/dashboard',
    label: 'Dashboard',
    icon: <LayoutDashboard className="w-5 h-5" />,
  },
  {
    href: '/transactions',
    label: 'Transações',
    icon: <CreditCard className="w-5 h-5" />,
  },
  {
    href: '/sandbox',
    label: 'Sandbox',
    icon: <TestTube className="w-5 h-5" />,
  },
  {
    href: '/webhooks',
    label: 'Webhooks',
    icon: <Webhook className="w-5 h-5" />,
  },
  {
    href: '/api-keys',
    label: 'API Keys',
    icon: <Key className="w-5 h-5" />,
  },
  {
    href: '/payment-links',
    label: 'Links de Pagamento',
    icon: <Link2 className="w-5 h-5" />,
  },
  {
    href: '/subscriptions',
    label: 'Assinaturas',
    icon: <Repeat className="w-5 h-5" />,
  },
  {
    href: '/settings',
    label: 'Configurações',
    icon: <Settings className="w-5 h-5" />,
  },
];

export const adminNavItems: NavItem[] = [
  {
    href: '/admin/dashboard',
    label: 'Dashboard',
    icon: <LayoutDashboard className="w-5 h-5" />,
  },
  {
    href: '/admin/merchants',
    label: 'Merchants',
    icon: <Users className="w-5 h-5" />,
  },
  {
    href: '/admin/analytics',
    label: 'Analytics',
    icon: <BarChart3 className="w-5 h-5" />,
  },
  {
    href: '/admin/reconciliation',
    label: 'Reconciliação',
    icon: <FileText className="w-5 h-5" />,
  },
  {
    href: '/admin/audit-logs',
    label: 'Audit Logs',
    icon: <Shield className="w-5 h-5" />,
  },
];
