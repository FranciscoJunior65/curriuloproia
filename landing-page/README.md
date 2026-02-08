# 🚀 CurriculosPro IA - Landing Page

Landing page moderna e profissional desenvolvida em Angular 19 para o sistema CurriculosPro IA.

## ✨ Características

- **Design Moderno**: Interface clean e profissional com gradientes e animações suaves
- **Totalmente Responsivo**: Funciona perfeitamente em desktop, tablet e mobile
- **Performance Otimizada**: Carregamento rápido e animações fluidas
- **SEO Otimizado**: Meta tags configuradas para melhor indexação
- **Acessibilidade**: Estrutura semântica e navegação otimizada

## 🎨 Componentes

### 1. Navbar
- Menu fixo no topo
- Links de navegação suave (scroll)
- Menu mobile responsivo
- Botões de CTA (Call to Action)

### 2. Hero Section
- Banner principal com gradientes animados
- Cards interativos demonstrando a análise
- Elementos flutuantes com animações
- Social proof (avaliações e estatísticas)

### 3. Features
- Grid de 6 recursos principais
- Ícones personalizados com gradientes
- Cards com hover effects
- Animações escalonadas

### 4. Pricing
- 4 planos de preços (Starter, Basic, Pro, Premium)
- Plano destaque (Pro) com badge
- Plano especial (Currículo em Inglês)
- Cálculos de economia visíveis
- Garantia de satisfação

### 5. Testimonials
- 6 depoimentos de clientes
- Avaliações com estrelas
- Cards com gradientes únicos
- Informações de profissão

### 6. Footer
- Links organizados por categoria
- Redes sociais
- Informações legais
- Selo de pagamento seguro

## 🛠️ Tecnologias

- **Angular 19**: Framework principal
- **Tailwind CSS**: Estilização e responsividade
- **Angular Material**: Componentes base
- **TypeScript**: Tipagem e lógica

## 📦 Instalação

```bash
# Já instalado! Acesse a pasta
cd landing-page

# Instalar dependências (já instaladas)
npm install

# Rodar em desenvolvimento
ng serve

# Build para produção
ng build --configuration production
```

## 🌐 URLs e Configuração

### URLs Importantes

- **Desenvolvimento**: `http://localhost:4200`
- **Sistema Principal**: `http://localhost:4200/login` (link nos botões)

### Configurar Links

Os botões "Começar Grátis", "Entrar", etc. apontam para `/login`. Para mudar:

1. Edite os arquivos de componentes (`.html`)
2. Procure por `href="/login"`
3. Substitua pela URL desejada

### Integração com o Sistema Principal

Para integrar com o sistema principal em `frontend/`:

**Opção 1: Hospedar Separadamente**
- Landing page em: `https://curriculospro.ai`
- Sistema em: `https://app.curriculospro.ai`

**Opção 2: Mesma Aplicação**
- Copiar componentes da landing para `frontend/src/app/components/`
- Adicionar rota no `app.routes.ts`:
```typescript
{ path: '', component: LandingPageComponent },
{ path: 'app', component: AnalyzerComponent, canActivate: [AuthGuard] }
```

## 🎨 Customização

### Cores

As cores principais estão em `tailwind.config.js`:

```javascript
colors: {
  primary: { /* azul */ },
  secondary: { /* roxo */ }
}
```

### Conteúdo

Para alterar textos, imagens e preços:

- **Textos**: Edite os arquivos `.html` de cada componente
- **Preços**: `pricing.component.html`
- **Depoimentos**: `testimonials.component.html`
- **Features**: `features.component.html`

### Animações

Animações estão em `styles.css` e `tailwind.config.js`:

```css
.animate-fade-in { /* fade in */ }
.animate-slide-up { /* slide up */ }
.animate-float { /* floating effect */ }
```

## 📱 Responsividade

Breakpoints do Tailwind:
- `sm`: 640px
- `md`: 768px
- `lg`: 1024px
- `xl`: 1280px

## 🚀 Deploy

### Vercel (Recomendado)

```bash
# Instalar Vercel CLI
npm i -g vercel

# Deploy
vercel
```

### Netlify

```bash
# Build
ng build --configuration production

# Arraste a pasta dist/landing-page para netlify.com
```

### GitHub Pages

```bash
# Instalar angular-cli-ghpages
ng add angular-cli-ghpages

# Deploy
ng deploy --base-href=/landing-page/
```

## 📊 Performance

- **Lighthouse Score**: 90+
- **First Contentful Paint**: < 1.5s
- **Time to Interactive**: < 3.5s

## 🔒 SEO

Meta tags configuradas em `index.html`:
- Title
- Description
- Keywords
- Open Graph (Facebook)
- Twitter Cards

## 📄 Licença

Propriedade do CurriculosPro IA © 2026

---

**Desenvolvido com ❤️ usando Angular 19 e Tailwind CSS**
