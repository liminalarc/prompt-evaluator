import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { AuthService } from './auth.service';
import { AuthUser } from './user';

const user: AuthUser = {
  id: 'u1',
  email: 'ada@example.com',
  displayName: 'Ada',
  isAdmin: false,
};

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('starts unauthenticated', () => {
    expect(service.currentUser()).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
  });

  it('loadSession sets the current user on 200', async () => {
    const done = service.loadSession();
    const req = httpMock.expectOne('/api/auth/me');
    expect(req.request.method).toBe('GET');
    expect(req.request.withCredentials).toBeTrue();
    req.flush(user);
    await done;
    expect(service.currentUser()).toEqual(user);
    expect(service.isAuthenticated()).toBeTrue();
  });

  it('loadSession clears the user (settles, does not reject) on 401', async () => {
    const done = service.loadSession();
    httpMock.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    await expectAsync(done).toBeResolved();
    expect(service.currentUser()).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
  });

  it('ensureLoaded fetches /me only once', async () => {
    const first = service.ensureLoaded();
    httpMock.expectOne('/api/auth/me').flush(user);
    await first;
    await service.ensureLoaded(); // already loaded — no second request
    httpMock.verify(); // would throw if a second /me went out
    expect(service.isAuthenticated()).toBeTrue();
  });

  it('login posts credentials and sets the user', () => {
    service.login('ada@example.com', 'pw').subscribe();
    const req = httpMock.expectOne('/api/auth/login');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'ada@example.com', password: 'pw' });
    expect(req.request.withCredentials).toBeTrue();
    req.flush(user);
    expect(service.currentUser()).toEqual(user);
  });

  it('register posts the profile and sets the user', () => {
    service.register('ada@example.com', 'Ada', 'pw').subscribe();
    const req = httpMock.expectOne('/api/auth/register');
    expect(req.request.body).toEqual({
      email: 'ada@example.com',
      displayName: 'Ada',
      password: 'pw',
    });
    req.flush(user);
    expect(service.currentUser()).toEqual(user);
  });

  it('logout posts and clears the user', () => {
    service.login('ada@example.com', 'pw').subscribe();
    httpMock.expectOne('/api/auth/login').flush(user);
    expect(service.isAuthenticated()).toBeTrue();

    service.logout().subscribe();
    const req = httpMock.expectOne('/api/auth/logout');
    expect(req.request.method).toBe('POST');
    req.flush(null);
    expect(service.currentUser()).toBeNull();
  });

  it('forgotPassword posts the email', () => {
    service.forgotPassword('ada@example.com').subscribe();
    const req = httpMock.expectOne('/api/auth/forgot-password');
    expect(req.request.body).toEqual({ email: 'ada@example.com' });
    req.flush(null);
  });

  it('resetPassword posts email, token and new password', () => {
    service.resetPassword('ada@example.com', 'tok', 'newpw').subscribe();
    const req = httpMock.expectOne('/api/auth/reset-password');
    expect(req.request.body).toEqual({
      email: 'ada@example.com',
      token: 'tok',
      newPassword: 'newpw',
    });
    req.flush(null);
  });
});
