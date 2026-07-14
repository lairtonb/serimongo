export type TraceStatusFilter = 'All' | 'Error' | 'Successful';

export interface TraceSummary {
  traceId: string;
  startTimestamp: string;
  endTimestamp: string;
  durationNanoseconds: number;
  rootSpanId: string | null;
  rootSpanName: string;
  rootServiceName: string;
  spanCount: number;
  errorSpanCount: number;
  serviceNames: string[];
  lastUpdated: string;
}

export interface TraceSpan {
  traceId: string;
  spanId: string;
  parentSpanId: string | null;
  traceState: string;
  flags: number;
  name: string;
  kind: number;
  startTimestamp: string;
  endTimestamp: string;
  durationNanoseconds: number;
  statusCode: number;
  statusMessage: string | null;
  serviceName: string;
  resourceAttributesJson: string;
  scopeName: string | null;
  scopeVersion: string | null;
  attributesJson: string;
  eventsJson: string;
  linksJson: string;
}

export interface TraceDetail {
  summary: TraceSummary;
  spans: TraceSpan[];
}

export interface TraceSearchPage {
  items: TraceSummary[];
  page: number;
  pageSize: number;
}

export interface TraceSearchFilter {
  startTimestamp?: string;
  endTimestamp?: string;
  traceId?: string;
  serviceName?: string;
  rootOperationName?: string;
  status?: TraceStatusFilter;
  minimumDurationNanoseconds?: number;
  maximumDurationNanoseconds?: number;
  page?: number;
  pageSize?: number;
}