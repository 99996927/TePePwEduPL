import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatIconModule } from '@angular/material/icon';
import { provideIconFont } from './icon-font';

@Component({ imports: [MatIconModule], template: '<mat-icon fontIcon="menu" />' })
class Host {}

describe('provideIconFont', () => {
  it('renders_icons_as_ligatures_of_material_symbols', async () => {
    TestBed.configureTestingModule({ providers: [provideIconFont()] });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const icon = (fixture.nativeElement as HTMLElement).querySelector('mat-icon')!;

    expect(icon.classList).toContain('material-symbols-outlined');
    expect(icon.classList).toContain('mat-ligature-font');
    expect(icon.classList).not.toContain('menu');
    expect(icon.textContent).toBe('');
  });
});
