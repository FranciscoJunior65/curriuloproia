import { supabaseAdmin } from './supabase.service.js';

/**
 * Salva um currículo importado no banco de dados
 * @param {string} userId - ID do usuário
 * @param {string} siteId - ID do site de vagas
 * @param {string} fileName - Nome do arquivo original
 * @param {string} fileType - Tipo MIME do arquivo
 * @param {string} textContent - Texto extraído do arquivo
 * @param {string} creditId - ID do crédito usado (opcional)
 * @param {object} analysisData - Dados da análise para reutilizar em exportações (opcional)
 */
export const saveImportedResume = async (userId, siteId, fileName, fileType, textContent, creditId = null, analysisData = null) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  if (!userId || !siteId) {
    console.warn('⚠️ userId ou siteId não fornecidos - não será possível salvar currículo');
    return null;
  }

  try {
    const insertData = {
      id_usuario: userId,
      id_site_vagas: siteId,
      nome_arquivo_original: fileName,
      tipo_arquivo: fileType,
      caminho_arquivo: `upload/${userId}/${Date.now()}_${fileName}`, // Caminho virtual (arquivo não é salvo fisicamente)
      conteudo_extraido: textContent,
      dados_estruturados: {
        textLength: textContent?.length || 0,
        uploadedAt: new Date().toISOString(),
        analysisData: analysisData || null
      },
      id_credito_usado: creditId
    };

    const { data, error } = await supabaseAdmin
      .from('curriculos_importados')
      .insert(insertData)
      .select()
      .single();

    if (error) {
      console.error('❌ Erro ao salvar currículo:', error);
      return null;
    }

    console.log(`✅ Currículo salvo no banco: ${data.id} (sem arquivo base64)`);
    return data.id;
  } catch (error) {
    console.error('❌ Erro ao salvar currículo:', error);
    return null;
  }
};

/**
 * Busca um currículo por ID
 */
export const getResumeById = async (resumeId) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  try {
    const { data, error } = await supabaseAdmin
      .from('curriculos_importados')
      .select('*')
      .eq('id', resumeId)
      .single();

    if (error) {
      throw error;
    }

    return data;
  } catch (error) {
    console.error('Erro ao buscar currículo:', error);
    throw error;
  }
};
