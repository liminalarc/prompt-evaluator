import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let auth: AuthService;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate');
    spyOn(auth, 'clearSession').and.callThrough();
  });

  afterEach(() => httpMock.verify());

  it('adds withCredentials to every request', () => {
    http.get('/api/prompts').subscribe();
    const req = httpMock.expectOne('/api/prompts');
    expect(req.request.withCredentials).toBeTrue();
    req.flush([]);
  });

  it('on a 401 from a non-auth endpoint, clears the session and redirects to /login', () => {
    http.get('/api/prompts').subscribe({ error: () => {} });
    httpMock.expectOne('/api/prompts').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(auth.clearSession).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('does NOT redirect on a 401 from an auth endpoint (e.g. bad login / unauthenticated /me)', () => {
    http.get('/api/auth/me').subscribe({ error: () => {} });
    httpMock.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(auth.clearSession).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('passes through non-401 errors without redirecting', () => {
    http.get('/api/prompts').subscribe({ error: () => {} });
    httpMock.expectOne('/api/prompts').flush(null, { status: 500, statusText: 'Server Error' });
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
