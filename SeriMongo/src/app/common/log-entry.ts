export class LogEntry {
  id: string;
  timestamp: Date | string;
  level: string;
  renderedMessage: string;
  properties: Property[]
  showDetails: boolean;
  selected: boolean;
}

interface Property {
  [key: string]: string;
}
