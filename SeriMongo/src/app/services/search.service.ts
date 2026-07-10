import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { LogEntry } from '../home/log-entry';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class SearchService {
  private readonly baseApiUrl = environment.apiBaseUrl;

  constructor(private http: HttpClient) {}

  async search(searchExpression: string, currentPage = 1, pageSize = 100): Promise<LogEntry[]> {
    const url = this.baseApiUrl + `/api/search/?currentPage=${currentPage}&pageSize=${pageSize}`;
    const body = { query: searchExpression?.trim() || '*' };
    return await firstValueFrom(this.http.post<LogEntry[]>(url, body));
  }
}
