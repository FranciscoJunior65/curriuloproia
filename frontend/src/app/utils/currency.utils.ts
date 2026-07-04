/** Máscara monetária BR: digitação vira 0,00 (centavos automáticos). */
export function maskBrlInput(raw: string): { text: string; value: number } {
  const digits = raw.replace(/\D/g, '').slice(0, 9);
  if (!digits) {
    return { text: '', value: 0 };
  }

  const value = parseInt(digits, 10) / 100;
  const text = value.toFixed(2).replace('.', ',');
  return { text, value: Math.round(value * 100) / 100 };
}

export function formatBrlDisplay(value: number): string {
  const safe = Number.isFinite(value) ? Math.max(0, value) : 0;
  return safe.toFixed(2).replace('.', ',');
}
