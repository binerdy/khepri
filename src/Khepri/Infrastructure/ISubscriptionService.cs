// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri.Infrastructure;

public interface ISubscriptionService
{
    Task<bool> IsSubscribedAsync();
    Task<SubscriptionPrices?> GetPricesAsync();
    Task<bool> PurchaseAsync(string productId);
    Task<bool> RestorePurchasesAsync();
}
