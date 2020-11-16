import { Component, OnInit, ViewChild, HostListener, AfterViewInit } from '@angular/core';
import { componentFactoryName } from '@angular/compiler';
import { stringify } from 'querystring';

import { LogEntry } from './log-entry';
import { getLogEntries } from './mocks';
import { SignalRService } from '../services/signalr.service';
import { Subscription } from 'rxjs/internal/Subscription';

@Component({
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css']
})
export class HomeComponent implements OnInit, AfterViewInit {

  title = 'SeriMongo UI';
  logEntries: LogEntry[] = [];
  selectedRow: LogEntry;
  debugInfo: any = {};

  private signalRSubscription: Subscription;

  constructor(private signalRService: SignalRService) {

  }

  // Replaced by requestAnimationFrame for a smoother experience
  // @HostListener('body:resize', ['$event'])
  onResize(event) {
    this.debugInfo = {
      tableContainerElement: this.tableContainerElement.offsetHeight
    }
    return true;
  }

  ngOnInit() {
    // this.logEntries = getLogEntries();
    this.signalRService.start();
    this.signalRService.getLogEntries(this.onReceiveLogEntry);
  }

  onReceiveLogEntry = (logEntry: LogEntry) => {
    this.logEntries.splice(0, 0, logEntry)
  }

  ngAfterViewInit() {
    window.requestAnimationFrame(this.resize);
  }

  @ViewChild('tableContainer')
  tableContainerElement: any; // Todo - see if it works with HtmlElement type

  @ViewChild('tableLogsBody')
  tableLogsBodyElement: any;  // Todo - see if it works with HtmlElement type

  tableLogsBodyHeight: number = 200;

  resize = () => {
    if (this.tableContainerElement) {

      const tableLogsBodyTop = this.tableLogsBodyElement.nativeElement.offsetTop;
      const tableContainerTop = this.tableContainerElement.nativeElement.offsetTop;

      this.debugInfo = {
        'tableLogsBodyOffsetHeight': this.tableLogsBodyElement.nativeElement.offsetHeight,
        'tableLogsBody':  this.tableLogsBodyElement.nativeElement.clientTop,
        '------------------------': '',
        'windowInnerHeight': window.innerHeight,
        'tableLogsBodyTop': this.tableLogsBodyElement.nativeElement.offsetTop,
        'tableContainerTop': this.tableContainerElement.nativeElement.offsetTop,
        'tableLogs.Body.Height': `${window.innerHeight} - ${tableLogsBodyTop} - ${tableContainerTop} - 10`,
        'tableLogsBodyHeight': window.innerHeight - tableLogsBodyTop - tableContainerTop - 10
      }

      this.tableLogsBodyHeight = window.innerHeight - tableLogsBodyTop - tableContainerTop - 10;
    }

    window.requestAnimationFrame(this.resize);
  }

  /**
   * Expands current log entry line to show structured logging
   * properties and scope properties saved by Serilog.
   */
  setClickedRow(le: LogEntry) {
    this.selectedRow = le;
    le.showDetails = !le.showDetails;
  }

  redException(key) {
    if (key === 'Exception')
      return 'Exception';
  }

  // TODO: inprove Angular $event handling following best practices
  // https://angular.io/guide/user-input

  /**
   * Expands search box to fit contents up to six lines, after which it adds scollbars.
   */
  controlHeight(e: HTMLTextAreaElement) {
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
  handleTab(e: KeyboardEvent, ta: HTMLTextAreaElement) {
    console.log(e.keyCode);
    console.log(ta.value);
    if (e.keyCode === 9) { // tab was pressed
      // get caret position/selection
      let val = ta.value;
      let start = ta.selectionStart;
      let end = ta.selectionEnd;

      // Set textarea value to: text before caret + tab + text after caret
      ta.value = val.substring(0, start) + '\x20\x20' + val.substring(end);

      // put caret at right position again
      ta.selectionStart = ta.selectionEnd = start + 2;

      // Prevent losing focus
      e.preventDefault();
    }
  }

}
