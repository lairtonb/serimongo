import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';

import { LogEntry } from '../home/log-entry';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class SearchService {

  // todo implement config service to get the server url
  private baseApiUrl = "http://localhost:51983";

  constructor(private http: HttpClient) {}

  search(searchExpression: string): Observable<LogEntry[]> {
    const url = this.baseApiUrl + '/api/search/?currentPage=1&pageSize=100';
    const body = JSON.parse(searchExpression);
    return this.http.post<LogEntry[]>(url, body);
  }
}
