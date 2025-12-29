import pdfParse from 'pdf-parse';
import mammoth from 'mammoth';

export const extractTextFromFile = async (file) => {
  const fileType = file.mimetype;
  const buffer = file.buffer;

  try {
    if (fileType === 'application/pdf') {
      const data = await pdfParse(buffer);
      return data.text;
    } else if (
      fileType === 'application/vnd.openxmlformats-officedocument.wordprocessingml.document' ||
      fileType === 'application/msword'
    ) {
      const result = await mammoth.extractRawText({ buffer });
      return result.value;
    } else if (fileType === 'text/plain') {
      return buffer.toString('utf-8');
    } else {
      throw new Error('Formato de arquivo não suportado');
    }
  } catch (error) {
    throw new Error(`Erro ao extrair texto: ${error.message}`);
  }
};


