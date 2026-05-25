/** Máscara percentual BR: digitação vira 0,00 até 100,00 */
export function maskPercentInput(raw: string): { text: string; value: number } {
  let digits = raw.replace(/\D/g, '').slice(0, 5);
  if (!digits) {
    return { text: '', value: 0 };
  }

  let value = parseInt(digits, 10) / 100;
  if (value > 100) {
    value = 100;
  }

  const text = value.toFixed(2).replace('.', ',');
  return { text, value: Math.round(value * 100) / 100 };
}

export function formatPercentDisplay(value: number): string {
  const safe = Number.isFinite(value) ? Math.min(100, Math.max(0, value)) : 0;
  return safe.toFixed(2).replace('.', ',');
}
