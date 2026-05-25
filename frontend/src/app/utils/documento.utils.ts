/** Apenas dígitos do documento (CPF 11 ou CNPJ 14). */
export function getDocumentDigits(value: string): string {
  return (value || '').replace(/\D/g, '').slice(0, 14);
}

/** Formata CPF ou CNPJ conforme quantidade de dígitos digitados. */
export function formatCpfCnpjDisplay(value: string): string {
  const digits = getDocumentDigits(value);
  if (digits.length <= 11) {
    if (digits.length <= 3) return digits;
    if (digits.length <= 6) return `${digits.slice(0, 3)}.${digits.slice(3)}`;
    if (digits.length <= 9) {
      return `${digits.slice(0, 3)}.${digits.slice(3, 6)}.${digits.slice(6)}`;
    }
    return `${digits.slice(0, 3)}.${digits.slice(3, 6)}.${digits.slice(6, 9)}-${digits.slice(9)}`;
  }

  if (digits.length <= 12) {
    return `${digits.slice(0, 2)}.${digits.slice(2, 5)}.${digits.slice(5, 8)}/${digits.slice(8)}`;
  }
  return `${digits.slice(0, 2)}.${digits.slice(2, 5)}.${digits.slice(5, 8)}/${digits.slice(8, 12)}-${digits.slice(12)}`;
}

export function isValidPartnerDocument(digits: string): boolean {
  return digits.length === 11 || digits.length === 14;
}

export function partnerDocumentLabel(digits?: string): string {
  const d = getDocumentDigits(digits || '');
  if (d.length === 14) return 'CNPJ';
  if (d.length === 11) return 'CPF';
  return 'CPF/CNPJ';
}
