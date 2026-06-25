import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnDestroy, OnInit, ViewChild, computed, signal } from '@angular/core';
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

type LogColumnId = 'traceId' | 'timestamp' | 'level' | 'serviceName' | 'message';

interface LogColumnOption {
  id: LogColumnId;
  label: string;
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
  tailInjectedLogIds = signal<ReadonlySet<string>>(new Set<string>());
  isTailing = signal(true);
  selectedPeriod = signal<string | null>(null);
  selectedPeriodSince = signal<string | null>(null);
  selectedLevels = signal<string[]>([]);
  serviceNames = signal<string[]>([]);
  selectedServiceNames = signal<string[]>([]);
  levelFilterOpen = signal(true);
  serviceFilterOpen = signal(true);
  visibleLogColumns = signal<LogColumnId[]>(['traceId', 'timestamp', 'level', 'serviceName', 'message']);
  detailSidebarWidth = signal(360);
  hasMoreSearchResults = signal(false);
  isLoadingSearchPage = signal(false);
  virtualScrollTop = signal(0);
  virtualViewportHeight = signal(0);

  readonly minDetailSidebarWidth = 280;
  readonly maxDetailSidebarWidth = 640;

  private readonly searchPageSize = 100;
  private readonly virtualRowHeightPx = 32;
  private readonly virtualOverscanRows = 8;
  private readonly defaultVirtualViewportHeightPx = 720;
  private readonly minLogAreaWidth = 380;
  private readonly loadMoreScrollThresholdPx = 240;
  private readonly tailInjectedHighlightMs = 120;
  private readonly tailInjectedTimers = new Map<string, ReturnType<typeof setTimeout>>();
  private currentSearchQuery = '*';
  private currentSearchPage = 0;
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

  readonly logColumnOptions: LogColumnOption[] = [
    { id: 'traceId', label: 'TraceId' },
    { id: 'timestamp', label: 'Timestamp' },
    { id: 'level', label: 'Level' },
    { id: 'serviceName', label: 'Service' },
    { id: 'message', label: 'Message' }
  ];

  readonly visibleLogColumnCount = computed(() => this.visibleLogColumns().length);

  readonly virtualRows = computed(() => {
    const entries = this.logEntries();
    const viewportHeight = this.virtualViewportHeight() || this.defaultVirtualViewportHeightPx;
    const visibleCount = Math.ceil(viewportHeight / this.virtualRowHeightPx) + (this.virtualOverscanRows * 2);
    const startIndex = Math.max(0, Math.floor(this.virtualScrollTop() / this.virtualRowHeightPx) - this.virtualOverscanRows);
    const endIndex = Math.min(entries.length, startIndex + visibleCount);

    return {
      entries: entries.slice(startIndex, endIndex),
      topPaddingPx: startIndex * this.virtualRowHeightPx,
      bottomPaddingPx: Math.max(0, entries.length - endIndex) * this.virtualRowHeightPx
    };
  });

  constructor(private signalRService: SignalRService,
    private searchService: SearchService) {
  }

  async ngOnInit(): Promise<void> {
    window.addEventListener('resize', this.clampSidebarToViewport);
    this.signalRService.getLogEntries(this.onReceiveLogEntry);
    this.signalRService.getServiceNames(this.onReceiveServiceNames);
    await this.signalRService.start();
  }

  onReceiveLogEntry = (logEntry: LogEntry) => {
    if (!this.isTailing()) {
      return;
    }

    this.markTailInjected(logEntry);
    this.logEntries.update(entries => [logEntry, ...entries].slice(0, 1000));
  };

  onReceiveServiceNames = (serviceNames: string[]) => {
    const nextServiceNames = serviceNames
      .map(serviceName => serviceName.trim())
      .filter(serviceName => serviceName.length > 0);

    this.serviceNames.set(nextServiceNames);
    this.selectedServiceNames.update(selected => selected.filter(serviceName => nextServiceNames.includes(serviceName)));
  };

  async ngOnDestroy(): Promise<void> {
    window.removeEventListener('resize', this.clampSidebarToViewport);
    this.clearTailInjectedRows();
    this.stopSidebarResize();
    await this.signalRService.stop();
  }

  async onSearchClick(): Promise<void> {
    this.refreshPeriodWindow();
    const query = this.buildLogQuery();
    await this.pauseTail();
    this.clearTailInjectedRows();
    await this.loadFirstSearchPage(query);
  }

  async selectPeriod(periodId: string): Promise<void> {
    const nextPeriod = this.selectedPeriod() === periodId ? null : periodId;
    this.selectedPeriod.set(nextPeriod);
    this.selectedPeriodSince.set(nextPeriod ? this.calculatePeriodSince(nextPeriod) : null);
    await this.onFilterChange();
  }

  async toggleLevel(level: string): Promise<void> {
    this.selectedLevels.update(levels => levels.includes(level)
      ? levels.filter(selected => selected !== level)
      : [...levels, level]);
    await this.onFilterChange();
  }

  async toggleServiceName(serviceName: string): Promise<void> {
    this.selectedServiceNames.update(serviceNames => serviceNames.includes(serviceName)
      ? serviceNames.filter(selected => selected !== serviceName)
      : [...serviceNames, serviceName]);
    await this.onFilterChange();
  }

  async clearQuickFilters(): Promise<void> {
    this.selectedPeriod.set(null);
    this.selectedPeriodSince.set(null);
    this.selectedLevels.set([]);
    this.selectedServiceNames.set([]);
    await this.onFilterChange();
  }

  isLevelSelected(level: string): boolean {
    return this.selectedLevels().includes(level);
  }

  isServiceNameSelected(serviceName: string): boolean {
    return this.selectedServiceNames().includes(serviceName);
  }

  isLogColumnVisible(column: LogColumnId): boolean {
    return this.visibleLogColumns().includes(column);
  }

  toggleLogColumn(column: LogColumnId): void {
    const visibleColumns = this.visibleLogColumns();
    if (visibleColumns.includes(column)) {
      if (visibleColumns.length === 1) {
        return;
      }

      this.visibleLogColumns.set(visibleColumns.filter(visibleColumn => visibleColumn !== column));
      return;
    }

    const nextVisibleColumns = new Set([...visibleColumns, column]);
    this.visibleLogColumns.set(this.logColumnOptions
      .map(option => option.id)
      .filter(option => nextVisibleColumns.has(option)));
  }

  toggleLevelFilter(): void {
    this.levelFilterOpen.update(open => !open);
  }

  toggleServiceFilter(): void {
    this.serviceFilterOpen.update(open => !open);
  }

  effectiveQueryPreview(): string {
    return this.buildLogQuery();
  }

  async toggleTail(): Promise<void> {
    const next = !this.isTailing();
    this.isTailing.set(next);
    if (next) {
      await this.signalRService.setTailQuery(this.buildLogQuery());
    }
  }

  trackByLogId(index: number, log: LogEntry): string {
    return log.id || index.toString();
  }

  isTailInjected(log: LogEntry): boolean {
    return !!log.id && this.tailInjectedLogIds().has(log.id);
  }

  async onLogScroll(event: Event): Promise<void> {
    const target = event.target as HTMLElement;
    this.virtualScrollTop.set(target.scrollTop);
    this.virtualViewportHeight.set(target.clientHeight);

    if (this.isTailing() || this.isLoadingSearchPage() || !this.hasMoreSearchResults()) {
      return;
    }

    const remainingScroll = target.scrollHeight - target.scrollTop - target.clientHeight;
    if (remainingScroll <= this.loadMoreScrollThresholdPx) {
      await this.loadNextSearchPage();
    }
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

  traceId(log: LogEntry): string {
    return this.firstPropertyText(log, 'traceId', 'TraceId', 'trace.id', 'TraceID') || '-';
  }

  private firstPropertyText(log: LogEntry, ...keys: string[]): string {
    for (const key of keys) {
      const value = log.properties[key];
      if (value !== null && value !== undefined) {
        return this.formatValue(value);
      }
    }

    return '';
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

    const serviceNames = this.selectedServiceNames();
    if (serviceNames.length > 0) {
      clauses.push(`serviceName in (${serviceNames.map(serviceName => this.quoteLogQueryValue(serviceName)).join(', ')})`);
    }

    return clauses.length > 0 ? clauses.join(' and ') : '*';
  }

  private quoteLogQueryValue(value: string): string {
    return `"${value.replace(/\\/g, '\\\\').replace(/"/g, '\\"')}"`;
  }

  private markTailInjected(logEntry: LogEntry): void {
    if (!logEntry.id) {
      return;
    }

    const logEntryId = logEntry.id;
    const existingTimer = this.tailInjectedTimers.get(logEntryId);
    if (existingTimer) {
      clearTimeout(existingTimer);
    }

    this.tailInjectedLogIds.update(ids => new Set(ids).add(logEntryId));
    this.tailInjectedTimers.set(logEntryId, setTimeout(() => {
      this.tailInjectedTimers.delete(logEntryId);
      this.tailInjectedLogIds.update(ids => {
        const nextIds = new Set(ids);
        nextIds.delete(logEntryId);
        return nextIds;
      });
    }, this.tailInjectedHighlightMs));
  }

  private clearTailInjectedRows(): void {
    for (const timer of this.tailInjectedTimers.values()) {
      clearTimeout(timer);
    }

    this.tailInjectedTimers.clear();
    this.tailInjectedLogIds.set(new Set<string>());
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

  @ViewChild('logTableBody')
  logTableBodyElement?: ElementRef<HTMLElement>;

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
  async setClickedRow(le: LogEntry): Promise<void> {
    const nextSelected = this.selectedRow() === le ? null : le;
    this.selectedRow.set(nextSelected);
    if (nextSelected) {
      await this.pauseTail();
    }
  }

  clearSelectedRow(): void {
    this.selectedRow.set(null);
  }

  private async onFilterChange(): Promise<void> {
    this.refreshPeriodWindow();
    const query = this.buildLogQuery();
    this.clearTailInjectedRows();

    if (this.isTailing()) {
      await this.signalRService.setTailQuery(query);
    }

    await this.loadFirstSearchPage(query);
  }

  private async loadFirstSearchPage(query: string): Promise<void> {
    this.currentSearchQuery = query;
    this.currentSearchPage = 1;
    const page = await this.searchService.search(this.currentSearchQuery, this.currentSearchPage, this.searchPageSize);
    this.resetLogScrollPosition();
    this.logEntries.set(page);
    this.hasMoreSearchResults.set(page.length === this.searchPageSize);
  }

  private resetLogScrollPosition(): void {
    const logTableBody = this.logTableBodyElement?.nativeElement;
    if (logTableBody) {
      logTableBody.scrollTop = 0;
      this.virtualViewportHeight.set(logTableBody.clientHeight);
    }

    this.virtualScrollTop.set(0);
  }

  private async loadNextSearchPage(): Promise<void> {
    this.isLoadingSearchPage.set(true);
    try {
      const nextPageNumber = this.currentSearchPage + 1;
      const page = await this.searchService.search(this.currentSearchQuery, nextPageNumber, this.searchPageSize);
      this.currentSearchPage = nextPageNumber;
      this.logEntries.update(entries => [...entries, ...page]);
      this.hasMoreSearchResults.set(page.length === this.searchPageSize);
    } finally {
      this.isLoadingSearchPage.set(false);
    }
  }

  private async pauseTail(): Promise<void> {
    if (!this.isTailing()) {
      return;
    }

    this.isTailing.set(false);
    await this.signalRService.pauseTail();
    this.clearTailInjectedRows();
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
