import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { VersionService, VersionInfo } from './version.service';

const info = (over: Partial<VersionInfo> = {}): VersionInfo => ({
  version: '1.2.0',
  commit: 'abc1234def',
  buildTime: '2026-07-15T12:00:00Z',
  environment: 'Production',
  channel: 'dev',
  ...over,
});

describe('VersionService', () => {
  let service: VersionService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [VersionService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(VersionService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('starts with no version info and no indicators', () => {
    expect(service.info()).toBeNull();
    expect(service.buildLabel()).toBeNull();
    expect(service.envBadge()).toBeNull();
  });

  it('load() fetches GET /api/version and populates the signal', async () => {
    const load = service.load();
    http.expectOne('/api/version').flush(info());
    await load;
    expect(service.info()?.version).toBe('1.2.0');
  });

  it('load() swallows errors and leaves info null (indicators just do not render)', async () => {
    const load = service.load();
    http.expectOne('/api/version').flush('nope', { status: 500, statusText: 'Server Error' });
    await load;
    expect(service.info()).toBeNull();
    expect(service.buildLabel()).toBeNull();
    expect(service.envBadge()).toBeNull();
  });

  describe('prod channel', () => {
    beforeEach(async () => {
      const load = service.load();
      http.expectOne('/api/version').flush(info({ channel: 'prod' }));
      await load;
    });
    it('footer chip shows v<version> · <sha7>', () => {
      expect(service.buildLabel()).toBe('v1.2.0 · abc1234');
    });
    it('shows NO env badge in prod', () => {
      expect(service.envBadge()).toBeNull();
    });
  });

  describe('dev channel', () => {
    beforeEach(async () => {
      const load = service.load();
      http.expectOne('/api/version').flush(info({ channel: 'dev' }));
      await load;
    });
    it('footer chip shows dev · <sha7> (channel, not a semver)', () => {
      expect(service.buildLabel()).toBe('dev · abc1234');
    });
    it('env badge shows DEV — channel wins over environment="Production"', () => {
      expect(service.envBadge()).toBe('DEV');
    });
  });

  describe('local channel', () => {
    beforeEach(async () => {
      const load = service.load();
      // Local build: commit is the "dev" default (no real sha).
      http.expectOne('/api/version').flush(info({ channel: 'local', commit: 'dev' }));
      await load;
    });
    it('footer chip shows bare "local" when there is no real commit', () => {
      expect(service.buildLabel()).toBe('local');
    });
    it('env badge shows LOCAL', () => {
      expect(service.envBadge()).toBe('LOCAL');
    });
  });

  it('tooltip carries the full channel · version · commit · buildTime detail', async () => {
    const load = service.load();
    http.expectOne('/api/version').flush(info({ channel: 'prod' }));
    await load;
    expect(service.buildTooltip()).toBe('prod · v1.2.0 · abc1234def · built 2026-07-15T12:00:00Z');
  });
});
