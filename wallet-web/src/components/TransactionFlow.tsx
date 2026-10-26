import { useEffect, useRef, useState, type ReactNode } from 'react';
import { AlertCircle, CheckCircle } from 'lucide-react';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from './ui/dialog';
import { Button } from './ui/button';
import { Input } from './ui/input';
import { Label } from './ui/label';
import { notifyWalletChanged } from '../services/walletEvents';

export interface TransactionRequest {
  title: string;
  rows: { label: string; value: string; highlight?: boolean }[];
  actionLabel: string;
  processingText: string;
  successText: string;
  // Throws an Error with the server message when the operation is refused.
  run: (pin: string) => Promise<void>;
  onDone?: () => void;
}

type Step = 'confirm' | 'pin' | 'processing' | 'success' | 'error';

// The same steps as phone top-up and transfers: review -> transaction PIN -> processing -> result.
// `view` is the step content for a dialog that already exists; `dialog` wraps it in its own dialog.
export function useTransactionFlow() {
  const [request, setRequest] = useState<TransactionRequest | null>(null);
  const [step, setStep] = useState<Step>('confirm');
  const [pin, setPin] = useState('');
  const [error, setError] = useState('');
  const timer = useRef<number | undefined>(undefined);

  useEffect(() => () => window.clearTimeout(timer.current), []);

  const reset = () => {
    window.clearTimeout(timer.current);
    setRequest(null);
    setStep('confirm');
    setPin('');
    setError('');
  };

  const start = (next: TransactionRequest) => {
    window.clearTimeout(timer.current);
    setRequest(next);
    setStep('confirm');
    setPin('');
    setError('');
  };

  const submit = async () => {
    if (!request) return;
    if (pin.length !== 6) {
      setError('Mã PIN phải có 6 số');
      return;
    }
    setError('');
    setStep('processing');
    try {
      await request.run(pin);
      notifyWalletChanged();
      setStep('success');
      const done = request.onDone;
      timer.current = window.setTimeout(() => {
        reset();
        done?.();
      }, 2000);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Có lỗi xảy ra');
      setPin('');
      setStep('error');
    }
  };

  const view: ReactNode = request && (
    <>
      {step === 'confirm' && (
        <div className="space-y-4">
          <div className="bg-gray-50 rounded-lg p-4 space-y-3">
            {request.rows.map((row) => (
              <div key={row.label} className="flex justify-between gap-4">
                <span className="text-gray-600">{row.label}:</span>
                <span className={`text-right ${row.highlight ? 'font-semibold text-blue-600' : 'font-medium'}`}>{row.value}</span>
              </div>
            ))}
          </div>
          <div className="flex space-x-3">
            <Button variant="outline" className="flex-1" onClick={reset}>Quay lại</Button>
            <Button className="flex-1" onClick={() => setStep('pin')}>Xác nhận</Button>
          </div>
        </div>
      )}

      {step === 'pin' && (
        <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); submit(); }}>
          <div>
            <Label htmlFor="transaction-pin">Nhập mã PIN</Label>
            <Input
              id="transaction-pin"
              type="password"
              inputMode="numeric"
              autoComplete="off"
              autoFocus
              placeholder="******"
              value={pin}
              onChange={(e) => setPin(e.target.value.replace(/\D/g, '').slice(0, 6))}
              maxLength={6}
              className="text-center text-2xl tracking-widest"
            />
          </div>
          {error && (
            <div className="text-sm text-red-600 bg-red-50 p-3 rounded-lg flex items-center space-x-2">
              <AlertCircle className="w-4 h-4" />
              <span>{error}</span>
            </div>
          )}
          <div className="flex space-x-3">
            <Button type="button" variant="outline" className="flex-1" onClick={() => { setError(''); setStep('confirm'); }}>Quay lại</Button>
            <Button type="submit" className="flex-1">{request.actionLabel}</Button>
          </div>
        </form>
      )}

      {step === 'processing' && (
        <div className="py-8 text-center">
          <div className="w-16 h-16 border-4 border-blue-600 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
          <div className="text-lg font-medium">{request.processingText}</div>
        </div>
      )}

      {step === 'success' && (
        <div className="py-8 text-center">
          <CheckCircle className="w-16 h-16 text-green-600 mx-auto mb-4" />
          <div className="text-xl font-semibold">{request.successText}</div>
        </div>
      )}

      {step === 'error' && (
        <div className="space-y-4">
          <div className="py-8 text-center">
            <AlertCircle className="w-16 h-16 text-red-600 mx-auto mb-4" />
            <div className="text-xl font-semibold mb-2">Giao dịch thất bại</div>
            <div className="text-red-600">{error}</div>
          </div>
          <Button className="w-full" onClick={() => { setError(''); setStep('pin'); }}>Thử lại</Button>
        </div>
      )}
    </>
  );

  const dialog = (
    <Dialog open={request !== null} onOpenChange={(open) => { if (!open && step !== 'processing') reset(); }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{request?.title}</DialogTitle>
        </DialogHeader>
        {view}
      </DialogContent>
    </Dialog>
  );

  return { active: request !== null, step, start, reset, view, dialog };
}
