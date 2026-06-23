import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';

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
export class AppComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private nextActivityId = 1;

  readonly targetBaseUrl = signal('loading...');
  readonly serviceNames = signal<string[]>([]);
  readonly pendingAction = signal<string | null>(null);
  readonly totalEmitted = signal(0);
  readonly activity = signal<ActivityItem[]>([]);

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

  private recordSuccess(result: EmitResponse): void {
    this.totalEmitted.update(total => total + result.count);
    this.addActivity(result.message, 'ok');
  }

  private recordError(text: string): void {
    this.addActivity(text, 'error');
    this.pendingAction.set(null);
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
