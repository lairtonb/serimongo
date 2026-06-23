import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';

import { LogEntry } from '../home/log-entry';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private readonly baseApiUrl = environment.apiBaseUrl;
  private readonly connection: signalR.HubConnection;

  constructor() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(this.baseApiUrl + '/logs')
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();
  }

  async start(): Promise<void> {
    if (this.connection.state !== signalR.HubConnectionState.Disconnected) {
      return;
    }

    try {
      await this.connection.start();
    } catch (err) {
      console.error(err);
    }
  }

  getLogEntries(next: (logEntry: LogEntry) => void): void {
    this.connection.on('OnReceiveLogEntry', (logEntry: LogEntry) => {
      next(logEntry);
    });
  }

  getServiceNames(next: (serviceNames: string[]) => void): void {
    this.connection.on('OnReceiveServiceNames', (serviceNames: string[]) => {
      next(serviceNames);
    });
  }

  async stop(): Promise<void> {
    await this.connection.stop();
  }
}
