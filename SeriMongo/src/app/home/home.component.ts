import { CommonModule } from '@angular/common';
import { Component, AfterViewInit, ElementRef, OnDestroy, OnInit, ViewChild, signal } from '@angular/core';
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
export class HomeComponent implements OnInit, AfterViewInit, OnDestroy {

  title = 'SeriMongo';

  searchExpression = '*';
  logEntries = signal<LogEntry[]>([]);
  selectedRow = signal<LogEntry | null>(null);
  debugInfo = signal<Record<string, unknown>>({});
  tableLogsBodyHeight = signal(200);
  isTailing = signal(true);
  selectedPeriod = signal<string | null>(null);
  selectedPeriodSince = signal<string | null>(null);
  selectedLevels = signal<string[]>([]);

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
    this.signalRService.getLogEntries(this.onReceiveLogEntry);
    await this.signalRService.start();
  }

  onReceiveLogEntry = (logEntry: LogEntry) => {
    if (!this.isTailing()) {
      return;
    }

    this.logEntries.update(entries => [logEntry, ...entries].slice(0, 1000));
  };

  ngAfterViewInit(): void {
    window.requestAnimationFrame(this.resize);
  }

  async ngOnDestroy(): Promise<void> {
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

  @ViewChild('tableContainer')
  tableContainerElement?: ElementRef<HTMLElement>;

  @ViewChild('tableLogsBody')
  tableLogsBodyElement?: ElementRef<HTMLElement>;

  resize = () => {
    if (this.tableContainerElement && this.tableLogsBodyElement) {

      const tableLogsBodyTop = this.tableLogsBodyElement.nativeElement.offsetTop;
      const tableContainerTop = this.tableContainerElement.nativeElement.offsetTop;

      this.debugInfo.set({
        'tableLogsBodyOffsetHeight': this.tableLogsBodyElement.nativeElement.offsetHeight,
        'tableLogsBody':  this.tableLogsBodyElement.nativeElement.clientTop,
        '------------------------': '',
        'windowInnerHeight': window.innerHeight,
        'tableLogsBodyTop': this.tableLogsBodyElement.nativeElement.offsetTop,
        'tableContainerTop': this.tableContainerElement.nativeElement.offsetTop,
        'tableLogs.Body.Height': `${window.innerHeight} - ${tableLogsBodyTop} - ${tableContainerTop} - 10`,
        'tableLogsBodyHeight': window.innerHeight - tableLogsBodyTop - tableContainerTop - 10
      });

      this.tableLogsBodyHeight.set(window.innerHeight - tableLogsBodyTop - tableContainerTop - 10);
    }

    window.requestAnimationFrame(this.resize);
  };

  /**
   * Expands current log entry line to show structured logging
   * properties and scope properties saved by Serilog.
   */
  setClickedRow(le: LogEntry) {
    this.selectedRow.set(le.showDetails ? null : le);
    le.showDetails = !le.showDetails;
    this.logEntries.update(entries => [...entries]);
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
