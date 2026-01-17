import { generateCoverLetter, generateCoverLetterPDF } from '../services/cover-letter.service.js';
import { getUserProfile } from '../services/supabase.service.js';
import jwt from 'jsonwebtoken';

export const generateCoverLetterAndPDF = async (req, res) => {
  const startTime = Date.now();
  
  try {
    const { resumeText, analysis, siteId } = req.body;
    
    // Obtém o nome do usuário do token JWT
    let userName = 'carta-apresentacao';
    try {
      const token = req.headers.authorization?.replace('Bearer ', '');
      if (token) {
        const JWT_SECRET = process.env.JWT_SECRET || 'seu_secret_key_super_seguro_aqui_mude_em_producao';
        const decoded = jwt.verify(token, JWT_SECRET);
        const userId = decoded.userId;
        const profile = await getUserProfile(userId);
        if (profile && (profile.name || profile.nome)) {
          // Remove caracteres especiais e espaços do nome para usar no arquivo
          userName = (profile.name || profile.nome)
            .normalize('NFD')
            .replace(/[\u0300-\u036f]/g, '') // Remove acentos
            .replace(/[^a-zA-Z0-9\s]/g, '') // Remove caracteres especiais
            .replace(/\s+/g, '-') // Substitui espaços por hífen
            .toLowerCase();
          console.log(`👤 Nome do usuário para arquivo: ${userName}`);
        }
      }
    } catch (error) {
      console.warn('⚠️ Não foi possível obter nome do usuário, usando nome padrão:', error.message);
    }

    // Validação
    if (!resumeText || !analysis) {
      return res.status(400).json({
        success: false,
        error: 'Dados incompletos',
        message: 'É necessário fornecer resumeText e analysis'
      });
    }

    if (!analysis.pontosFortes || !analysis.experiencia) {
      return res.status(400).json({
        success: false,
        error: 'Análise inválida',
        message: 'A análise deve conter pontosFortes e experiencia'
      });
    }

    console.log('📝 Gerando carta de apresentação...');
    console.log('📋 Dados recebidos:', {
      hasResumeText: !!resumeText,
      hasAnalysis: !!analysis,
      siteId: siteId || 'NÃO FORNECIDO'
    });
    if (siteId) {
      console.log(`📍 Site de vagas selecionado: ${siteId}`);
    } else {
      console.warn('⚠️ ATENÇÃO: Nenhum site de vagas foi fornecido! A carta será genérica.');
    }

    // Gera a carta de apresentação
    const coverLetter = await generateCoverLetter(resumeText, analysis, siteId || null);

    console.log('📄 Gerando PDF da carta...');

    // Gera o PDF
    const pdfBuffer = await generateCoverLetterPDF(coverLetter);

    const processingTime = ((Date.now() - startTime) / 1000).toFixed(2);
    console.log(`✨ Carta de apresentação gerada em ${processingTime}s`);

    // Define headers para download com nome do usuário
    const fileName = `${userName}-carta-apresentacao.pdf`;
    res.setHeader('Content-Type', 'application/pdf');
    res.setHeader('Content-Disposition', `attachment; filename="${fileName}"`);
    res.setHeader('Content-Length', pdfBuffer.length);

    res.send(pdfBuffer);

  } catch (error) {
    const processingTime = ((Date.now() - startTime) / 1000).toFixed(2);
    console.error(`❌ Erro ao gerar carta de apresentação (${processingTime}s):`, error);
    
    res.status(500).json({
      success: false,
      error: 'Erro ao gerar carta de apresentação',
      message: error.message || 'Ocorreu um erro inesperado ao gerar a carta de apresentação'
    });
  }
};
