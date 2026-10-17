const money = new Intl.NumberFormat('pl-PL', { style: 'currency', currency: 'PLN', useGrouping: true });

export function formatMoney(n: number): string {
  return money.format(n);
}
