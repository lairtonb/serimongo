import { CommonModule } from '@angular/common';
import { Component, ElementRef, EventEmitter, HostListener, Input, Output, inject, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { ionChevronBackOutline, ionChevronForwardOutline, ionCloseSharp, ionHelpCircleOutline } from '@ng-icons/ionicons';

interface HelpSection {
  id: string;
  label: string;
}

@Component({
  selector: 'app-help-drawer',
  standalone: true,
  imports: [CommonModule, NgIcon],
  providers: [provideIcons({ ionChevronBackOutline, ionChevronForwardOutline, ionCloseSharp, ionHelpCircleOutline })],
  templateUrl: './help-drawer.component.html',
  styleUrls: ['./help-drawer.component.css']
})
export class HelpDrawerComponent {
  @Input() open = false;
  @Output() closed = new EventEmitter<void>();

  readonly tocCollapsed = signal(false);
  readonly activeSection = signal('help-overview');
  readonly sections: HelpSection[] = [
    { id: 'help-overview', label: 'Visão geral' },
    { id: 'help-search', label: 'Busca' },
    { id: 'help-logql', label: 'LogQL' },
    { id: 'help-filters', label: 'Filtros rápidos' },
    { id: 'help-table', label: 'Tabela de logs' },
    { id: 'help-details', label: 'Detalhes do log' },
    { id: 'help-copy', label: 'Copiar informações' },
    { id: 'help-tail', label: 'Tail e pausa' }
  ];

  private readonly elementRef = inject(ElementRef<HTMLElement>);

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open) {
      this.close();
    }
  }

  close(): void {
    this.closed.emit();
  }

  toggleToc(): void {
    this.tocCollapsed.update(collapsed => !collapsed);
  }

  scrollToSection(event: Event, sectionId: string): void {
    event.preventDefault();
    this.activeSection.set(sectionId);

    const section = this.elementRef.nativeElement.querySelector(`#${sectionId}`) as HTMLElement | null;
    section?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
}
