import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnDestroy, OnInit, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { ionCloseCircleOutline, ionCopyOutline, ionRefreshOutline } from '@ng-icons/ionicons';

import { TraceService } from '../services/trace.service';
import { TraceDetail, TraceSearchFilter, TraceSearchPage, TraceSpan, TraceStatusFilter, TraceSummary } from './trace.models';

interface TracePeriodOption {
  label: string;
  value: string;
  hours: number | null;
}

@Component({
  selector: 'app-trace-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, NgIcon],
  providers: [provideIcons({ ionCloseCircleOutline, ionCopyOutline, ionRefreshOutline })],
  templateUrl: './trace-panel.component.html',
  styleUrls: ['./trace-panel.component.css']
})
export class TracePanelComponent implements OnInit, OnDestroy {
  @Input()
  set focusedTraceId(value: string | null) {
    const traceId = this.isTraceId(value) ? value : null;
    if (this._focusedTraceId === traceId) {
      return;
    }

    this._focusedTraceId = traceId;
    if (traceId) {
      this.traceId = traceId;
      if (this.initialized) {
        void this.loadTrace(traceId);
      }
    }
  }

  @Input()
  set refreshVersion(value: number) {
    if (value === this._refreshVersion) {
      return;
    }

    this._refreshVersion = value;
    if (this.initialized) {
      this.scheduleRefresh();
    }
  }

  @Output() closed = new EventEmitter<void>();
  @Output() spanSelected = new EventEmitter<TraceSpan | null>();

  traceId = '';
  serviceName = '';
  rootOperationName = '';
  status: TraceStatusFilter = 'All';
  minimumDurationMs: number | null = null;
  selectedPeriod = '24h';

  readonly page = signal<TraceSearchPage | null>(null);
  readonly services = signal<string[]>([]);
  readonly selectedTrace = signal<TraceDetail | null>(null);
  readonly selectedSpan = signal<TraceSpan | null>(null);
  readonly loading = signal(false);
  readonly loadingTrace = signal(false);
  readonly error = signal<string | null>(null);

  readonly periods: TracePeriodOption[] = [
    { label: 'Any time', value: 'any', hours: null },
    { label: 'Last hour', value: '1h', hours: 1 },
    { label: 'Last 24 hours', value: '24h', hours: 24 },
    { label: 'Last 7 days', value: '168h', hours: 168 }
  ];

  readonly statuses: TraceStatusFilter[] = ['All', 'Successful', 'Error'];

  private _focusedTraceId: string | null = null;
  private _refreshVersion = 0;
  private initialized = false;
  private refreshTimer?: ReturnType<typeof setTimeout>;

  constructor(private readonly traceService: TraceService) {}

  async ngOnInit(): Promise<void> {
    await Promise.all([this.loadServices(), this.refresh()]);
    this.initialized = true;
    if (this._focusedTraceId) {
      await this.loadTrace(this._focusedTraceId);
    }
  }

  ngOnDestroy(): void {
    if (this.refreshTimer) {
      clearTimeout(this.refreshTimer);
    }
  }

  async refresh(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      this.page.set(await this.traceService.search(this.buildSearchFilter()));
    } catch (error) {
      console.error('Unable to load traces.', error);
      this.error.set('Trace data could not be loaded.');
    } finally {
      this.loading.set(false);
    }
  }

  async selectTrace(summary: TraceSummary): Promise<void> {
    await this.loadTrace(summary.traceId);
  }

  private async loadTrace(traceId: string): Promise<void> {
    this.loadingTrace.set(true);
    this.error.set(null);

    try {
      const trace = await this.traceService.getTrace(traceId);
      this.selectedTrace.set(trace);
      this.selectSpan(this.rootSpan(trace) ?? trace.spans[0] ?? null);
    } catch (error) {
      console.error('Unable to load trace details.', error);
      this.error.set('Trace details could not be loaded.');
    } finally {
      this.loadingTrace.set(false);
    }
  }

  closeTrace(): void {
    this.selectedTrace.set(null);
    this.selectSpan(null);
  }

  closePanel(): void {
    this.selectSpan(null);
    this.closed.emit();
  }

  selectSpan(span: TraceSpan | null): void {
    this.selectedSpan.set(span);
    this.spanSelected.emit(span);
  }

  orderedSpans(trace: TraceDetail): TraceSpan[] {
    return [...trace.spans].sort((left, right) =>
      Date.parse(left.startTimestamp) - Date.parse(right.startTimestamp)
      || left.spanId.localeCompare(right.spanId));
  }

  spanDepth(span: TraceSpan, trace: TraceDetail): number {
    const spansById = new Map(trace.spans.map(item => [item.spanId, item]));
    const visited = new Set<string>();
    let depth = 0;
    let parentSpanId = span.parentSpanId;

    while (parentSpanId && !visited.has(parentSpanId)) {
      visited.add(parentSpanId);
      const parent = spansById.get(parentSpanId);
      if (!parent) {
        break;
      }

      depth++;
      parentSpanId = parent.parentSpanId;
    }

    return depth;
  }

  spanOffset(span: TraceSpan, trace: TraceDetail): number {
    const traceStart = Date.parse(trace.summary.startTimestamp);
    const durationMs = Math.max(1, trace.summary.durationNanoseconds / 1_000_000);
    return Math.min(100, Math.max(0, ((Date.parse(span.startTimestamp) - traceStart) / durationMs) * 100));
  }

  spanWidth(span: TraceSpan, trace: TraceDetail): number {
    const durationMs = Math.max(1, trace.summary.durationNanoseconds / 1_000_000);
    const rawWidth = (span.durationNanoseconds / 1_000_000 / durationMs) * 100;
    return Math.min(100 - this.spanOffset(span, trace), Math.max(1.5, rawWidth));
  }

  spanTone(span: TraceSpan): string {
    return span.statusCode === 2 ? 'error' : span.statusCode === 1 ? 'ok' : 'unset';
  }

  formatDuration(durationNanoseconds: number): string {
    if (durationNanoseconds >= 1_000_000_000) {
      return `${(durationNanoseconds / 1_000_000_000).toFixed(2)}s`;
    }

    if (durationNanoseconds >= 1_000_000) {
      return `${Math.round(durationNanoseconds / 1_000_000)}ms`;
    }

    return `${Math.round(durationNanoseconds / 1_000)}us`;
  }

  formatTimestamp(timestamp: string): string {
    const date = new Date(timestamp);
    return Number.isNaN(date.getTime()) ? timestamp : date.toISOString();
  }

  abbreviatedTraceId(traceId: string): string {
    return traceId.length <= 16 ? traceId : `${traceId.slice(0, 8)}...${traceId.slice(-6)}`;
  }

  async copyTraceId(traceId: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(traceId);
    } catch (error) {
      console.error('Unable to copy trace ID.', error);
    }
  }

  trackByTraceId(index: number, trace: TraceSummary): string {
    return trace.traceId || index.toString();
  }

  trackBySpanId(index: number, span: TraceSpan): string {
    return span.spanId || index.toString();
  }

  private async loadServices(): Promise<void> {
    try {
      this.services.set(await this.traceService.getServiceNames());
    } catch (error) {
      console.error('Unable to load trace services.', error);
    }
  }

  private buildSearchFilter(): TraceSearchFilter {
    const period = this.periods.find(option => option.value === this.selectedPeriod);
    const startTimestamp = period?.hours
      ? new Date(Date.now() - period.hours * 60 * 60 * 1000).toISOString()
      : undefined;

    return {
      traceId: this.traceId.trim() || undefined,
      serviceName: this.serviceName || undefined,
      rootOperationName: this.rootOperationName.trim() || undefined,
      status: this.status,
      minimumDurationNanoseconds: this.minimumDurationMs && this.minimumDurationMs > 0
        ? Math.round(this.minimumDurationMs * 1_000_000)
        : undefined,
      startTimestamp,
      page: 1,
      pageSize: 50
    };
  }

  private rootSpan(trace: TraceDetail): TraceSpan | undefined {
    return trace.spans.find(span => span.spanId === trace.summary.rootSpanId)
      ?? trace.spans.find(span => !span.parentSpanId);
  }

  private scheduleRefresh(): void {
    if (this.refreshTimer) {
      clearTimeout(this.refreshTimer);
    }

    this.refreshTimer = setTimeout(() => {
      this.refreshTimer = undefined;
      void this.refreshAfterRealtimeUpdate();
    }, 250);
  }

  private async refreshAfterRealtimeUpdate(): Promise<void> {
    await this.refresh();
    const traceId = this.selectedTrace()?.summary.traceId ?? this._focusedTraceId;
    if (traceId) {
      await this.loadTrace(traceId);
    }
  }

  private isTraceId(value: string | null): value is string {
    return !!value && /^[0-9a-f]{32}$/.test(value);
  }
}