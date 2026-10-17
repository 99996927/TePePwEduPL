import { formatMoney } from './format';

describe('format', () => {
  const norm = (s: string) => s.replace(/[  ]/g, ' ');

  it('formatMoney_uses_polish_currency_format', () => {
    expect(norm(formatMoney(1234.5))).toBe('1 234,50 zł');
    expect(norm(formatMoney(0))).toBe('0,00 zł');
  });
});
