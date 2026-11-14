import { ReadingPosition } from './reading-position';
export interface ProgressSnapshot extends Partial<ReadingPosition> { progressUpdatedAt: string | null; }
export interface ProgressSaved { contentKind?: 'full' | 'summaryOnly'; progressUpdatedAt: string; readingStatus: 'unread' | 'reading' | 'read'; lastReadAt: string | null; }
export interface PendingProgress { position: ReadingPosition; baseUpdatedAt: string | null; }
export interface ProgressRequest extends ReadingPosition { sessionId: string; baseUpdatedAt: string | null; }
export type SyncStatus = 'synced' | 'saving' | 'unsynced';
interface WriterCallbacks {
  send: (request: ProgressRequest, keepalive: boolean) => Promise<ProgressSaved>;
  reset: (sessionId: string) => Promise<ProgressSaved>;
  persist: (pending: PendingProgress | null) => void;
  onChange: (status: SyncStatus, conflict: ProgressSnapshot | null) => void;
  onSaved: (result: ProgressSaved, position: ReadingPosition | null) => void;
}

/** One in-flight write per page, including reset. Failed requests retain their original base. */
export class ProgressWriter {
  pending: PendingProgress | null;
  conflict: ProgressSnapshot | null = null;
  private inFlight: Promise<void> | null = null;
  private timer: ReturnType<typeof setTimeout> | undefined;
  private lastStartedAt = 0;
  private retry = 0;
  private retryDelay: number | null = null;
  private visible = true;
  private resetting = false;
  private changingState = false;
  private status: SyncStatus = 'synced';
  constructor(readonly sessionId: string, public baseUpdatedAt: string | null, private callbacks: WriterCallbacks, pending: PendingProgress | null = null) {
    this.pending = pending;
  }
  private changed(status = this.status) { this.status = status; this.callbacks.onChange(status, this.conflict); }
  private clearTimer() { clearTimeout(this.timer); this.timer = undefined; }
  private schedule(delay: number) {
    this.clearTimer();
    if (this.visible && this.pending && !this.conflict && !this.resetting && !this.changingState)
      this.timer = setTimeout(() => { void this.flush(); }, delay);
  }
  queue(position: ReadingPosition) {
    if (this.resetting) return;
    this.pending = { position, baseUpdatedAt: this.pending ? this.pending.baseUpdatedAt : this.baseUpdatedAt };
    this.callbacks.persist(this.pending);
    if (this.status !== 'unsynced') this.changed('saving');
    this.schedule(Math.max(0, 5000 - (Date.now() - this.lastStartedAt)));
  }
  setVisible(visible: boolean) {
    this.visible = visible;
    if (!visible) this.clearTimer();
    else this.schedule(0);
  }
  showConflict(current: ProgressSnapshot) { this.conflict = current; this.clearTimer(); this.changed('unsynced'); }
  resolveConflict(current: ProgressSnapshot, jump: boolean) {
    this.baseUpdatedAt = current.progressUpdatedAt;
    this.conflict = null; this.retry = 0; this.retryDelay = null;
    if (jump) this.pending = null;
    else if (this.pending) this.pending = { ...this.pending, baseUpdatedAt: this.baseUpdatedAt };
    this.callbacks.persist(this.pending);
    this.changed(this.pending ? 'saving' : 'synced');
    this.schedule(0);
  }
  async flush(keepalive = false): Promise<void> {
    this.clearTimer();
    if (this.inFlight) { await this.inFlight; if (keepalive) await this.flush(true); return; }
    if (!this.pending || this.conflict || this.resetting || this.changingState || (!this.visible && !keepalive)) return;
    const pending = this.pending;
    this.pending = null;
    this.lastStartedAt = Date.now();
    this.changed('saving');
    this.inFlight = (async () => {
      try {
        const result = await this.callbacks.send({ ...pending.position, sessionId: this.sessionId, baseUpdatedAt: pending.baseUpdatedAt }, keepalive);
        this.baseUpdatedAt = result.progressUpdatedAt;
        this.retry = 0; this.retryDelay = null;
        if (this.pending) this.pending.baseUpdatedAt = this.baseUpdatedAt;
        this.callbacks.persist(this.pending);
        this.callbacks.onSaved(result, pending.position);
        this.changed(this.pending ? 'saving' : 'synced');
      } catch (error) {
        this.pending ||= pending;
        this.callbacks.persist(this.pending);
        const failure = error as { status?: number; current?: ProgressSnapshot };
        if (failure.status === 409 && failure.current) this.showConflict(failure.current);
        else {
          this.changed('unsynced');
          this.retryDelay = !failure.status || failure.status >= 500 ? [2000, 5000, 15000, 30000][Math.min(this.retry++, 3)] : null;
          if (this.retryDelay != null) this.schedule(this.retryDelay);
        }
      }
    })();
    await this.inFlight;
    this.inFlight = null;
    if (this.pending && !this.conflict && this.status !== 'unsynced') this.schedule(Math.max(0, 5000 - (Date.now() - this.lastStartedAt)));
  }
  async withStateChange(action: () => Promise<void>, drainPending = false): Promise<void> {
    this.changingState = true; this.clearTimer();
    try {
      await this.inFlight;
      while (drainPending && this.pending) {
        this.changingState = false;
        await this.flush(true);
        this.changingState = true; this.clearTimer();
        if (this.pending && (this.status === 'unsynced' || this.conflict))
          throw new Error('Resolve unsaved progress before marking unread.');
      }
      await action();
    }
    finally {
      this.changingState = false;
      if (this.status !== 'unsynced' || this.retryDelay != null) this.schedule(this.retryDelay ?? 0);
    }
  }
  async reset(): Promise<void> {
    this.resetting = true; this.clearTimer(); this.pending = null; this.conflict = null;
    this.callbacks.persist(null);
    await this.inFlight;
    // A failed earlier PUT must not remain queued behind this reset.
    this.pending = null; this.conflict = null; this.callbacks.persist(null); this.changed('saving');
    try {
      const result = await this.callbacks.reset(this.sessionId);
      this.baseUpdatedAt = result.progressUpdatedAt; this.retry = 0; this.retryDelay = null;
      this.callbacks.onSaved(result, null); this.changed('synced');
    } catch (error) { this.changed('unsynced'); throw error; }
    finally { this.resetting = false; }
  }
}
