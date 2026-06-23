import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';

interface LevelAction {
  level: string;
  description: string;
  tone: string;
}

interface EmitResponse {
  level?: string;
  serviceName?: string;
  count: number;
  message: string;
}

interface InfoResponse {
  targetBaseUrl: string;
  serviceNames: string[];
}

interface ActivityItem {
  id: number;
  text: string;
  tone: 'ok' | 'error';
  timestamp: Date;
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit, OnDestroy {
  private readonly http = inject(HttpClient);
  private nextActivityId = 1;
  private continuousTimerId: ReturnType<typeof setTimeout> | null = null;

  readonly targetBaseUrl = signal('loading...');
  readonly serviceNames = signal<string[]>([]);
  readonly pendingAction = signal<string | null>(null);
  readonly totalEmitted = signal(0);
  readonly continuousRunning = signal(false);
  readonly continuousEmitted = signal(0);
  readonly nextContinuousDelayMs = signal<number | null>(null);
  readonly activity = signal<ActivityItem[]>([]);

  continuousCount = 25;
  continuousIntervalMs = 2000;
  continuousJitterMs = 500;

  readonly levels: LevelAction[] = [
    { level: 'Trace', description: 'High-volume probes and spans.', tone: 'trace' },
    { level: 'Debug', description: 'Diagnostic state snapshots.', tone: 'debug' },
    { level: 'Information', description: 'Successful request flow.', tone: 'information' },
    { level: 'Warning', description: 'Slow or degraded behavior.', tone: 'warning' },
    { level: 'Error', description: 'Recoverable exception path.', tone: 'error' },
    { level: 'Fatal', description: 'Critical worker failure.', tone: 'fatal' }
  ];

  ngOnInit(): void {
    this.http.get<InfoResponse>('/api/info').subscribe({
      next: info => {
        this.targetBaseUrl.set(info.targetBaseUrl);
        this.serviceNames.set(info.serviceNames);
      },
      error: () => this.targetBaseUrl.set('unavailable')
    });
  }

  ngOnDestroy(): void {
    this.clearContinuousTimer();
  }

  emit(level: string): void {
    this.pendingAction.set(level);
    this.http.post<EmitResponse>(`/api/logs/${level}`, {}).subscribe({
      next: result => this.recordSuccess(result),
      error: err => this.recordError(`Failed to emit ${level}: ${this.formatError(err)}`),
      complete: () => this.pendingAction.set(null)
    });
  }

  emitBurst(count: number): void {
    const action = `Burst ${count}`;
    this.pendingAction.set(action);
    this.http.post<EmitResponse>('/api/logs/burst', { count }).subscribe({
      next: result => this.recordSuccess(result),
      error: err => this.recordError(`Failed to emit burst: ${this.formatError(err)}`),
      complete: () => this.pendingAction.set(null)
    });
  }

  startContinuous(): void {
    if (this.continuousRunning()) {
      return;
    }

    this.normalizeContinuousSettings();
    this.continuousRunning.set(true);
    this.addActivity(`Continuous logging started: ${this.continuousCount} logs every ${this.continuousIntervalMs}ms +/- ${this.continuousJitterMs}ms.`, 'ok');
    this.scheduleContinuousBurst(0);
  }

  stopContinuous(): void {
    if (!this.continuousRunning()) {
      return;
    }

    this.continuousRunning.set(false);
    this.nextContinuousDelayMs.set(null);
    this.clearContinuousTimer();
    this.addActivity('Continuous logging stopped.', 'ok');
  }

  updateContinuousCount(event: Event): void {
    this.continuousCount = this.readNumberInput(event, this.continuousCount);
  }

  updateContinuousIntervalMs(event: Event): void {
    this.continuousIntervalMs = this.readNumberInput(event, this.continuousIntervalMs);
  }

  updateContinuousJitterMs(event: Event): void {
    this.continuousJitterMs = this.readNumberInput(event, this.continuousJitterMs);
  }

  private recordSuccess(result: EmitResponse): void {
    this.totalEmitted.update(total => total + result.count);
    this.addActivity(result.message, 'ok');
  }

  private recordError(text: string): void {
    this.addActivity(text, 'error');
    this.pendingAction.set(null);
  }

  private emitContinuousBurst(): void {
    if (!this.continuousRunning()) {
      return;
    }

    this.normalizeContinuousSettings();
    this.http.post<EmitResponse>('/api/logs/burst', { count: this.continuousCount }).subscribe({
      next: result => {
        this.totalEmitted.update(total => total + result.count);
        this.continuousEmitted.update(total => total + result.count);
        this.addActivity(`Continuous: ${result.message}`, 'ok');
      },
      error: err => {
        this.addActivity(`Continuous logging failed: ${this.formatError(err)}`, 'error');
        this.scheduleNextContinuousBurst();
      },
      complete: () => this.scheduleNextContinuousBurst()
    });
  }

  private scheduleNextContinuousBurst(): void {
    if (this.continuousRunning()) {
      this.scheduleContinuousBurst(this.getNextContinuousDelayMs());
    }
  }

  private scheduleContinuousBurst(delayMs: number): void {
    this.clearContinuousTimer();
    this.nextContinuousDelayMs.set(delayMs);
    this.continuousTimerId = setTimeout(() => this.emitContinuousBurst(), delayMs);
  }

  private clearContinuousTimer(): void {
    if (this.continuousTimerId !== null) {
      clearTimeout(this.continuousTimerId);
      this.continuousTimerId = null;
    }
  }

  private normalizeContinuousSettings(): void {
    this.continuousCount = this.clampInteger(this.continuousCount, 1, 200, 25);
    this.continuousIntervalMs = this.clampInteger(this.continuousIntervalMs, 250, 60_000, 2000);
    this.continuousJitterMs = this.clampInteger(this.continuousJitterMs, 0, 60_000, 500);
  }

  private getNextContinuousDelayMs(): number {
    this.normalizeContinuousSettings();
    const jitterOffset = this.continuousJitterMs === 0
      ? 0
      : Math.round((Math.random() * 2 - 1) * this.continuousJitterMs);

    return Math.max(100, this.continuousIntervalMs + jitterOffset);
  }

  private clampInteger(value: number, min: number, max: number, fallback: number): number {
    const parsed = Math.trunc(Number(value));
    if (!Number.isFinite(parsed)) {
      return fallback;
    }

    return Math.min(Math.max(parsed, min), max);
  }

  private readNumberInput(event: Event, fallback: number): number {
    const target = event.target as HTMLInputElement | null;
    if (!target) {
      return fallback;
    }

    const value = Number(target.value);
    return Number.isFinite(value) ? value : fallback;
  }

  private addActivity(text: string, tone: 'ok' | 'error'): void {
    const item: ActivityItem = {
      id: this.nextActivityId++,
      text,
      tone,
      timestamp: new Date()
    };

    this.activity.update(items => [item, ...items].slice(0, 12));
  }

  private formatError(err: unknown): string {
    if (typeof err === 'object' && err !== null && 'message' in err) {
      return String((err as { message: unknown }).message);
    }

    return 'unknown error';
  }
}
