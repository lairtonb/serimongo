import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../environments/environment';
import { TraceDetail, TraceSearchFilter, TraceSearchPage } from '../traces/trace.models';

@Injectable({
  providedIn: 'root'
})
export class TraceService {
  private readonly baseApiUrl = environment.apiBaseUrl;

  constructor(private readonly http: HttpClient) {}

  async search(filter: TraceSearchFilter): Promise<TraceSearchPage> {
    const query = new URLSearchParams();
    for (const [key, value] of Object.entries(filter)) {
      if (value !== undefined && value !== null && value !== '') {
        query.set(key, String(value));
      }
    }

    const queryString = query.size > 0 ? `?${query.toString()}` : '';
    return await firstValueFrom(this.http.get<TraceSearchPage>(`${this.baseApiUrl}/api/traces${queryString}`));
  }

  async getTrace(traceId: string): Promise<TraceDetail> {
    return await firstValueFrom(this.http.get<TraceDetail>(`${this.baseApiUrl}/api/traces/${encodeURIComponent(traceId)}`));
  }

  async getServiceNames(): Promise<string[]> {
    return await firstValueFrom(this.http.get<string[]>(`${this.baseApiUrl}/api/traces/services`));
  }
}