import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App', () => {
  it('App_creates_and_renders_router_outlet', async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])],
    }).compileComponents();

    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(fixture.componentInstance).toBeTruthy();
    expect(element.querySelector('router-outlet')).not.toBeNull();
  });
});
