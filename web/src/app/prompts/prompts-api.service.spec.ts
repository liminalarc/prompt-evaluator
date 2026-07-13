import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { PromptsApiService } from './prompts-api.service';

describe('PromptsApiService', () => {
  let service: PromptsApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(PromptsApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('lists prompts', () => {
    service.listPrompts().subscribe();
    const req = httpMock.expectOne('/api/prompts');
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('gets one prompt by id', () => {
    service.getPrompt('abc').subscribe();
    const req = httpMock.expectOne('/api/prompts/abc');
    expect(req.request.method).toBe('GET');
    req.flush({ id: 'abc', name: 'x', description: null, versions: [] });
  });

  it('creates a prompt', () => {
    service.createPrompt('Summarizer', 'desc').subscribe();
    const req = httpMock.expectOne('/api/prompts');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Summarizer', description: 'desc' });
    req.flush({ id: 'abc', name: 'Summarizer', description: 'desc', versions: [] });
  });

  it('moves a prompt into a folder', () => {
    service.movePrompt('abc', 'folder-1').subscribe();
    const req = httpMock.expectOne('/api/prompts/abc/move');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ folderId: 'folder-1' });
    req.flush({ id: 'abc', folderId: 'folder-1', name: 'x', description: null, versions: [] });
  });

  it('unfiles a prompt to the root', () => {
    service.movePrompt('abc', null).subscribe();
    const req = httpMock.expectOne('/api/prompts/abc/move');
    expect(req.request.body).toEqual({ folderId: null });
    req.flush({ id: 'abc', folderId: null, name: 'x', description: null, versions: [] });
  });

  it('adds a version', () => {
    const body = {
      content: 'Summarize: {input}',
      targetModel: 'claude-sonnet-5',
      label: 'baseline',
      sourceApp: 'Stormboard',
    };
    service.addVersion('abc', body).subscribe();
    const req = httpMock.expectOne('/api/prompts/abc/versions');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({ id: 'abc', name: 'x', description: null, versions: [] });
  });
});
