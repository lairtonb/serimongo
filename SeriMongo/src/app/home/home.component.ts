import { CommonModule } from '@angular/common';
import { Component, AfterViewInit, ElementRef, OnDestroy, OnInit, ViewChild, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { LogEntry } from './log-entry';
import { SignalRService } from '../services/signalr.service';
import { SearchService } from '../services/search.service';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css']
})
export class HomeComponent implements OnInit, AfterViewInit, OnDestroy {

  title = 'SeriMongo UI';

  searchExpression = '*';
  logEntries = signal<LogEntry[]>([]);
  selectedRow = signal<LogEntry | null>(null);
  debugInfo = signal<Record<string, unknown>>({});
  tableLogsBodyHeight = signal(200);
  isTailing = signal(true);

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
    this.logEntries.set(await this.searchService.search(this.searchExpression));
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
