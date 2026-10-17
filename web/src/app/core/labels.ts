const lookup = (map: Readonly<Record<string, string>>) => (key: string): string =>
  (Object.hasOwn(map, key) ? map[key] : key);

export const payrollChangeStatusLabel = lookup({
  Draft: 'Szkic',
  Pending: 'Oczekuje na zatwierdzenie',
  Approved: 'Zatwierdzona',
  Rejected: 'Odrzucona',
});

export const payrollComponentLabel = lookup({
  BaseSalary: 'Pensja zasadnicza',
  FixedAllowance: 'Dodatek stały',
});

export const draftStatusLabel = lookup({
  Pending: 'Oczekuje',
  Approved: 'Zatwierdzony',
  Rejected: 'Odrzucony',
});

export const auditEventLabel = lookup({
  Added: 'Dodano',
  Modified: 'Zmieniono',
  Deleted: 'Usunięto',
  ConversationRead: 'Odczyt rozmowy',
});

export const messageRoleLabel = lookup({
  User: 'Ty',
  Assistant: 'Agent',
  ToolCall: 'Wywołanie narzędzia',
  ToolResult: 'Wynik narzędzia',
});

const AUDIT_ENTITIES: Readonly<Record<string, string>> = {
  Employee: 'Pracownik',
  Team: 'Zespół',
  LeaveRequest: 'Wniosek urlopowy',
  WorkEntryRecord: 'Wpis czasu',
  PayrollChange: 'Zmiana płacowa',
  PayrollResultRecord: 'Naliczenie',
  CorrectionDraft: 'Szkic korekty',
  Conversation: 'Rozmowa',
};

export const auditEntityLabel = lookup(AUDIT_ENTITIES);

export const AUDIT_ENTITY_TYPES: readonly string[] = Object.keys(AUDIT_ENTITIES);
