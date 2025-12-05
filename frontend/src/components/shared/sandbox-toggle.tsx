'use client';

import { useSandboxMode } from '@/lib/hooks/use-sandbox-mode';
import { Switch } from '@/components/ui/switch';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';

export function SandboxToggle() {
  const [mode, setMode] = useSandboxMode();

  return (
    <div className="flex items-center space-x-2">
      <Switch
        checked={mode === 'sandbox'}
        onCheckedChange={(checked) => setMode(checked ? 'sandbox' : 'production')}
        id="sandbox-mode"
      />
      <Label htmlFor="sandbox-mode" className="cursor-pointer">
        {mode === 'sandbox' ? 'Modo Sandbox' : 'Modo Produção'}
      </Label>
      {mode === 'sandbox' && (
        <Badge variant="secondary" className="bg-yellow-500 text-black">
          Teste
        </Badge>
      )}
    </div>
  );
}
