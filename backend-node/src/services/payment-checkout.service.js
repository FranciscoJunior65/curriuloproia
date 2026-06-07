import { PRICING_PLANS } from './pricing.service.js';
import { validateCoupon, normalizeCpf } from './supabase.service.js';

/**
 * Calcula valor e metadados do checkout (cupom, checkout grátis)
 */
export const buildCheckoutContext = async (planId, userId, couponCode = null, cpf = null) => {
  const plan = PRICING_PLANS[planId];
  if (!plan) {
    throw new Error('Plano não encontrado');
  }

  let amountBRL = plan.priceBRL;
  const metadata = {
    userId,
    planId,
    planName: plan.name,
    analyses: String(plan.analyses)
  };
  let couponInfo = null;
  let cpfNormalized = null;

  if (cpf && String(cpf).trim()) {
    cpfNormalized = normalizeCpf(cpf);
    if (cpfNormalized.length === 11) {
      metadata.cpfNormalized = cpfNormalized;
    }
  }

  if (couponCode && String(couponCode).trim()) {
    if (!cpfNormalized || cpfNormalized.length !== 11) {
      throw new Error('Para usar cupom, informe seu CPF (11 dígitos).');
    }
    const result = await validateCoupon(couponCode, cpf);
    if (!result.valid || !result.coupon) {
      throw new Error(result.message || 'Cupom inválido ou já utilizado por este CPF.');
    }
    const pct = result.coupon.porcentagem_desconto;
    const original = plan.priceBRL;
    amountBRL = Math.max(0, original * (1 - pct / 100));
    couponInfo = {
      couponId: result.coupon.id,
      couponName: result.coupon.nome,
      discountPercent: pct,
      originalPrice: original
    };
    metadata.couponId = result.coupon.id;
    metadata.couponName = result.coupon.nome;
    metadata.discountPercent = String(pct);
    metadata.originalPrice = String(original);
  }

  const amountInCents = Math.round(amountBRL * 100);

  if (amountInCents <= 0 && couponInfo) {
    return {
      freeCheckout: true,
      userId,
      planId,
      planName: plan.name,
      analyses: plan.analyses,
      amountBRL: 0,
      plan,
      metadata,
      couponInfo,
      cpfNormalized: cpfNormalized || metadata.cpfNormalized || null
    };
  }

  return {
    freeCheckout: false,
    userId,
    planId,
    planName: plan.name,
    analyses: plan.analyses,
    amountBRL,
    amountInCents,
    plan,
    metadata,
    couponInfo,
    cpfNormalized: cpfNormalized || metadata.cpfNormalized || null
  };
};
