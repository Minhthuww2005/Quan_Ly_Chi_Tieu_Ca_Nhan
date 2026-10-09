// Contracts used by the integrated API and UI.
export type Tokens = { accessToken: string; refreshToken: string; accessTokenExpiresAt: string }
export type Context = { groups: { groupId: number; name: string }[]; wallets: { id: number; groupId: number; name: string }[]; categories: { id: number; walletId: number; name: string }[] }
export type Revenue = { id: number; walletId: number; categoryId: number | null; amount: number; occurredOn: string; note: string | null; rowVersion: number }
export type Report = { totalRevenue: number; breakdown: { key: string; label: string; revenue: number }[]; generatedAtUtc: string }
export type Rule = { id: number; walletId: number; categoryId: number | null; amount: number; note: string | null; frequency: string; intervalValue: number; startDate: string; endDate: string | null; nextRunOn: string | null; isActive: boolean; rowVersion: number }
export type Run = { id: number; occurrenceDate: string; status: string; error: string | null }
