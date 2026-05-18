import { getOrCreateUser, saveUser } from '../models/user.model.js';
import { createPurchase, registerCouponUse, getPurchaseByPaymentId } from '../services/supabase.service.js';
import { sendPurchaseConfirmationEmail } from '../services/email.service.js';

/**
 * Conclui pedido pago com idempotência por id_pagamento
 */
export const fulfillPaidOrder = async ({
  userId,
  planId,
  planName,
  analyses,
  price,
  paymentMethod,
  paymentId,
  customerEmail = '',
  couponId = null,
  couponName = null,
  discountPercent = null,
  originalPrice = null,
  cpfNormalized = null,
  extraInfo = ''
}) => {
  const existing = await getPurchaseByPaymentId(paymentId);
  if (existing) {
    const user = await getOrCreateUser(userId, customerEmail);
    return {
      alreadyFulfilled: true,
      user: {
        id: user.id,
        credits: user.credits,
        plan: user.plan
      }
    };
  }

  await createPurchase(
    userId,
    planId,
    planName,
    analyses,
    price,
    'BRL',
    paymentMethod,
    paymentId,
    null,
    'analysis_plan',
    couponId,
    couponName,
    discountPercent,
    originalPrice
  );

  if (couponId && cpfNormalized) {
    await registerCouponUse(couponId, cpfNormalized);
  }

  const user = await getOrCreateUser(userId, customerEmail);
  await user.addCredits(analyses);
  const updatedUser = await saveUser({ ...user, plan: planId });

  const email = customerEmail || updatedUser.email || '';
  if (email) {
    try {
      await sendPurchaseConfirmationEmail(email, {
        planName,
        analyses,
        price,
        customerName: updatedUser.name || '',
        extraInfo,
        couponName: couponName || undefined,
        discountPercent: discountPercent ?? undefined,
        originalPrice: originalPrice ?? undefined
      });
    } catch (emailErr) {
      console.error('Erro ao enviar confirmação de compra:', emailErr);
    }
  }

  return {
    alreadyFulfilled: false,
    user: {
      id: updatedUser.id,
      credits: updatedUser.credits,
      plan: updatedUser.plan
    }
  };
};

/**
 * Checkout 100% grátis (cupom)
 */
export const fulfillFreeCheckout = async (ctx, email = '') => {
  return fulfillPaidOrder({
    userId: ctx.userId,
    planId: ctx.planId,
    planName: ctx.planName,
    analyses: ctx.analyses,
    price: 0,
    paymentMethod: 'coupon',
    paymentId: `free_${Date.now()}_${ctx.userId}`,
    customerEmail: email,
    couponId: ctx.couponId,
    couponName: ctx.couponName,
    discountPercent: ctx.discountPercent,
    originalPrice: ctx.originalPrice,
    cpfNormalized: ctx.cpfNormalized,
    extraInfo: 'Compra 100% grátis com cupom.'
  });
};
