export interface LogEntry {
  id: string;
  logId?: string;
  timestamp: Date | string;
  level: string;
  renderedMessage: string;
  exception?: string | null;
  properties: Record<string, unknown>;
  showDetails?: boolean;
  selected?: boolean;
}
