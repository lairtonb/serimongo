import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';

import { LogEntry } from '../home/log-entry';

@Injectable({
  providedIn: 'root'
})
export class SearchService {

  // todo implement config service to get the server url
  private baseApiUrl = "http://localhost:51983";

  constructor(private http: HttpClient) {}

  async search(searchExpression: string): Promise<LogEntry[]> {
    try {
      const url = this.baseApiUrl + '/api/search/?currentPage=1&pageSize=100';
      const body = JSON.parse(searchExpression);
      return await this.http.post<LogEntry[]>(url, body).toPromise();
    } catch (err) {
      console.error(err);
    }
  }

}
