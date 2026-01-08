# Solução para Erro "Could not find the table 'public.user_profiles'"

## 🔍 Problema
O erro indica que o Supabase ainda está tentando acessar a tabela antiga `user_profiles` que não existe mais.

## ✅ Solução

### Passo 1: Execute o Script SQL de Limpeza
Execute o arquivo `VERIFICAR_E_CORRIGIR_TABELAS.sql` no Supabase SQL Editor:

1. Abra o Supabase Dashboard
2. Vá em SQL Editor
3. Cole e execute o conteúdo de `VERIFICAR_E_CORRIGIR_TABELAS.sql`
4. Isso irá:
   - Remover a tabela antiga `user_profiles` se existir
   - Garantir que apenas as tabelas em português existam
   - Criar as tabelas se não existirem

### Passo 2: Limpar Cache do Supabase
O Supabase pode ter cache do schema. Para limpar:

1. No Supabase Dashboard, vá em **Settings** > **API**
2. Role até **Schema Cache**
3. Clique em **Clear Cache** ou **Refresh Schema**

### Passo 3: Verificar Tabelas
Execute este SQL para verificar:

```sql
SELECT table_name 
FROM information_schema.tables 
WHERE table_schema = 'public' 
  AND table_name IN ('perfis_usuarios', 'compras', 'creditos', 'user_profiles')
ORDER BY table_name;
```

**Resultado esperado:**
- ✅ `perfis_usuarios` - deve existir
- ✅ `compras` - deve existir  
- ✅ `creditos` - deve existir
- ❌ `user_profiles` - NÃO deve existir

### Passo 4: Reiniciar o Backend
Após executar o SQL, reinicie o servidor backend:

```bash
# Pare o servidor (Ctrl+C)
# Inicie novamente
npm start
```

## 📋 Checklist de Verificação

- [ ] Script `VERIFICAR_E_CORRIGIR_TABELAS.sql` executado
- [ ] Tabela `user_profiles` removida
- [ ] Tabelas `perfis_usuarios`, `compras`, `creditos` existem
- [ ] Cache do Supabase limpo
- [ ] Backend reiniciado
- [ ] Teste de login realizado

## 🔧 Se o Problema Persistir

1. **Verifique os logs do backend** - pode mostrar qual função está tentando acessar a tabela antiga
2. **Verifique se há views ou funções SQL** que ainda referenciam `user_profiles`
3. **Verifique o código** - execute `grep -r "user_profiles" backend/src` para encontrar referências

## 📝 Nota
Todos os arquivos do backend já foram atualizados para usar `perfis_usuarios`. O problema é provavelmente cache do Supabase ou a tabela antiga ainda existindo no banco.

