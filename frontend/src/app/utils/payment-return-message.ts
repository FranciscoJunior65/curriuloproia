export const PAYMENT_RETURN_READY_TYPE = 'curriculospro_payment_return_ready';
export const PAYMENT_RETURN_STATUS_TYPE = 'curriculospro_payment_return_status';

export type PaymentReturnProvider = 'kiwify' | 'cakto';
export type PaymentReturnStatus = 'confirming' | 'success' | 'pending';

export interface PaymentReturnReadyMessage {
  type: typeof PAYMENT_RETURN_READY_TYPE;
  provider?: PaymentReturnProvider;
}

export interface PaymentReturnStatusMessage {
  type: typeof PAYMENT_RETURN_STATUS_TYPE;
  status: PaymentReturnStatus;
  credits?: number;
}

export function isPaymentReturnReadyMessage(data: unknown): data is PaymentReturnReadyMessage {
  return (
    typeof data === 'object' &&
    data !== null &&
    (data as PaymentReturnReadyMessage).type === PAYMENT_RETURN_READY_TYPE
  );
}

export function isPaymentReturnStatusMessage(data: unknown): data is PaymentReturnStatusMessage {
  return (
    typeof data === 'object' &&
    data !== null &&
    (data as PaymentReturnStatusMessage).type === PAYMENT_RETURN_STATUS_TYPE
  );
}

export function buildPaymentReturnReadyMessage(
  provider?: PaymentReturnProvider
): PaymentReturnReadyMessage {
  return { type: PAYMENT_RETURN_READY_TYPE, provider };
}

export function buildPaymentReturnStatusMessage(
  status: PaymentReturnStatus,
  credits?: number
): PaymentReturnStatusMessage {
  return { type: PAYMENT_RETURN_STATUS_TYPE, status, credits };
}
