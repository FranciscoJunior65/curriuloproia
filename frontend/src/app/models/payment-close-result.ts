export interface PaymentCloseResult {
  paid: boolean;
  credits?: number;
}

export function isPaymentCloseResult(value: unknown): value is PaymentCloseResult {
  return (
    typeof value === 'object' &&
    value !== null &&
    'paid' in value &&
    (value as PaymentCloseResult).paid === true
  );
}

export function isPaidCloseResult(value: unknown): boolean {
  return value === 'paid' || isPaymentCloseResult(value);
}

export function extractPaidCredits(value: unknown): number | undefined {
  if (isPaymentCloseResult(value) && typeof value.credits === 'number') {
    return value.credits;
  }
  return undefined;
}
