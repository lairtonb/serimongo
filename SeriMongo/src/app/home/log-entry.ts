export class LogEntry {
  id: string;
  timestamp: Date;
  level: string;
  renderedMessage: string;
  properties: { [key: string]: any }[]
  showDetails: boolean;
  selected: boolean;
}
