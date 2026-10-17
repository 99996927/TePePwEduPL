import {
  AUDIT_ENTITY_TYPES, auditEntityLabel, auditEventLabel, draftStatusLabel, messageRoleLabel, payrollChangeStatusLabel, payrollComponentLabel,
} from './labels';

describe('labels', () => {
  it.each([['Draft', 'Szkic'], ['Pending', 'Oczekuje na zatwierdzenie'], ['Approved', 'Zatwierdzona'], ['Rejected', 'Odrzucona'], ['X', 'X']])(
    'payrollChangeStatusLabel_%s', (k, v) => expect(payrollChangeStatusLabel(k)).toBe(v));

  it.each([['BaseSalary', 'Pensja zasadnicza'], ['FixedAllowance', 'Dodatek stały'], ['X', 'X']])(
    'payrollComponentLabel_%s', (k, v) => expect(payrollComponentLabel(k)).toBe(v));

  it.each([['Pending', 'Oczekuje'], ['Approved', 'Zatwierdzony'], ['Rejected', 'Odrzucony'], ['X', 'X']])(
    'draftStatusLabel_%s', (k, v) => expect(draftStatusLabel(k)).toBe(v));

  it.each([['Added', 'Dodano'], ['Modified', 'Zmieniono'], ['Deleted', 'Usunięto'], ['ConversationRead', 'Odczyt rozmowy'], ['X', 'X']])(
    'auditEventLabel_%s', (k, v) => expect(auditEventLabel(k)).toBe(v));

  it.each([
    ['Employee', 'Pracownik'], ['Team', 'Zespół'], ['LeaveRequest', 'Wniosek urlopowy'], ['WorkEntryRecord', 'Wpis czasu'],
    ['PayrollChange', 'Zmiana płacowa'], ['PayrollResultRecord', 'Naliczenie'], ['CorrectionDraft', 'Szkic korekty'],
    ['Conversation', 'Rozmowa'], ['X', 'X'],
  ])('auditEntityLabel_%s', (k, v) => expect(auditEntityLabel(k)).toBe(v));

  it.each([
    ['User', 'Ty'], ['Assistant', 'Agent'], ['ToolCall', 'Wywołanie narzędzia'], ['ToolResult', 'Wynik narzędzia'], ['X', 'X'],
  ])('messageRoleLabel_%s', (k, v) => expect(messageRoleLabel(k)).toBe(v));

  it('inherited_object_keys_are_returned_unchanged', () => {
    expect(draftStatusLabel('constructor')).toBe('constructor');
    expect(payrollChangeStatusLabel('toString')).toBe('toString');
    expect(auditEntityLabel('__proto__')).toBe('__proto__');
    expect(messageRoleLabel('constructor')).toBe('constructor');
    expect(messageRoleLabel('toString')).toBe('toString');
  });

  it('AUDIT_ENTITY_TYPES_has_eight_types_in_order', () => {
    expect(AUDIT_ENTITY_TYPES).toEqual([
      'Employee', 'Team', 'LeaveRequest', 'WorkEntryRecord', 'PayrollChange', 'PayrollResultRecord', 'CorrectionDraft', 'Conversation',
    ]);
  });
});
