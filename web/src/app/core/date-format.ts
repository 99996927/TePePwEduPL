const pad = (n: number): string => String(n).padStart(2, '0');

/** `yyyy-MM-dd` from local date components (never via toISOString). */
export function toDateOnly(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** Local date-time without zone: `yyyy-MM-ddTHH:mm:00` (ADR-17). `time` is `HH:mm`. */
export function toLocalDateTime(day: Date, time: string): string {
  return `${toDateOnly(day)}T${time}:00`;
}

export function monthRange(first: Date): { from: string; to: string } {
  const start = new Date(first.getFullYear(), first.getMonth(), 1);
  const end = new Date(first.getFullYear(), first.getMonth() + 1, 0);
  return { from: toDateOnly(start), to: toDateOnly(end) };
}

export function formatDate(s: string): string {
  const [y, m, d] = s.slice(0, 10).split('-').map(Number);
  return new Date(y, m - 1, d).toLocaleDateString('pl-PL', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

export function formatTime(s: string): string {
  return s.slice(11, 16);
}

export function formatHours(n: number): string {
  return n.toLocaleString('pl-PL', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
}
