import { getAllUsers } from '../models/user.model.js';
import { supabaseAdmin, getAllPurchases, getSalesStats } from '../services/supabase.service.js';
import { userHasCredits } from '../services/supabase.service.js';
import { getAIUsageStats } from '../services/ai-usage.service.js';
import { getJobSiteUsageStats, getJobSiteRanking, getJobSiteDetailedStats } from '../services/analytics.service.js';

/**
 * Obtém estatísticas gerais do sistema
 */
export const getDashboardStats = async (req, res) => {
  try {
    // Se Supabase estiver configurado, usa ele
    if (supabaseAdmin) {
      try {
        // Total de usuários
        const { count: totalUsers } = await supabaseAdmin
          .from('perfis_usuarios')
          .select('*', { count: 'exact', head: true });

        // Total de créditos: conta todos os créditos criados na tabela creditos
        const { count: totalCredits } = await supabaseAdmin
          .from('creditos')
          .select('*', { count: 'exact', head: true });

        // Total de créditos usados
        const { count: creditsUsed } = await supabaseAdmin
          .from('creditos')
          .select('*', { count: 'exact', head: true })
          .eq('usado', true);

        // Total de créditos disponíveis
        const { count: creditsAvailable } = await supabaseAdmin
          .from('creditos')
          .select('*', { count: 'exact', head: true })
          .eq('usado', false);

        // Estima análises realizadas (usuários com ultima_analise preenchido)
        const { data: usersData } = await supabaseAdmin
          .from('perfis_usuarios')
          .select('criado_em, ultima_analise');
        const analysesPerformed = usersData?.filter(u => u.ultima_analise).length || 0;

        // Calcula faturamento estimado (assumindo que cada crédito foi comprado)
        // Plano único: R$ 9,90, Pacote 3: R$ 24,90 (média ~R$ 8,30 por crédito)
        const avgPricePerCredit = 8.30;
        const estimatedRevenue = (totalCredits + analysesPerformed) * avgPricePerCredit;

        return res.json({
          success: true,
          stats: {
            totalUsers: totalUsers || 0,
            totalCredits: totalCredits || 0,
            creditsUsed: creditsUsed || 0,
            creditsAvailable: creditsAvailable || 0,
            analysesPerformed,
            estimatedRevenue: parseFloat(estimatedRevenue.toFixed(2)),
            activeUsers: usersData?.filter(u => u.ultima_analise).length || 0
          }
        });
      } catch (error) {
        console.error('Erro ao buscar do Supabase:', error);
        // Fallback para dados em memória
      }
    }

    // Fallback: usa dados em memória
    const allUsers = await getAllUsers();
    const totalUsers = allUsers.length;
    // Calcula créditos da tabela creditos
    let totalCredits = 0;
    try {
      const { count } = await supabaseAdmin
        .from('creditos')
        .select('*', { count: 'exact', head: true });
      totalCredits = count || 0;
    } catch (error) {
      console.error('Erro ao contar créditos:', error);
    }
    const analysesPerformed = allUsers.filter(u => u.lastAnalysis).length;
    const avgPricePerCredit = 8.30;
    const estimatedRevenue = (totalCredits + analysesPerformed) * avgPricePerCredit;

    res.json({
      success: true,
      stats: {
        totalUsers,
        totalCredits,
        analysesPerformed,
        estimatedRevenue: parseFloat(estimatedRevenue.toFixed(2)),
        activeUsers: analysesPerformed
      }
    });
  } catch (error) {
    console.error('Erro ao obter estatísticas:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao obter estatísticas',
      message: error.message
    });
  }
};

/**
 * Obtém uso por dia
 */
export const getDailyUsage = async (req, res) => {
  try {
    const { days = 30 } = req.query;

    if (supabaseAdmin) {
      try {
        const { data: users } = await supabaseAdmin
          .from('perfis_usuarios')
          .select('criado_em, ultima_analise');

        // Agrupa por dia
        const dailyStats = {};
        const endDate = new Date();
        const startDate = new Date();
        startDate.setDate(startDate.getDate() - parseInt(days));

        // Inicializa todos os dias
        for (let d = new Date(startDate); d <= endDate; d.setDate(d.getDate() + 1)) {
          const dateKey = d.toISOString().split('T')[0];
          dailyStats[dateKey] = {
            date: dateKey,
            registrations: 0,
            analyses: 0,
            revenue: 0
          };
        }

        // Processa dados
        users?.forEach(user => {
          const regDate = user.criado_em ? new Date(user.criado_em).toISOString().split('T')[0] : null;
          const analysisDate = user.ultima_analise ? new Date(user.ultima_analise).toISOString().split('T')[0] : null;

          if (regDate && dailyStats[regDate]) {
            dailyStats[regDate].registrations++;
          }

          if (analysisDate && dailyStats[analysisDate]) {
            dailyStats[analysisDate].analyses++;
            dailyStats[analysisDate].revenue += 8.30; // Estimativa por análise
          }
        });

        const result = Object.values(dailyStats).sort((a, b) => 
          new Date(a.date) - new Date(b.date)
        );

        return res.json({
          success: true,
          data: result
        });
      } catch (error) {
        console.error('Erro ao buscar do Supabase:', error);
      }
    }

    // Fallback: dados em memória
    const allUsers = await getAllUsers();
    const dailyStats = {};
    const endDate = new Date();
    const startDate = new Date();
    startDate.setDate(startDate.getDate() - parseInt(days));

    for (let d = new Date(startDate); d <= endDate; d.setDate(d.getDate() + 1)) {
      const dateKey = d.toISOString().split('T')[0];
      dailyStats[dateKey] = {
        date: dateKey,
        registrations: 0,
        analyses: 0,
        revenue: 0
      };
    }

    allUsers.forEach(user => {
      if (user.createdAt) {
        const regDate = new Date(user.createdAt).toISOString().split('T')[0];
        if (dailyStats[regDate]) {
          dailyStats[regDate].registrations++;
        }
      }

      if (user.lastAnalysis) {
        const analysisDate = new Date(user.lastAnalysis).toISOString().split('T')[0];
        if (dailyStats[analysisDate]) {
          dailyStats[analysisDate].analyses++;
          dailyStats[analysisDate].revenue += 8.30;
        }
      }
    });

    const result = Object.values(dailyStats).sort((a, b) => 
      new Date(a.date) - new Date(b.date)
    );

    res.json({
      success: true,
      data: result
    });
  } catch (error) {
    console.error('Erro ao obter uso diário:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao obter uso diário',
      message: error.message
    });
  }
};

/**
 * Obtém uso por mês
 */
export const getMonthlyUsage = async (req, res) => {
  try {
    const { months = 12 } = req.query;

    if (supabaseAdmin) {
      try {
        const { data: users } = await supabaseAdmin
          .from('perfis_usuarios')
          .select('criado_em, ultima_analise');

        const monthlyStats = {};
        const endDate = new Date();
        const startDate = new Date();
        startDate.setMonth(startDate.getMonth() - parseInt(months));

        // Inicializa todos os meses
        for (let d = new Date(startDate); d <= endDate; d.setMonth(d.getMonth() + 1)) {
          const monthKey = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
          monthlyStats[monthKey] = {
            month: monthKey,
            registrations: 0,
            analyses: 0,
            revenue: 0
          };
        }

        // Processa dados
        users?.forEach(user => {
          if (user.criado_em) {
            const regDate = new Date(user.criado_em);
            const monthKey = `${regDate.getFullYear()}-${String(regDate.getMonth() + 1).padStart(2, '0')}`;
            if (monthlyStats[monthKey]) {
              monthlyStats[monthKey].registrations++;
            }
          }

          if (user.ultima_analise) {
            const analysisDate = new Date(user.ultima_analise);
            const monthKey = `${analysisDate.getFullYear()}-${String(analysisDate.getMonth() + 1).padStart(2, '0')}`;
            if (monthlyStats[monthKey]) {
              monthlyStats[monthKey].analyses++;
              monthlyStats[monthKey].revenue += 8.30;
            }
          }
        });

        const result = Object.values(monthlyStats).sort((a, b) => 
          a.month.localeCompare(b.month)
        );

        return res.json({
          success: true,
          data: result
        });
      } catch (error) {
        console.error('Erro ao buscar do Supabase:', error);
      }
    }

    // Fallback: dados em memória
    const allUsers = await getAllUsers();
    const monthlyStats = {};
    const endDate = new Date();
    const startDate = new Date();
    startDate.setMonth(startDate.getMonth() - parseInt(months));

    for (let d = new Date(startDate); d <= endDate; d.setMonth(d.getMonth() + 1)) {
      const monthKey = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
      monthlyStats[monthKey] = {
        month: monthKey,
        registrations: 0,
        analyses: 0,
        revenue: 0
      };
    }

    allUsers.forEach(user => {
      if (user.createdAt) {
        const regDate = new Date(user.createdAt);
        const monthKey = `${regDate.getFullYear()}-${String(regDate.getMonth() + 1).padStart(2, '0')}`;
        if (monthlyStats[monthKey]) {
          monthlyStats[monthKey].registrations++;
        }
      }

      if (user.lastAnalysis) {
        const analysisDate = new Date(user.lastAnalysis);
        const monthKey = `${analysisDate.getFullYear()}-${String(analysisDate.getMonth() + 1).padStart(2, '0')}`;
        if (monthlyStats[monthKey]) {
          monthlyStats[monthKey].analyses++;
          monthlyStats[monthKey].revenue += 8.30;
        }
      }
    });

    const result = Object.values(monthlyStats).sort((a, b) => 
      a.month.localeCompare(b.month)
    );

    res.json({
      success: true,
      data: result
    });
  } catch (error) {
    console.error('Erro ao obter uso mensal:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao obter uso mensal',
      message: error.message
    });
  }
};

/**
 * Obtém lista de vendas/compras (para admin)
 */
export const getSales = async (req, res) => {
  try {
    const limit = parseInt(req.query.limit) || 100;
    const offset = parseInt(req.query.offset) || 0;

    const purchases = await getAllPurchases(limit, offset);

    res.json({
      success: true,
      purchases: purchases.map(p => ({
        id: p.id,
        userId: p.user_id,
        planId: p.plan_id,
        planName: p.plan_name,
        creditsAmount: p.credits_amount,
        price: p.price,
        currency: p.currency,
        status: p.status,
        paymentMethod: p.payment_method,
        paymentId: p.payment_id,
        createdAt: p.created_at,
        updatedAt: p.updated_at
      })),
      total: purchases.length,
      limit,
      offset
    });
  } catch (error) {
    console.error('Erro ao obter vendas:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao obter vendas',
      message: error.message
    });
  }
};

/**
 * Obtém estatísticas de vendas (para admin)
 */
export const getSalesStatistics = async (req, res) => {
  try {
    const { startDate, endDate } = req.query;

    const stats = await getSalesStats(startDate || null, endDate || null);

    res.json({
      success: true,
      stats
    });
  } catch (error) {
    console.error('Erro ao obter estatísticas de vendas:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao obter estatísticas de vendas',
      message: error.message
    });
  }
};

/**
 * Obtém estatísticas de uso de IA
 */
export const getAIUsageStatistics = async (req, res) => {
  try {
    const { period = 'day' } = req.query;

    const stats = await getAIUsageStats(period);

    if (!stats) {
      return res.status(500).json({
        success: false,
        error: 'Erro ao obter estatísticas de uso de IA'
      });
    }

    res.json({
      success: true,
      stats
    });
  } catch (error) {
    console.error('Erro ao obter estatísticas de uso de IA:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao obter estatísticas de uso de IA',
      message: error.message
    });
  }
};

/**
 * Obtém estatísticas de uso por site de vagas
 */
export const getJobSiteStats = async (req, res) => {
  try {
    const { startDate, endDate, limit } = req.query;
    
    const stats = await getJobSiteUsageStats(
      startDate || null,
      endDate || null
    );
    
    const ranking = await getJobSiteRanking(
      limit ? parseInt(limit) : 10,
      startDate || null,
      endDate || null
    );
    
    res.json({
      success: true,
      stats: stats,
      ranking: ranking,
      total: stats.length
    });
  } catch (error) {
    console.error('Erro ao obter estatísticas de sites:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao obter estatísticas de sites',
      message: error.message
    });
  }
};

/**
 * Obtém estatísticas detalhadas de um site específico
 */
export const getJobSiteDetailedStatsController = async (req, res) => {
  try {
    const { siteId } = req.params;
    const { startDate, endDate } = req.query;
    
    const stats = await getJobSiteDetailedStats(
      siteId || null,
      startDate || null,
      endDate || null
    );
    
    res.json({
      success: true,
      stats: stats,
      total: stats.length
    });
  } catch (error) {
    console.error('Erro ao obter estatísticas detalhadas:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao obter estatísticas detalhadas',
      message: error.message
    });
  }
};
