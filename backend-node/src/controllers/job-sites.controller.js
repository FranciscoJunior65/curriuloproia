import { getActiveJobSites } from '../services/job-sites.service.js';

export const listJobSites = async (req, res) => {
  try {
    const sites = await getActiveJobSites();
    
    res.json({
      success: true,
      sites: sites || []
    });
  } catch (error) {
    console.error('Erro ao listar sites de vagas:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao listar sites de vagas',
      message: error.message
    });
  }
};
