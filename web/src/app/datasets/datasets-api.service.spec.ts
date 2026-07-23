import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { DatasetsApiService } from './datasets-api.service';

describe('DatasetsApiService', () => {
  let service: DatasetsApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(DatasetsApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('lists datasets', () => {
    service.listDatasets().subscribe();
    const req = httpMock.expectOne('/api/datasets');
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('gets one dataset by id', () => {
    service.getDataset('abc').subscribe();
    const req = httpMock.expectOne('/api/datasets/abc');
    expect(req.request.method).toBe('GET');
    req.flush({ id: 'abc', name: 'x', description: null, fixtures: [] });
  });

  it('lists the datasets belonging to a prompt', () => {
    service.listDatasetsByPrompt('p1').subscribe();
    const req = httpMock.expectOne('/api/prompts/p1/datasets');
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('creates a dataset under its owning prompt', () => {
    service.createDataset('p1', 'Summaries', 'desc').subscribe();
    const req = httpMock.expectOne('/api/prompts/p1/datasets');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Summaries', description: 'desc' });
    req.flush({ id: 'abc', promptId: 'p1', name: 'Summaries', description: 'desc', fixtures: [] });
  });

  it('captures fixtures with the capture-schema body', () => {
    const tuples = [
      { promptInput: 'summarize', input: null, slmOutput: 'raw', downstreamResult: null },
    ];
    service.captureFixtures('abc', tuples).subscribe();
    const req = httpMock.expectOne('/api/datasets/abc/fixtures/capture');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ tuples });
    req.flush({ id: 'abc', name: 'x', description: null, fixtures: [] });
  });

  it('triggers generation with guidance and count', () => {
    const guidance = { coverageGoals: 'cover more', edgeCases: null, constraints: null };
    service.generateFixtures('abc', guidance, 3).subscribe();
    const req = httpMock.expectOne('/api/datasets/abc/fixtures/generate');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ guidance, count: 3 });
    req.flush({ id: 'abc', name: 'x', description: null, fixtures: [] });
  });

  it('deletes a dataset', () => {
    service.deleteDataset('abc').subscribe();
    const req = httpMock.expectOne('/api/datasets/abc');
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('deletes a single fixture and returns the updated dataset [U19]', () => {
    service.deleteFixture('abc', 'f2').subscribe();
    const req = httpMock.expectOne('/api/datasets/abc/fixtures/f2');
    expect(req.request.method).toBe('DELETE');
    req.flush({ id: 'abc', name: 'x', description: null, fixtures: [] });
  });
});
