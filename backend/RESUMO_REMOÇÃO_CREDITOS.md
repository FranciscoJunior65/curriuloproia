# Remoção do Campo Créditos de perfis_usuarios

## ✅ Mudanças Realizadas

### 1. Banco de Dados
- **Campo `creditos` removido** da tabela `perfis_usuarios`
- Os créditos agora são **calculados dinamicamente** da tabela `creditos`

### 2. Cálculo de Créditos
- **Antes**: Campo `creditos` em `perfis_usuarios` (podia ficar desatualizado)
- **Agora**: Conta créditos disponíveis na tabela `creditos` onde `usado = false`

### 3. Funções Atualizadas

#### `getUserProfile()` / `getUserProfileByEmail()`
- Agora calcula créditos dinamicamente via `getAvailableCredits()`
- Retorna créditos sempre atualizados

#### `addCreditsToUser()`
- Não atualiza mais `perfis_usuarios.creditos`
- Os créditos são criados via `createPurchase()` diretamente na tabela `creditos`

#### `deductCreditsFromUser()`
- Não atualiza mais `perfis_usuarios.creditos`
- Os créditos são marcados como usados via `recordCreditUsage()`

#### `mapProfileToEnglish()`
- Agora é **async** (calcula créditos dinamicamente)
- Busca créditos disponíveis da tabela `creditos`

### 4. Admin Controller
- Estatísticas agora calculam créditos da tabela `creditos`
- Mostra: total, usados, disponíveis

## 📋 Scripts SQL

Execute `REMOVER_CAMPO_CREDITOS.sql` para:
- Remover a coluna `creditos` de `perfis_usuarios`

## 🔍 Como Funciona Agora

### Para obter créditos do usuário:
```javascript
const credits = await getAvailableCredits(userId);
// Retorna: número de créditos onde usado = false
```

### Ao criar compra:
1. Cria 1 linha em `compras`
2. Cria N linhas em `creditos` (1 por crédito, `usado = false`)
3. **NÃO atualiza** `perfis_usuarios.creditos`

### Ao usar crédito:
1. Busca créditos disponíveis (`usado = false`)
2. Marca como usado (`usado = true`)
3. **NÃO atualiza** `perfis_usuarios.creditos`

## ✅ Vantagens

1. **Sempre atualizado**: Créditos sempre refletem a realidade
2. **Sem inconsistências**: Não há mais campo cache que pode ficar desatualizado
3. **Rastreabilidade**: Cada crédito tem sua própria linha
4. **Análise facilitada**: Fácil ver quantos créditos de cada compra foram usados

## ⚠️ Importante

- Todas as funções que retornam perfil agora são **async** (porque calculam créditos)
- O campo `credits` no objeto retornado é sempre calculado dinamicamente
- Não há mais atualização do campo `creditos` em `perfis_usuarios`

