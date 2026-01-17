import axios from 'axios';
import * as cheerio from 'cheerio';

/**
 * Serviço para buscar vagas em diferentes sites de emprego
 * Baseado na análise do currículo e site selecionado
 */

/**
 * Extrai termos de busca relevantes da análise do currículo
 */
export const extractSearchTerms = (analysis) => {
  const terms = [];
  
  // Adiciona habilidades técnicas
  if (Array.isArray(analysis.habilidades)) {
    terms.push(...analysis.habilidades);
  }
  
  // Adiciona experiência mencionada
  if (analysis.experiencia) {
    // Extrai tecnologias mencionadas na experiência
    const techKeywords = extractTechKeywords(analysis.experiencia);
    terms.push(...techKeywords);
  }
  
  // Adiciona pontos fortes relevantes
  if (Array.isArray(analysis.pontosFortes)) {
    const relevantStrengths = analysis.pontosFortes
      .filter(ponto => ponto.length < 50) // Apenas pontos concisos
      .map(ponto => extractKeywords(ponto))
      .flat();
    terms.push(...relevantStrengths);
  }
  
  // Remove duplicatas e retorna os termos mais relevantes
  return [...new Set(terms)].slice(0, 10);
};

/**
 * Extrai palavras-chave técnicas de um texto
 */
const extractTechKeywords = (text) => {
  const techPatterns = [
    /\b(JavaScript|TypeScript|Python|Java|C#|C\+\+|PHP|Ruby|Go|Rust|Swift|Kotlin)\b/gi,
    /\b(React|Angular|Vue|Node\.js|Express|Django|Flask|Spring|Laravel|Rails)\b/gi,
    /\b(SQL|MySQL|PostgreSQL|MongoDB|Redis|Oracle|SQL Server)\b/gi,
    /\b(AWS|Azure|GCP|Docker|Kubernetes|Jenkins|Git|GitHub|GitLab)\b/gi,
    /\b(HTML|CSS|SASS|LESS|Bootstrap|Tailwind)\b/gi,
    /\b(\.NET|ASP\.NET|Entity Framework|Hibernate|JPA)\b/gi,
    /\b(Agile|Scrum|Kanban|DevOps|CI\/CD|TDD|BDD)\b/gi
  ];
  
  const keywords = [];
  techPatterns.forEach(pattern => {
    const matches = text.match(pattern);
    if (matches) {
      keywords.push(...matches.map(m => m.toLowerCase()));
    }
  });
  
  return [...new Set(keywords)];
};

/**
 * Extrai palavras-chave relevantes de um texto
 */
const extractKeywords = (text) => {
  // Remove palavras comuns e mantém apenas termos relevantes
  const stopWords = ['de', 'da', 'do', 'em', 'para', 'com', 'por', 'a', 'o', 'e', 'é', 'são', 'foi', 'ser', 'ter', 'ter', 'mais', 'muito', 'bem', 'mais', 'pode', 'deve'];
  const words = text.toLowerCase()
    .replace(/[^\w\s]/g, ' ')
    .split(/\s+/)
    .filter(word => word.length > 3 && !stopWords.includes(word));
  
  return words.slice(0, 5); // Retorna até 5 palavras-chave
};

/**
 * Busca vagas no LinkedIn (modo gratuito - busca pública)
 */
export const searchLinkedInJobs = async (searchTerms, location = 'Brasil') => {
  try {
    // LinkedIn tem uma API pública limitada, vamos usar busca web
    // Nota: LinkedIn pode bloquear requisições automatizadas
    const query = searchTerms.join(' OR ');
    const searchUrl = `https://www.linkedin.com/jobs/search/?keywords=${encodeURIComponent(query)}&location=${encodeURIComponent(location)}`;
    
    console.log(`🔍 Buscando vagas no LinkedIn: ${query}`);
    
    // Para implementação real, seria necessário:
    // 1. Usar uma API de scraping (como Puppeteer/Playwright)
    // 2. Ou usar uma API oficial do LinkedIn (se disponível)
    // 3. Ou integrar com serviços de terceiros
    
    // Por enquanto, retornamos uma estrutura de exemplo
    return {
      site: 'LinkedIn',
      url: searchUrl,
      jobs: [],
      message: 'Busca no LinkedIn requer autenticação ou API. Retornando URL de busca.',
      searchTerms: searchTerms
    };
  } catch (error) {
    console.error('❌ Erro ao buscar vagas no LinkedIn:', error);
    throw new Error(`Erro ao buscar vagas no LinkedIn: ${error.message}`);
  }
};

/**
 * Busca vagas no Catho
 */
export const searchCathoJobs = async (searchTerms, location = 'Brasil') => {
  try {
    const query = searchTerms.join(' ');
    const searchUrl = `https://www.catho.com.br/vagas/?q=${encodeURIComponent(query)}&localizacao=${encodeURIComponent(location)}`;
    
    console.log(`🔍 Buscando vagas no Catho: ${query}`);
    
    // Catho permite visualização de algumas vagas sem login
    // Vamos tentar fazer scraping básico
    try {
      const response = await axios.get(searchUrl, {
        headers: {
          'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36'
        },
        timeout: 10000
      });
      
      const $ = cheerio.load(response.data);
      const jobs = [];
      
      // Tenta encontrar vagas na página (estrutura pode variar)
      $('.job-card, .vaga-item, [data-testid*="job"]').each((index, element) => {
        if (index >= 10) return false; // Limita a 10 vagas
        
        const title = $(element).find('h2, h3, .job-title, .vaga-titulo').first().text().trim();
        const company = $(element).find('.company, .empresa, [data-testid*="company"]').first().text().trim();
        const location = $(element).find('.location, .localizacao, [data-testid*="location"]').first().text().trim();
        const link = $(element).find('a').first().attr('href');
        
        if (title) {
          jobs.push({
            title,
            company: company || 'Não informado',
            location: location || 'Não informado',
            url: link ? (link.startsWith('http') ? link : `https://www.catho.com.br${link}`) : searchUrl,
            site: 'Catho'
          });
        }
      });
      
      return {
        site: 'Catho',
        url: searchUrl,
        jobs: jobs.length > 0 ? jobs : [],
        message: jobs.length > 0 ? `${jobs.length} vagas encontradas` : 'Nenhuma vaga encontrada na busca automatizada',
        searchTerms: searchTerms
      };
    } catch (scrapingError) {
      console.warn('⚠️ Erro no scraping do Catho, retornando URL:', scrapingError.message);
      return {
        site: 'Catho',
        url: searchUrl,
        jobs: [],
        message: 'Não foi possível fazer scraping automático. Use o link fornecido para buscar manualmente.',
        searchTerms: searchTerms
      };
    }
  } catch (error) {
    console.error('❌ Erro ao buscar vagas no Catho:', error);
    throw new Error(`Erro ao buscar vagas no Catho: ${error.message}`);
  }
};

/**
 * Busca vagas no Indeed
 */
export const searchIndeedJobs = async (searchTerms, location = 'Brasil') => {
  try {
    const query = searchTerms.join(' ');
    const searchUrl = `https://br.indeed.com/jobs?q=${encodeURIComponent(query)}&l=${encodeURIComponent(location)}`;
    
    console.log(`🔍 Buscando vagas no Indeed: ${query}`);
    
    try {
      const response = await axios.get(searchUrl, {
        headers: {
          'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'
        },
        timeout: 10000
      });
      
      const $ = cheerio.load(response.data);
      const jobs = [];
      
      $('.job_seen_beacon, .jobsearch-SerpJobCard').each((index, element) => {
        if (index >= 10) return false;
        
        const title = $(element).find('h2 a, .jobTitle a').first().text().trim();
        const company = $(element).find('.companyName, .company').first().text().trim();
        const location = $(element).find('.companyLocation, .location').first().text().trim();
        const link = $(element).find('h2 a, .jobTitle a').first().attr('href');
        
        if (title) {
          jobs.push({
            title,
            company: company || 'Não informado',
            location: location || 'Não informado',
            url: link ? (link.startsWith('http') ? link : `https://br.indeed.com${link}`) : searchUrl,
            site: 'Indeed'
          });
        }
      });
      
      return {
        site: 'Indeed',
        url: searchUrl,
        jobs: jobs.length > 0 ? jobs : [],
        message: jobs.length > 0 ? `${jobs.length} vagas encontradas` : 'Nenhuma vaga encontrada',
        searchTerms: searchTerms
      };
    } catch (scrapingError) {
      return {
        site: 'Indeed',
        url: searchUrl,
        jobs: [],
        message: 'Use o link fornecido para buscar manualmente.',
        searchTerms: searchTerms
      };
    }
  } catch (error) {
    console.error('❌ Erro ao buscar vagas no Indeed:', error);
    throw new Error(`Erro ao buscar vagas no Indeed: ${error.message}`);
  }
};

/**
 * Busca vagas genérica (retorna URL de busca)
 */
export const searchGenericJobs = async (siteName, searchTerms, location = 'Brasil') => {
  const query = searchTerms.join(' ');
  const searchUrl = `https://www.google.com/search?q=${encodeURIComponent(`${query} vagas ${siteName} ${location}`)}`;
  
  return {
    site: siteName,
    url: searchUrl,
    jobs: [],
    message: `Busca genérica para ${siteName}. Use o link fornecido.`,
    searchTerms: searchTerms
  };
};

/**
 * Extrai dados completos de uma vaga (descrição, requisitos, salário)
 */
export const extractJobDetails = async (jobUrl, siteName) => {
  try {
    const response = await axios.get(jobUrl, {
      headers: {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36'
      },
      timeout: 10000
    });
    
    const $ = cheerio.load(response.data);
    const details = {
      description: '',
      requirements: [],
      salary: '',
      contractType: '',
      experienceLevel: ''
    };
    
    // Extrai descrição (varia por site)
    if (siteName.toLowerCase().includes('catho')) {
      details.description = $('.job-description, .descricao-vaga, [data-testid*="description"]').first().text().trim();
      details.salary = $('.salary, .salario, [data-testid*="salary"]').first().text().trim();
    } else if (siteName.toLowerCase().includes('indeed')) {
      details.description = $('#jobDescriptionText, .jobsearch-jobDescriptionText').first().text().trim();
      details.salary = $('.salaryText, [data-testid*="salary"]').first().text().trim();
    } else {
      // Genérico
      details.description = $('.description, .job-description, [class*="description"]').first().text().trim();
    }
    
    // Extrai requisitos (tenta encontrar listas)
    $('ul li, ol li').each((index, element) => {
      const text = $(element).text().trim().toLowerCase();
      if (text.includes('requisito') || text.includes('exigência') || text.includes('necessário')) {
        details.requirements.push($(element).text().trim());
      }
    });
    
    return details;
  } catch (error) {
    console.warn(`⚠️ Erro ao extrair detalhes da vaga ${jobUrl}:`, error.message);
    return {
      description: '',
      requirements: [],
      salary: '',
      contractType: '',
      experienceLevel: ''
    };
  }
};

/**
 * Busca vagas com múltiplas combinações e extrai dados completos
 */
export const searchJobsAdvanced = async (siteId, resumeText, analysis, location = 'Brasil', userId = null, resumeId = null) => {
  try {
    // Importa serviços necessários
    const { getJobSiteById } = await import('./job-sites.service.js');
    const { generateSearchKeywordsWithAI, generateSearchCombinations, calculateCompatibilityScore } = await import('./job-search-ai.service.js');
    const { saveFoundJobs } = await import('./job-search-db.service.js');
    
    // Busca informações do site
    const site = await getJobSiteById(siteId);
    if (!site) {
      throw new Error('Site de vagas não encontrado');
    }
    
    console.log(`🔍 Iniciando busca avançada no ${site.nome}...`);
    
    // 1. Gera palavras-chave otimizadas com IA
    const keywords = await generateSearchKeywordsWithAI(resumeText, analysis, site);
    console.log(`✅ ${keywords.length} palavras-chave geradas`);
    
    // 2. Gera combinações de busca
    const searchCombinations = generateSearchCombinations(keywords, 8);
    console.log(`✅ ${searchCombinations.length} combinações de busca criadas`);
    
    // 3. Faz múltiplas buscas
    const allJobs = [];
    const siteName = site.nome.toLowerCase();
    
    for (const combination of searchCombinations) {
      try {
        let searchResults;
        
        if (siteName.includes('catho')) {
          searchResults = await searchCathoJobs(combination, location);
        } else if (siteName.includes('indeed')) {
          searchResults = await searchIndeedJobs(combination, location);
        } else if (siteName.includes('linkedin')) {
          searchResults = await searchLinkedInJobs(combination, location);
        } else {
          searchResults = await searchGenericJobs(site.nome, combination, location);
        }
        
        if (searchResults.jobs && searchResults.jobs.length > 0) {
          // Extrai detalhes completos de cada vaga
          for (const job of searchResults.jobs) {
            // Evita duplicatas
            const isDuplicate = allJobs.some(j => j.url === job.url || (j.title === job.title && j.company === job.company));
            if (!isDuplicate) {
              // Extrai detalhes adicionais
              try {
                const details = await extractJobDetails(job.url, site.nome);
                job.description = details.description;
                job.requirements = details.requirements;
                job.salary = details.salary;
                job.contractType = details.contractType;
                job.experienceLevel = details.experienceLevel;
              } catch (detailError) {
                console.warn(`⚠️ Erro ao extrair detalhes da vaga ${job.url}:`, detailError.message);
              }
              
              // Calcula score de compatibilidade
              const compatibility = calculateCompatibilityScore(job, analysis, keywords);
              job.compatibilityScore = compatibility.score;
              job.matchedKeywords = compatibility.matchedKeywords;
              
              allJobs.push(job);
            }
          }
        }
        
        // Pequeno delay entre buscas para evitar bloqueio
        await new Promise(resolve => setTimeout(resolve, 1000));
        
      } catch (searchError) {
        console.warn(`⚠️ Erro na busca com combinação ${combination.join(' ')}:`, searchError.message);
        continue;
      }
    }
    
    // 4. Remove duplicatas e ordena por score
    const uniqueJobs = [];
    const seenUrls = new Set();
    
    for (const job of allJobs) {
      const key = `${job.url}_${job.title}_${job.company}`;
      if (!seenUrls.has(key)) {
        seenUrls.add(key);
        uniqueJobs.push(job);
      }
    }
    
    // Ordena por score de compatibilidade
    uniqueJobs.sort((a, b) => (b.compatibilityScore || 0) - (a.compatibilityScore || 0));
    
    console.log(`✅ ${uniqueJobs.length} vagas únicas encontradas`);
    
    // 5. Salva no banco de dados se userId e resumeId foram fornecidos
    if (userId && resumeId && uniqueJobs.length > 0) {
      try {
        await saveFoundJobs(userId, resumeId, siteId, uniqueJobs);
        console.log(`✅ ${uniqueJobs.length} vagas salvas no banco de dados`);
      } catch (saveError) {
        console.error('❌ Erro ao salvar vagas no banco:', saveError);
        // Continua mesmo se não conseguir salvar
      }
    }
    
    return {
      site: site.nome,
      url: site.url_base,
      jobs: uniqueJobs.slice(0, 50), // Limita a 50 vagas
      totalFound: uniqueJobs.length,
      searchKeywords: keywords,
      searchCombinations: searchCombinations.length,
      message: `${uniqueJobs.length} vagas encontradas após ${searchCombinations.length} buscas`
    };
    
  } catch (error) {
    console.error('❌ Erro na busca avançada:', error);
    throw new Error(`Erro ao buscar vagas: ${error.message}`);
  }
};

/**
 * Busca vagas baseado no site selecionado e análise do currículo
 * (Mantido para compatibilidade, mas agora usa busca avançada)
 */
export const searchJobsBySite = async (siteId, analysis, location = 'Brasil', resumeText = null, userId = null, resumeId = null) => {
  // Se resumeText foi fornecido, usa busca avançada
  if (resumeText) {
    return await searchJobsAdvanced(siteId, resumeText, analysis, location, userId, resumeId);
  }
  
  // Caso contrário, usa busca simples (compatibilidade)
  try {
    const { getJobSiteById } = await import('./job-sites.service.js');
    
    const site = await getJobSiteById(siteId);
    if (!site) {
      throw new Error('Site de vagas não encontrado');
    }
    
    const searchTerms = extractSearchTerms(analysis);
    console.log(`🔍 Buscando vagas no ${site.nome} com termos: ${searchTerms.join(', ')}`);
    
    const siteName = site.nome.toLowerCase();
    
    if (siteName.includes('linkedin')) {
      return await searchLinkedInJobs(searchTerms, location);
    } else if (siteName.includes('catho')) {
      return await searchCathoJobs(searchTerms, location);
    } else if (siteName.includes('indeed')) {
      return await searchIndeedJobs(searchTerms, location);
    } else {
      return await searchGenericJobs(site.nome, searchTerms, location);
    }
  } catch (error) {
    console.error('❌ Erro ao buscar vagas:', error);
    throw new Error(`Erro ao buscar vagas: ${error.message}`);
  }
};
