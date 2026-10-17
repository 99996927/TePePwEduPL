import { EnvironmentProviders, inject, provideAppInitializer } from '@angular/core';
import { MatIconRegistry } from '@angular/material/icon';

// Załadowany font to Material Symbols; `mat-ligature-font` każe `mat-icon` rysować ligaturę z atrybutu `fontIcon`.
export function provideIconFont(): EnvironmentProviders {
  return provideAppInitializer(() => {
    inject(MatIconRegistry).setDefaultFontSetClass('material-symbols-outlined', 'mat-ligature-font');
  });
}
