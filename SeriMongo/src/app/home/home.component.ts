import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnDestroy, OnInit, ViewChild, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { LogEntry } from './log-entry';
import { SignalRService } from '../services/signalr.service';
import { SearchService } from '../services/search.service';

type PeriodUnit = 'minute' | 'hour' | 'day' | 'month';

interface PeriodOption {
  id: string;
  label: string;
  amount: number;
  unit: PeriodUnit;
}

interface LevelOption {
  name: string;
  tone: string;
}

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css']
})
export class HomeComponent implements OnInit, OnDestroy {

  title = 'SeriMongo';

  searchExpression = '*';
  logEntries = signal<LogEntry[]>([]);
  selectedRow = signal<LogEntry | null>(null);
  isTailing = signal(true);
  selectedPeriod = signal<string | null>(null);
  selectedPeriodSince = signal<string | null>(null);
  selectedLevels = signal<string[]>([]);
  detailSidebarWidth = signal(360);

  readonly minDetailSidebarWidth = 280;
  readonly maxDetailSidebarWidth = 640;

  private readonly minLogAreaWidth = 380;
  private resizeStartX = 0;
  private resizeStartWidth = 0;

  readonly periodOptions: PeriodOption[] = [
    { id: '5m', label: '5m', amount: 5, unit: 'minute' },
    { id: '15m', label: '15m', amount: 15, unit: 'minute' },
    { id: '30m', label: '30m', amount: 30, unit: 'minute' },
    { id: '1h', label: '1h', amount: 1, unit: 'hour' },
    { id: '2h', label: '2h', amount: 2, unit: 'hour' },
    { id: '4h', label: '4h', amount: 4, unit: 'hour' },
    { id: '12h', label: '12h', amount: 12, unit: 'hour' },
    { id: '1d', label: '1d', amount: 1, unit: 'day' },
    { id: '3d', label: '3d', amount: 3, unit: 'day' },
    { id: '7d', label: '7d', amount: 7, unit: 'day' },
    { id: '1mo', label: '1 mo', amount: 1, unit: 'month' },
    { id: '2mo', label: '2 mo', amount: 2, unit: 'month' },
    { id: '3mo', label: '3 mo', amount: 3, unit: 'month' }
  ];

  readonly levelOptions: LevelOption[] = [
    { name: 'Verbose', tone: 'verbose' },
    { name: 'Trace', tone: 'trace' },
    { name: 'Debug', tone: 'debug' },
    { name: 'Information', tone: 'information' },
    { name: 'Warning', tone: 'warning' },
    { name: 'Error', tone: 'error' },
    { name: 'Fatal', tone: 'fatal' },
    { name: 'Critical', tone: 'critical' }
  ];

  constructor(private signalRService: SignalRService,
    private searchService: SearchService) {
  }

  async ngOnInit(): Promise<void> {
    window.addEventListener('resize', this.clampSidebarToViewport);
    this.signalRService.getLogEntries(this.onReceiveLogEntry);
    await this.signalRService.start();
  }

  onReceiveLogEntry = (logEntry: LogEntry) => {
    if (!this.isTailing()) {
      return;
    }

    this.logEntries.update(entries => [logEntry, ...entries].slice(0, 1000));
  };

  async ngOnDestroy(): Promise<void> {
    window.removeEventListener('resize', this.clampSidebarToViewport);
    this.stopSidebarResize();
    await this.signalRService.stop();
  }

  async onSearchClick(): Promise<void> {
    this.refreshPeriodWindow();
    this.logEntries.set(await this.searchService.search(this.buildLogQuery()));
  }

  async selectPeriod(periodId: string): Promise<void> {
    const nextPeriod = this.selectedPeriod() === periodId ? null : periodId;
    this.selectedPeriod.set(nextPeriod);
    this.selectedPeriodSince.set(nextPeriod ? this.calculatePeriodSince(nextPeriod) : null);
    await this.onSearchClick();
  }

  async toggleLevel(level: string): Promise<void> {
    this.selectedLevels.update(levels => levels.includes(level)
      ? levels.filter(selected => selected !== level)
      : [...levels, level]);
    await this.onSearchClick();
  }

  async clearQuickFilters(): Promise<void> {
    this.selectedPeriod.set(null);
    this.selectedPeriodSince.set(null);
    this.selectedLevels.set([]);
    await this.onSearchClick();
  }

  isLevelSelected(level: string): boolean {
    return this.selectedLevels().includes(level);
  }

  effectiveQueryPreview(): string {
    return this.buildLogQuery();
  }

  toggleTail(): void {
    this.isTailing.update(value => !value);
  }

  trackByLogId(index: number, log: LogEntry): string {
    return log.id || index.toString();
  }

  formatValue(value: unknown): string {
    if (value === null || value === undefined) {
      return '';
    }

    if (typeof value === 'object') {
      return JSON.stringify(value, null, 2);
    }

    return String(value);
  }

  serviceName(log: LogEntry): string {
    return this.formatValue(log.properties['resource.service.name']) || '-';
  }

  private buildLogQuery(): string {
    const clauses: string[] = [];
    const manualQuery = this.searchExpression.trim();

    if (manualQuery && manualQuery !== '*') {
      clauses.push(`(${manualQuery})`);
    }

    const periodClause = this.buildPeriodClause();
    if (periodClause) {
      clauses.push(periodClause);
    }

    const levels = this.selectedLevels();
    if (levels.length > 0 && levels.length < this.levelOptions.length) {
      clauses.push(`level in (${levels.join(', ')})`);
    }

    return clauses.length > 0 ? clauses.join(' and ') : '*';
  }

  private buildPeriodClause(): string | null {
    const since = this.selectedPeriodSince();
    return since ? `timestamp >= "${since}"` : null;
  }

  private refreshPeriodWindow(): void {
    const selectedPeriod = this.selectedPeriod();
    this.selectedPeriodSince.set(selectedPeriod ? this.calculatePeriodSince(selectedPeriod) : null);
  }

  private calculatePeriodSince(periodId: string): string | null {
    const period = this.periodOptions.find(option => option.id === periodId);
    if (!period) {
      return null;
    }

    const since = new Date();
    switch (period.unit) {
      case 'minute':
        since.setMinutes(since.getMinutes() - period.amount);
        break;
      case 'hour':
        since.setHours(since.getHours() - period.amount);
        break;
      case 'day':
        since.setDate(since.getDate() - period.amount);
        break;
      case 'month':
        since.setMonth(since.getMonth() - period.amount);
        break;
    }

    return since.toISOString();
  }

  @ViewChild('resultsShell')
  resultsShellElement?: ElementRef<HTMLElement>;

  startSidebarResize(event: PointerEvent): void {
    if (event.button !== 0) {
      return;
    }

    event.preventDefault();
    this.resizeStartX = event.clientX;
    this.resizeStartWidth = this.detailSidebarWidth();
    document.body.classList.add('resizing-details');
    window.addEventListener('pointermove', this.resizeSidebar);
    window.addEventListener('pointerup', this.stopSidebarResize);
    window.addEventListener('pointercancel', this.stopSidebarResize);
  }

  resizeSidebarWithKeyboard(event: KeyboardEvent): void {
    const step = event.shiftKey ? 48 : 16;

    switch (event.key) {
      case 'ArrowLeft':
        this.detailSidebarWidth.set(this.clampDetailSidebarWidth(this.detailSidebarWidth() + step));
        event.preventDefault();
        break;
      case 'ArrowRight':
        this.detailSidebarWidth.set(this.clampDetailSidebarWidth(this.detailSidebarWidth() - step));
        event.preventDefault();
        break;
      case 'Home':
        this.detailSidebarWidth.set(this.minDetailSidebarWidth);
        event.preventDefault();
        break;
      case 'End':
        this.detailSidebarWidth.set(this.clampDetailSidebarWidth(this.maxDetailSidebarWidth));
        event.preventDefault();
        break;
    }
  }

  private readonly resizeSidebar = (event: PointerEvent): void => {
    const delta = this.resizeStartX - event.clientX;
    this.detailSidebarWidth.set(this.clampDetailSidebarWidth(this.resizeStartWidth + delta));
  };

  private readonly stopSidebarResize = (): void => {
    document.body.classList.remove('resizing-details');
    window.removeEventListener('pointermove', this.resizeSidebar);
    window.removeEventListener('pointerup', this.stopSidebarResize);
    window.removeEventListener('pointercancel', this.stopSidebarResize);
  };

  private readonly clampSidebarToViewport = (): void => {
    this.detailSidebarWidth.set(this.clampDetailSidebarWidth(this.detailSidebarWidth()));
  };

  private clampDetailSidebarWidth(width: number): number {
    const resultsWidth = this.resultsShellElement?.nativeElement.clientWidth ?? window.innerWidth;
    const maxAvailableWidth = Math.max(this.minDetailSidebarWidth, resultsWidth - this.minLogAreaWidth - 8);
    const maxWidth = Math.max(this.minDetailSidebarWidth, Math.min(this.maxDetailSidebarWidth, maxAvailableWidth));

    return Math.min(Math.max(width, this.minDetailSidebarWidth), maxWidth);
  }

  /** Selects a log entry for inspection in the details sidebar. */
  setClickedRow(le: LogEntry) {
    this.selectedRow.update(selected => selected === le ? null : le);
  }

  clearSelectedRow(): void {
    this.selectedRow.set(null);
  }

  // TODO: inprove Angular $event handling following best practices
  // https://angular.io/guide/user-input

  /**
   * Expands search box to fit contents up to six lines, after which it adds scollbars.
   */
  controlHeight(e: HTMLTextAreaElement): void {
    // this.smsMessage = this.smsMessage.length > 10 ? this.smsMessage.substr(0, 10), this.smsMessage;
    // console.log(e);
    // textarea.style.height = '';
    // textarea.style.height = Math.min(textarea.scrollHeight, limit) + 'px';
    // console.log(e.scrollHeight);
    e.style.height = 'auto';

    // Magic number 3 is the border + 1 (awful, need to improve).
    // This prevents the scrollbar unless height is higher than max-height.
    e.style.height = (3 + e.scrollHeight) + 'px';

    /*
    this.debugInfo = {
      tableContainerElement_offsetHeight: this.tableContainerElement.nativeElement.offsetHeight
    }
    */
  }

  /**
   * Prevent tab keyboard event from making the focus to move to next control.
   * Bug: does not handle Shift+Tab yet
   **/
  handleTab(e: KeyboardEvent, ta: HTMLTextAreaElement): void {
    if (e.key === 'Tab') {
      // get caret position/selection
      const val = ta.value;
      const start = ta.selectionStart;
      const end = ta.selectionEnd;

      // Set textarea value to: text before caret + tab + text after caret
      ta.value = val.substring(0, start) + '\x20\x20' + val.substring(end);

      // put caret at right position again
      ta.selectionStart = ta.selectionEnd = start + 2;

      // Prevent losing focus
      e.preventDefault();
    }
  }

}
