'use client';

import { useState } from 'react';
import { Menu, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Sidebar, NavItem } from './sidebar';
import { cn } from '@/lib/utils';

interface MobileSidebarProps {
  items: NavItem[];
}

export function MobileSidebar({ items }: MobileSidebarProps) {
  const [isOpen, setIsOpen] = useState(false);

  return (
    <>
      <Button
        variant="ghost"
        size="icon"
        className="lg:hidden"
        onClick={() => setIsOpen(!isOpen)}
      >
        {isOpen ? <X className="h-6 w-6" /> : <Menu className="h-6 w-6" />}
      </Button>

      <div
        className={cn(
          'fixed inset-0 z-40 lg:hidden transition-opacity',
          isOpen
            ? 'opacity-100 pointer-events-auto'
            : 'opacity-0 pointer-events-none'
        )}
      >
        <div
          className="absolute inset-0 bg-black/50"
          onClick={() => setIsOpen(false)}
        />
        <div
          className={cn(
            'absolute left-0 top-0 bottom-0 transition-transform',
            isOpen ? 'translate-x-0' : '-translate-x-full'
          )}
        >
          <Sidebar items={items} />
        </div>
      </div>
    </>
  );
}
