import { toMonthParam, localDay, workedHours, formatDate, formatHours, formatTime, monthRange, toDateOnly, toLocalDateTime } from './date-format';

describe('date-format', () => {
  it('toLocalDateTime_combines_local_day_and_time_without_zone', () => {
    expect(toLocalDateTime(new Date(2026, 9, 12), '08:05')).toBe('2026-10-12T08:05:00');
  });

  it('toDateOnly_uses_local_components', () => {
    expect(toDateOnly(new Date(2026, 0, 5))).toBe('2026-01-05');
  });

  it('monthRange_returns_first_and_last_day', () => {
    expect(monthRange(new Date(2026, 1, 1))).toEqual({ from: '2026-02-01', to: '2026-02-28' });
    expect(monthRange(new Date(2028, 1, 10))).toEqual({ from: '2028-02-01', to: '2028-02-29' });
  });

  it('formatHours_uses_comma_and_one_decimal', () => {
    expect(formatHours(8.5)).toBe('8,5');
    expect(formatHours(8)).toBe('8,0');
  });

  it('formatTime_returns_hh_mm', () => {
    expect(formatTime('2026-10-12T08:05:00')).toBe('08:05');
  });

  it('formatDate_uses_polish_locale', () => {
    expect(formatDate('2026-10-12')).toBe('12.10.2026');
  });

  it('localDay_builds_local_midnight_from_date_part', () => {
    const d = localDay('2026-10-25T23:59:00');
    expect([d.getFullYear(), d.getMonth(), d.getDate(), d.getHours()]).toEqual([2026, 9, 25, 0]);
  });

  it('workedHours_uses_wall_clock_difference_minus_break', () => {
    expect(workedHours('2026-10-25T00:00:00', '2026-10-25T08:00:00', 30)).toBe(7.5);
    expect(workedHours('2026-03-29T00:00:00', '2026-03-29T08:00:00', 0)).toBe(8);
    expect(workedHours('2026-10-05T22:00:00', '2026-10-06T06:00:00', 0)).toBe(8);
  });

  it('toMonthParam_returns_yyyy_mm', () => {
    expect(toMonthParam(new Date(2026, 8, 17))).toBe('2026-09');
    expect(toMonthParam(new Date(2026, 11, 1))).toBe('2026-12');
  });
});
