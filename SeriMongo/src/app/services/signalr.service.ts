import { Injectable } from '@angular/core';
import { ControlContainer } from '@angular/forms';
import * as signalR from "@microsoft/signalr";

import { Subscription } from 'rxjs/internal/Subscription';
import { LogEntry } from '../home/log-entry';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {

  // todo implement config service to get the server url
  private baseApiUrl = "http://localhost:51983";

  private connection: signalR.HubConnection;

  constructor() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(this.baseApiUrl + "/logs")
      .configureLogging(signalR.LogLevel.Information)
      .build();

    // todo implement better reconnect
    this.connection.onclose(this.start);
  }

  // todo implement async pattern
  start() {
    this.connection.start().catch(err => {
      // todo implement better error handling and notification
      console.error(err);
    });
  }

  // todo implement a better way to hanle log entries received from backend, most likely employig a Rx Subscription
  public getLogEntries(next: { (logEntry: LogEntry): void; }) {
    this.connection.on('OnReceiveLogEntry', (logEntry: LogEntry) => {
      next(logEntry);
    });
  }

  public stop() {
    this.connection.stop();
  }
}
