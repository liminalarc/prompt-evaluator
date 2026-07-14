import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { OrganizationsApiService } from './organizations-api.service';

describe('OrganizationsApiService', () => {
  let service: OrganizationsApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(OrganizationsApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('lists organizations', () => {
    service.listOrganizations().subscribe();
    const req = httpMock.expectOne('/api/organizations');
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('creates an organization', () => {
    service.createOrganization('Acme').subscribe();
    const req = httpMock.expectOne('/api/organizations');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Acme' });
    req.flush({ id: 'o1', name: 'Acme' });
  });

  it('renames an organization', () => {
    service.renameOrganization('o1', 'Acme Inc').subscribe();
    const req = httpMock.expectOne('/api/organizations/o1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ name: 'Acme Inc' });
    req.flush({ id: 'o1', name: 'Acme Inc' });
  });
});
