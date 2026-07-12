import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { Home } from './home';

describe('Home', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Home],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('renders the heading', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('LitmusAI');
  });

  it('posts the prompt and shows the echoed output', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[data-testid="run"]') as HTMLButtonElement).click();

    const req = httpMock.expectOne('/api/echo');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ prompt: 'Hello, LitmusAI' });
    req.flush({ output: 'Hello, LitmusAI' });
    fixture.detectChanges();

    const result = fixture.nativeElement.querySelector('[data-testid="result"]') as HTMLElement;
    expect(result.textContent).toContain('Hello, LitmusAI');
  });

  it('shows an error when the round trip fails', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[data-testid="run"]') as HTMLButtonElement).click();
    httpMock.expectOne('/api/echo').flush('boom', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    const error = fixture.nativeElement.querySelector('[data-testid="error"]') as HTMLElement;
    expect(error).toBeTruthy();
  });
});
