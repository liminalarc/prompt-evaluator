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
    expect(compiled.querySelector('h1')?.textContent).toContain('Prompt Evaluator');
  });

  it('posts the prompt and shows the echoed output', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[data-testid="run"]') as HTMLButtonElement).click();

    const req = httpMock.expectOne('/api/eval-runs');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ prompt: 'Hello, Prompt Evaluator' });
    req.flush({
      id: '11111111-1111-1111-1111-111111111111',
      prompt: 'Hello, Prompt Evaluator',
      output: 'Hello, Prompt Evaluator',
      createdAt: '2026-07-11T12:00:00Z',
    });
    fixture.detectChanges();

    const result = fixture.nativeElement.querySelector('[data-testid="result"]') as HTMLElement;
    expect(result.textContent).toContain('Hello, Prompt Evaluator');
  });

  it('shows an error when the round trip fails', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[data-testid="run"]') as HTMLButtonElement).click();
    httpMock.expectOne('/api/eval-runs').flush('boom', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    const error = fixture.nativeElement.querySelector('[data-testid="error"]') as HTMLElement;
    expect(error).toBeTruthy();
  });
});
