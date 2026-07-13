import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { FoldersApiService } from './folders-api.service';

describe('FoldersApiService', () => {
  let service: FoldersApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(FoldersApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('lists the folder tree', () => {
    service.listFolders().subscribe();
    const req = httpMock.expectOne('/api/folders');
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('creates a folder with a parent', () => {
    service.createFolder('Summarization', 'root-id').subscribe();
    const req = httpMock.expectOne('/api/folders');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Summarization', parentId: 'root-id' });
    req.flush({ id: 'x', parentId: 'root-id', name: 'Summarization' });
  });

  it('renames a folder', () => {
    service.renameFolder('abc', 'New').subscribe();
    const req = httpMock.expectOne('/api/folders/abc');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ name: 'New' });
    req.flush({ id: 'abc', parentId: null, name: 'New' });
  });

  it('moves a folder under a new parent', () => {
    service.moveFolder('abc', 'new-parent').subscribe();
    const req = httpMock.expectOne('/api/folders/abc/move');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ parentId: 'new-parent' });
    req.flush({ id: 'abc', parentId: 'new-parent', name: 'x' });
  });

  it('lists the prompts in a folder', () => {
    service.listFolderPrompts('abc').subscribe();
    const req = httpMock.expectOne('/api/folders/abc/prompts');
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('lists the unfiled (root) prompts', () => {
    service.listRootPrompts().subscribe();
    const req = httpMock.expectOne('/api/folders/root/prompts');
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });
});
