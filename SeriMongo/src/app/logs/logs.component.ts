import { Component, OnInit } from '@angular/core';

import { HubConnectionBuilder } from '@microsoft/signalr';
import { LogLevel } from '@microsoft/signalr';

import { LogEntry } from '../common/log-entry';
import { getLogEntries } from '../common/mocks';

@Component({
  selector: 'app-logs',
  templateUrl: './logs.component.html',
  styleUrls: ['./logs.component.css']
})
export class LogsComponent implements OnInit {

  logEntries: LogEntry[] = [];

  selectedRow: LogEntry;

  constructor() { }

  ngOnInit() {
    this.logEntries = getLogEntries();
  }

  setClickedRow(log: LogEntry) {
    this.selectedRow = log;
    log.showDetails = !log.showDetails;
  }


}
