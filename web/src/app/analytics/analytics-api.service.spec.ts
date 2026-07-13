import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AnalyticsApiService } from './analytics-api.service';

describe('AnalyticsApiService', () => {
  let service: AnalyticsApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AnalyticsApiService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AnalyticsApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('requests trends with promptId + datasetId params', () => {
    service.getTrends('p1', 'd1').subscribe();
    const req = http.expectOne(
      (r) => r.url === '/api/analytics/trends' && r.params.get('promptId') === 'p1',
    );
    expect(req.request.params.get('datasetId')).toBe('d1');
    req.flush([]);
  });

  it('includes the threshold on regressions when provided', () => {
    service.getRegressions('p1', 'd1', 0.1).subscribe();
    const req = http.expectOne((r) => r.url === '/api/analytics/regressions');
    expect(req.request.params.get('threshold')).toBe('0.1');
    req.flush([]);
  });

  it('omits the threshold when not provided', () => {
    service.getRegressions('p1', 'd1').subscribe();
    const req = http.expectOne((r) => r.url === '/api/analytics/regressions');
    expect(req.request.params.has('threshold')).toBeFalse();
    req.flush([]);
  });

  it('requests comparison with from/to version params', () => {
    service.getComparison('p1', 'd1', 'v1', 'v2').subscribe();
    const req = http.expectOne((r) => r.url === '/api/analytics/comparison');
    expect(req.request.params.get('fromVersionId')).toBe('v1');
    expect(req.request.params.get('toVersionId')).toBe('v2');
    req.flush({});
  });
});
