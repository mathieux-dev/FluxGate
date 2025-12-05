'use client';

import { useSandboxMode } from '@/lib/hooks/use-sandbox-mode';

export function ModeIndicator() {
  const [mode] = useSandboxMode();

  if (mode === 'production') return null;

  return (
    <div className="fixed top-0 left-0 right-0 bg-yellow-500 text-black text-center py-2 z-50 font-semibold">
      MODO SANDBOX - Transações de teste
    </div>
  );
}
