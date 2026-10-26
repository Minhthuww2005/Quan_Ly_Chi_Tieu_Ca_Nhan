// Fired after any operation that changes the wallet, so the balance, history and product lists refresh
// in place instead of reloading the whole page.
export const WALLET_CHANGED = 'htdpay:wallet-changed';

export function notifyWalletChanged() {
  window.dispatchEvent(new Event(WALLET_CHANGED));
}
