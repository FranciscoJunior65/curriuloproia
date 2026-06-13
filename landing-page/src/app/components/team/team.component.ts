import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

interface TeamMember {
  name: string;
  role: string;
  image: string;
  quote: string;
  imagePosition?: string;
}

@Component({
  selector: 'app-team',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './team.component.html',
  styleUrl: './team.component.css'
})
export class TeamComponent {
  readonly members: TeamMember[] = [
    {
      name: 'David Oliveira',
      role: 'CEO FOUNDER',
      image: 'assets/image/david-oliveira-ceo.png',
      imagePosition: 'center top',
      quote:
        'A IA que construímos não é prompt genérico nem template de internet. É uma plataforma calibrada para o mercado brasileiro — cada análise pensada para fazer seu currículo passar pelo ATS e impressionar o recrutador humano.'
    },
    {
      name: 'Francisco Fernandes',
      role: 'CTO',
      image: 'assets/image/francisco-fernandes-cto.png',
      imagePosition: 'center top',
      quote:
        'A engenharia por trás de cada análise — arquitetura, modelos e fluxos que transformam um PDF em um currículo competitivo em minutos. Não é teoria. É código rodando em produção, evoluindo a cada feedback real de quem busca emprego.'
    }
  ];
}
