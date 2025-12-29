import { getAllUsers } from '../models/user.model.js';
import { supabaseAdmin, getAllPurchases, getSalesStats } from '../services/supabase.service.js';
import { userHasCredits } from '../services/supabase.service.js';

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
          .from('user_profiles')
          .select('*', { count: 'exact', head: true });

        // Total de créditos vendidos (soma de todos os créditos já adicionados)
        // Como não temos histórico, vamos usar a soma atual de créditos + análises realizadas
        const { data: users } = await supabaseAdmin
          .from('user_profiles')
          .select('credits, created_at, last_analysis');

        const totalCredits = users?.reduce((sum, user) => sum + (user.credits || 0), 0) || 0;
        
        // Estima análises realizadas (usuários com last_analysis preenchido)
        const analysesPerformed = users?.filter(u => u.last_analysis).length || 0;

        // Calcula faturamento estimado (assumindo que cada crédito foi comprado)
        // Plano único: R$ 9,90, Pacote 3: R$ 24,90 (média ~R$ 8,30 por crédito)
        const avgPricePerCredit = 8.30;
        const estimatedRevenue = (totalCredits + analysesPerformed) * avgPricePerCredit;

        return res.json({
          success: true,
          stats: {
            totalUsers: totalUsers || 0,
            totalCredits: totalCredits,
            analysesPerformed,
            estimatedRevenue: parseFloat(estimatedRevenue.toFixed(2)),
            activeUsers: users?.filter(u => u.last_analysis).length || 0
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
    const totalCredits = allUsers.reduce((sum, user) => sum + (user.credits || 0), 0);
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
          .from('user_profiles')
          .select('created_at, last_analysis, credits');

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
          const regDate = user.created_at ? new Date(user.created_at).toISOString().split('T')[0] : null;
          const analysisDate = user.last_analysis ? new Date(user.last_analysis).toISOString().split('T')[0] : null;

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
          .from('user_profiles')
          .select('created_at, last_analysis, credits');

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
          if (user.created_at) {
            const regDate = new Date(user.created_at);
            const monthKey = `${regDate.getFullYear()}-${String(regDate.getMonth() + 1).padStart(2, '0')}`;
            if (monthlyStats[monthKey]) {
              monthlyStats[monthKey].registrations++;
            }
          }

          if (user.last_analysis) {
            const analysisDate = new Date(user.last_analysis);
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

