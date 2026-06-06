import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-faq',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './faq.component.html',
})
export class FaqComponent {
  openIndex: number | null = null;

  faqs = [
    {
      question: 'Como funciona a análise de currículo com IA?',
      answer: 'Você faz upload do seu currículo e informa a vaga desejada. Nossa IA analisa compatibilidade, identifica pontos fortes e fracos, sugere melhorias e gera um score — tudo em cerca de 5 minutos.',
    },
    {
      question: 'Preciso pagar mensalidade?',
      answer: 'Não. O CurriculosPro IA funciona com créditos avulsos. Você compra o pacote que precisa e usa quando quiser, sem assinatura recorrente.',
    },
    {
      question: 'A simulação de entrevista é ao vivo?',
      answer: 'Sim. Você pratica entrevistas com IA em tempo real, recebendo perguntas personalizadas com base no seu currículo e feedback detalhado sobre cada resposta.',
    },
    {
      question: 'O currículo otimizado passa no ATS?',
      answer: 'Sim. A otimização inclui palavras-chave específicas da vaga e formatação compatível com os principais sistemas de triagem (ATS) usados por recrutadores.',
    },
    {
      question: 'Posso traduzir meu currículo para inglês?',
      answer: 'Sim. Oferecemos tradução profissional com adaptação cultural para o mercado internacional — não é tradução literal, é adaptação profissional.',
    },
    {
      question: 'O pagamento é seguro?',
      answer: 'Sim. Todos os pagamentos são processados via Stripe, com criptografia de ponta a ponta. Não armazenamos dados de cartão.',
    },
  ];

  toggle(index: number): void {
    this.openIndex = this.openIndex === index ? null : index;
  }
}
