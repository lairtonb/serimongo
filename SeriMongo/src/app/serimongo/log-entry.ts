export class LogEntry {
  id: string;
  timestamp: Date | string;
  level: string;
  renderedMessage: string;s
  properties: Property[]
  showDetails: boolean;
  selected: boolean;
}

interface Property {
  [key: string]: string;
}
