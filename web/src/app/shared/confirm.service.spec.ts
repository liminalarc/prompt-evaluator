import { TestBed } from '@angular/core/testing';
import { ConfirmService } from './confirm.service';

describe('ConfirmService', () => {
  let service: ConfirmService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ConfirmService);
  });

  it('exposes the pending request while asking, then clears it', async () => {
    expect(service.request()).toBeNull();
    const pending = service.ask({ title: 'Delete', message: 'Cascades everything.' });
    expect(service.request()?.message).toContain('Cascades everything.');

    service.confirm();
    expect(service.request()).toBeNull();
    expect(await pending).toBe(true);
  });

  it('resolves false when cancelled', async () => {
    const pending = service.ask({ title: 'Delete', message: 'x' });
    service.cancel();
    expect(await pending).toBe(false);
    expect(service.request()).toBeNull();
  });

  it('cancels an earlier open request when a new one is asked', async () => {
    const first = service.ask({ title: 'A', message: 'a' });
    const second = service.ask({ title: 'B', message: 'b' });
    expect(await first).toBe(false); // superseded
    expect(service.request()?.title).toBe('B');
    service.confirm();
    expect(await second).toBe(true);
  });
});
