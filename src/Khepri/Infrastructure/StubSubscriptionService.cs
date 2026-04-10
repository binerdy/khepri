// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri.Infrastructure;

// On non-Android platforms subscriptions are not enforced.
public class StubSubscriptionService : ISubscriptionService
{
    public Task<bool> IsSubscribedAsync() => Task.FromResult(true);
    public Task<SubscriptionPrices?> GetPricesAsync() => Task.FromResult<SubscriptionPrices?>(null);
    public Task<bool> PurchaseAsync(string _) => Task.FromResult(true);
    public Task<bool> RestorePurchasesAsync() => Task.FromResult(true);
}
