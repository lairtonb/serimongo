import { Component, VERSION } from '@angular/core';

import { LogEntry } from './log-entry';
import { getLogEntries } from './mocks';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: [ './app.component.css' ]
})
export class AppComponent  {
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
