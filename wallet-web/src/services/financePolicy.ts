import { useEffect, useState } from 'react';

// Product rules owned by the backend (FinancePolicy). The forms display these values instead of
// hard-coded rates, so the rate a user sees is the rate the server applies.

export interface LoanProduct {
  type: string;
  annualRate: number;
  minAmount: number;
  maxAmount: number;
  maxTermMonths: number;
}

export interface SavingsTier {
  fromTerm: number;
  toTerm: number;
  annualRate: number;
}

export interface FinancePolicy {
  loans: LoanProduct[];
  savingsTiers: SavingsTier[];
  savingsMinAmount: number;
}

let cached: Promise<FinancePolicy> | null = null;

async function load(): Promise<FinancePolicy> {
  const [loansRes, savingsRes] = await Promise.all([fetch('/api/loans/products'), fetch('/api/savings/products')]);
  const loans = await loansRes.json();
  const savings = await savingsRes.json();
  if (!loansRes.ok || !savingsRes.ok) throw new Error(loans.message || savings.message || 'Không tải được biểu lãi suất');
  return { loans: loans.products, savingsTiers: savings.tiers, savingsMinAmount: savings.minAmount };
}

export function fetchFinancePolicy(): Promise<FinancePolicy> {
  cached ??= load().catch((error) => {
    cached = null;
    throw error;
  });
  return cached;
}

export function useFinancePolicy() {
  const [policy, setPolicy] = useState<FinancePolicy | null>(null);
  const [error, setError] = useState('');
  useEffect(() => {
    let active = true;
    fetchFinancePolicy()
      .then((value) => active && setPolicy(value))
      .catch((e) => active && setError(e instanceof Error ? e.message : 'Không tải được biểu lãi suất'));
    return () => {
      active = false;
    };
  }, []);
  return { policy, error };
}

export function savingsRate(policy: FinancePolicy | null, term: number): number | null {
  return policy?.savingsTiers.find((t) => term >= t.fromTerm && term <= t.toTerm)?.annualRate ?? null;
}

export function loanProduct(policy: FinancePolicy | null, type: string): LoanProduct | null {
  return policy?.loans.find((p) => p.type === type) ?? null;
}
