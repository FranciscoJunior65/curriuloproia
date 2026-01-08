# Nova Estrutura de Créditos

## 📊 Estrutura das Tabelas

### 1. `perfis_usuarios`
Armazena os dados dos usuários, incluindo um campo `creditos` que é um **cache** do total de créditos disponíveis.

### 2. `compras`
Armazena os detalhes de cada compra/transação.
- **1 linha por compra** (não por crédito)
- Campo `quantidade_creditos` indica quantos créditos foram comprados nesta transação

### 3. `creditos` (NOVA)
Armazena cada crédito individualmente.
- **1 linha por crédito**
- Vinculado à compra através de `id_compra`
- Campo `usado` indica se o crédito já foi utilizado
- Campo `usado_em` indica quando foi usado
- Campos `tipo_acao` e `nome_arquivo_curriculo` preenchidos quando usado

## 🔄 Fluxo de Dados

### Ao Comprar Créditos:
1. Cria 1 linha em `compras` com os detalhes da compra
2. Cria N linhas em `creditos` (1 por crédito comprado), todas com `usado = false`
3. Atualiza `perfis_usuarios.creditos` (cache)

### Ao Usar Crédito:
1. Busca créditos disponíveis (`usado = false`) do usuário
2. Marca o crédito como usado (`usado = true`, preenche `usado_em`, `tipo_acao`, etc)
3. Atualiza `perfis_usuarios.creditos` (cache)

## 📈 Vantagens

1. **Rastreabilidade completa**: Cada crédito tem sua própria linha
2. **Análise facilitada**: 
   - Quantos créditos foram criados por compra
   - Quais foram usados e quais não
   - Quando cada crédito foi usado
3. **Histórico detalhado**: Cada uso de crédito fica registrado
4. **Performance**: Campo `creditos` em `perfis_usuarios` como cache

## 🔍 Consultas Úteis

### Créditos disponíveis de um usuário:
```sql
SELECT COUNT(*) 
FROM creditos 
WHERE id_usuario = 'user-id' AND usado = false;
```

### Créditos de uma compra específica:
```sql
SELECT 
  COUNT(*) as total,
  COUNT(*) FILTER (WHERE usado = true) as usados,
  COUNT(*) FILTER (WHERE usado = false) as disponiveis
FROM creditos 
WHERE id_compra = 'compra-id';
```

### Histórico de uso de créditos:
```sql
SELECT * 
FROM creditos 
WHERE id_usuario = 'user-id' AND usado = true
ORDER BY usado_em DESC;
```

## 📝 Scripts

Execute `ESTRUTURA_CREDITOS_MELHORADA.sql` no Supabase para:
1. Criar a nova tabela `creditos`
2. Migrar dados existentes (se houver)
3. Remover tabela antiga `uso_creditos`

