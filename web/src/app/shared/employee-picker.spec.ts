import { TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MatSelectHarness } from '@angular/material/select/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { EmployeeDto } from '../api/models/employee-dto';
import { EmployeePicker } from './employee-picker';

const employees: EmployeeDto[] = [
  { id: 'e1', firstName: 'Anna', lastName: 'Nowak', hireDate: '2020-01-01', teamId: 't1', teamName: 'Platforma', userId: null },
  { id: 'e2', firstName: 'Jan', lastName: 'Kowalski', hireDate: '2021-01-01', teamId: 't2', teamName: 'Dane', userId: null },
];

describe('EmployeePicker', () => {
  function setup(selected: string | null, allLabel?: string) {
    TestBed.configureTestingModule({
      providers: [{ provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } }],
    });
    const fixture = TestBed.createComponent(EmployeePicker);
    fixture.componentRef.setInput('employees', employees);
    fixture.componentRef.setInput('selectedId', selected);
    if (allLabel !== undefined) fixture.componentRef.setInput('allLabel', allLabel);
    fixture.detectChanges();
    const loader = TestbedHarnessEnvironment.documentRootLoader(fixture);
    return { fixture, loader };
  }

  it('lists_employees_with_team_and_shows_current_selection', async () => {
    const { loader } = setup('e1');
    const select = await loader.getHarness(MatSelectHarness);
    expect(await select.getValueText()).toBe('Anna Nowak (Platforma)');
    await select.open();
    const options = await select.getOptions();
    expect(await Promise.all(options.map((o) => o.getText()))).toEqual([
      'Anna Nowak (Platforma)',
      'Jan Kowalski (Dane)',
    ]);
  });

  it('changing_selection_updates_selectedId', async () => {
    const { fixture, loader } = setup('e1');
    const select = await loader.getHarness(MatSelectHarness);
    await select.open();
    await select.clickOptions({ text: 'Jan Kowalski (Dane)' });
    expect(fixture.componentInstance.selectedId()).toBe('e2');
  });

  it('has_no_all_option_by_default', async () => {
    const { loader } = setup(null);
    const select = await loader.getHarness(MatSelectHarness);
    await select.open();
    expect((await select.getOptions()).length).toBe(2);
  });

  it('with_allLabel_shows_all_option_selected_for_null_and_sets_null_on_pick', async () => {
    const { fixture, loader } = setup(null, 'Wszyscy');
    const select = await loader.getHarness(MatSelectHarness);
    expect(await select.getValueText()).toBe('Wszyscy');
    await select.open();
    expect(await Promise.all((await select.getOptions()).map((o) => o.getText()))).toEqual([
      'Wszyscy',
      'Anna Nowak (Platforma)',
      'Jan Kowalski (Dane)',
    ]);
    await select.clickOptions({ text: 'Jan Kowalski (Dane)' });
    expect(fixture.componentInstance.selectedId()).toBe('e2');
    await select.open();
    await select.clickOptions({ text: 'Wszyscy' });
    expect(fixture.componentInstance.selectedId()).toBeNull();
  });
});
