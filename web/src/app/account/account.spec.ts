import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { Account } from './account';

describe('Account (self-service change-password)', () => {
  let httpMock: HttpTestingController;

  function setup() {
    TestBed.configureTestingModule({
      imports: [Account],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const fixture = TestBed.createComponent(Account);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => httpMock.verify());

  it('posts current + new password and confirms on success', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      current: { set: (v: string) => void };
      next: { set: (v: string) => void };
      saved: () => boolean;
      submit: (e: Event) => void;
    };
    cmp.current.set('Old-Password-1');
    cmp.next.set('New-Password-1');
    cmp.submit(new Event('submit'));

    const req = httpMock.expectOne('/api/auth/change-password');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      currentPassword: 'Old-Password-1',
      newPassword: 'New-Password-1',
    });
    req.flush(null);
    expect(cmp.saved()).toBeTrue();
  });

  it('does not post when a field is empty', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      current: { set: (v: string) => void };
      submit: (e: Event) => void;
    };
    cmp.current.set('only-current');
    cmp.submit(new Event('submit'));
    httpMock.expectNone('/api/auth/change-password');
  });
});
