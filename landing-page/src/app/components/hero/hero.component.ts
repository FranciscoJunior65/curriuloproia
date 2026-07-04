import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  ViewChild,
  signal
} from '@angular/core';

interface TerminalCard {
  label: string;
  description: string;
  barWidth: number;
}

@Component({
  selector: 'app-hero',
  standalone: true,
  imports: [],
  templateUrl: './hero.component.html',
  styleUrl: './hero.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeroComponent implements AfterViewInit, OnDestroy {
  @ViewChild('terminalSection') terminalSection!: ElementRef<HTMLElement>;
  @ViewChild('heroVideo') heroVideoRef!: ElementRef<HTMLVideoElement>;

  readonly command =
    'curriculoproia analyze --modules=6 --ats --brazil --realtime';
  readonly summaryText =
    '| 6 ferramentas · operação consolidada · mercado brasileiro';
  readonly cards: TerminalCard[] = [
    {
      label: 'ANÁLISE',
      description: 'score ATS · compatibilidade com a vaga',
      barWidth: 88,
    },
    {
      label: 'OTIMIZAÇÃO',
      description: '+53% callbacks · PDF e Word prontos',
      barWidth: 94,
    },
    {
      label: 'ENTREVISTA',
      description: 'simulação IA · feedback em tempo real',
      barWidth: 81,
    },
  ];

  displayedCommand = signal('');
  showCursor = signal(true);
  showSummary = signal(false);
  visibleCards = signal<number[]>([]);
  private readonly fixedVolume = 0.8;

  private observer?: IntersectionObserver;
  private timers: ReturnType<typeof setTimeout>[] = [];
  private intervals: ReturnType<typeof setInterval>[] = [];
  private animationStarted = false;

  ngAfterViewInit(): void {
    this.observer = new IntersectionObserver(
      (entries) => {
        if (entries[0]?.isIntersecting && !this.animationStarted) {
          this.animationStarted = true;
          this.startTypingAnimation();
        }
      },
      { threshold: 0.25 }
    );

    this.observer.observe(this.terminalSection.nativeElement);
    this.startVideoWithSound();
  }

  isCardVisible(index: number): boolean {
    return this.visibleCards().includes(index);
  }

  private startTypingAnimation(): void {
    let index = 0;
    this.displayedCommand.set('');
    this.showSummary.set(false);
    this.visibleCards.set([]);
    this.showCursor.set(true);

    const typeInterval = setInterval(() => {
      if (index < this.command.length) {
        this.displayedCommand.update((value) => value + this.command[index]);
        index += 1;
        return;
      }

      clearInterval(typeInterval);
      this.schedule(() => {
        this.showSummary.set(true);
        this.schedule(() => this.revealCards(), 500);
      }, 350);
    }, 42);

    this.intervals.push(typeInterval);
  }

  private revealCards(): void {
    this.cards.forEach((_, cardIndex) => {
      this.schedule(() => {
        this.visibleCards.update((cards) => [...cards, cardIndex]);
      }, cardIndex * 480);
    });

    const restartDelay = this.cards.length * 480 + 4500;
    this.schedule(() => this.resetAndRestart(), restartDelay);
  }

  private resetAndRestart(): void {
    this.showCursor.set(false);
    this.schedule(() => this.startTypingAnimation(), 700);
  }

  private schedule(fn: () => void, ms: number): void {
    const id = setTimeout(fn, ms);
    this.timers.push(id);
  }

  private startVideoWithSound(): void {
    const video = this.heroVideoRef?.nativeElement;
    if (!video) {
      return;
    }

    video.muted = false;
    video.volume = this.fixedVolume;
    void video.play().catch(() => undefined);
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
    this.timers.forEach(clearTimeout);
    this.intervals.forEach(clearInterval);
  }
}
