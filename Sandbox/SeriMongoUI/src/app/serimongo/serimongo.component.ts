import { Component, OnInit } from '@angular/core';

import { LogEntry } from './log-entry';
import { getLogEntries } from './mocks';

@Component({
  templateUrl: './serimongo.component.html',
  styleUrls: ['./serimongo.component.css']
})
export class SeriMongoComponent implements OnInit {
  title = 'SeriMongoUI';
  logEntries: LogEntry[] = [];

  selectedRow: LogEntry;

  constructor() {
    // this.startSignalR();
  }

  ngOnInit() {
    this.logEntries = getLogEntries();
  }

  setClickedRow(log: LogEntry) {
    this.selectedRow = log;
    log.showDetails = !log.showDetails;
  }

  redException(key) {
    if (key === 'Exception')
      return 'Exception';
  }

}
