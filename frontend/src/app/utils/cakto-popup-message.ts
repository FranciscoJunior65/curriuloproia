export const CAKTO_POPUP_MESSAGE_TYPE = 'curriculospro_cakto_paid';

export interface CaktoPopupPaidMessage {
  type: typeof CAKTO_POPUP_MESSAGE_TYPE;
  credits?: number;
}

export function isCaktoPopupPaidMessage(data: unknown): data is CaktoPopupPaidMessage {
  return (
    typeof data === 'object' &&
    data !== null &&
    (data as CaktoPopupPaidMessage).type === CAKTO_POPUP_MESSAGE_TYPE
  );
}
