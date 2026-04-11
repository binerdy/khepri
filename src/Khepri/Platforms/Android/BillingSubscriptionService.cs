// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Android.BillingClient.Api;
using Khepri.Infrastructure;
using AndroidBillingResult = Android.BillingClient.Api.BillingResult;
using AndroidLog = Android.Util.Log;

namespace Khepri.Platforms.Android;

public class BillingSubscriptionService : ISubscriptionService
{
    private const string SubscriptionId = "khepri_subscription";

    private BillingClient? _client;
    private TaskCompletionSource<bool>? _connectTcs;
    private TaskCompletionSource<bool>? _purchaseTcs;

    private async Task<bool> EnsureConnectedAsync()
    {
        if (_client?.IsReady == true)
        {
            return true;
        }

        var context = Platform.CurrentActivity;
        if (context == null)
        {
            return false;
        }

        _client?.EndConnection();

        var pendingParams = PendingPurchasesParams.NewBuilder()
            .EnableOneTimeProducts()
            .Build();

        _connectTcs = new TaskCompletionSource<bool>();

        _client = BillingClient.NewBuilder(context)
            .SetListener(new PurchaseListener(this))
            .EnablePendingPurchases(pendingParams)
            .Build();

        _client.StartConnection(new ConnectionStateListener(this));
        return await _connectTcs.Task;
    }

    internal void OnConnected(bool success) => _connectTcs?.TrySetResult(success);
    internal void OnPurchaseResult(bool success) => _purchaseTcs?.TrySetResult(success);

    internal async void AcknowledgeAsync(AcknowledgePurchaseParams ackParams)
    {
        try
        {
            if (_client?.IsReady == true)
            {
                await _client.AcknowledgePurchaseAsync(ackParams);
            }
        }
        catch { /* best-effort */ }
    }

    public async Task<bool> IsSubscribedAsync()
    {
        try
        {
            if (!await EnsureConnectedAsync())
            {
                return false;
            }

            var queryParams = QueryPurchasesParams.NewBuilder()
                .SetProductType(BillingClient.ProductType.Subs)
                .Build();

            var result = await _client!.QueryPurchasesAsync(queryParams);
            return result.Purchases?.Any(p =>
                p.Products.Contains(SubscriptionId) &&
                p.PurchaseState == PurchaseState.Purchased) ?? false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<SubscriptionPrices?> GetPricesAsync()
    {
        try
        {
            if (!await EnsureConnectedAsync())
            {
                return null;
            }

            var queryParams = QueryProductDetailsParams.NewBuilder()
                .SetProductList(
                [
                    QueryProductDetailsParams.Product.NewBuilder()
                        .SetProductId(SubscriptionId)
                        .SetProductType(BillingClient.ProductType.Subs)
                        .Build()
                ])
                .Build();

            var productResult = await _client!.QueryProductDetailsAsync(queryParams);
            if (productResult.Result.ResponseCode != BillingResponseCode.Ok)
            {
                return null;
            }

            var details = productResult.ProductDetails.FirstOrDefault();
            if (details == null)
            {
                return null;
            }

            var offers = details.GetSubscriptionOfferDetails();
            return new SubscriptionPrices(
                Monthly: GetRecurringPrice(offers, SubscriptionPage.MonthlyId),
                Annual: GetRecurringPrice(offers, SubscriptionPage.AnnualId));
        }
        catch
        {
            return null;
        }
    }

    // Returns the formatted price of the recurring phase for the given base plan.
    private static string? GetRecurringPrice(
        IList<ProductDetails.SubscriptionOfferDetails>? offers, string basePlanId)
    {
        var offer = offers?.FirstOrDefault(o => o.BasePlanId == basePlanId);

        // Pricing phases are ordered; the last one is always the recurring charge.
        return offer?.PricingPhases?.PricingPhaseList?.LastOrDefault()?.FormattedPrice;
    }

    public async Task<bool> PurchaseAsync(string productId)
    {
        string? failReason = null;
        try
        {
            if (!await EnsureConnectedAsync())
            {
                failReason = "Billing client could not connect.";
                return false;
            }

            var queryParams = QueryProductDetailsParams.NewBuilder()
                .SetProductList(
                [
                    QueryProductDetailsParams.Product.NewBuilder()
                        .SetProductId(SubscriptionId)
                        .SetProductType(BillingClient.ProductType.Subs)
                        .Build()
                ])
                .Build();

            var productResult = await _client!.QueryProductDetailsAsync(queryParams);
            if (productResult.Result.ResponseCode != BillingResponseCode.Ok)
            {
                failReason = $"QueryProductDetails failed: {productResult.Result.ResponseCode} — {productResult.Result.DebugMessage}";
                return false;
            }

            var details = productResult.ProductDetails.FirstOrDefault();
            if (details == null)
            {
                failReason = $"Product '{SubscriptionId}' not found in query result.";
                return false;
            }

            // productId here is a base plan ID (e.g. "khepri-monthly" / "khepri-annual").
            // Prefer the base plan offer (OfferId == null) so we don't accidentally use
            // the free-trial token when the user intends a direct subscription.
            var offerDetails = details.GetSubscriptionOfferDetails();
            var matchedOffer = offerDetails?.FirstOrDefault(o => o.BasePlanId == productId && o.OfferId == null)
                            ?? offerDetails?.FirstOrDefault(o => o.BasePlanId == productId)
                            ?? offerDetails?.FirstOrDefault();
            var offerToken = matchedOffer?.OfferToken;

            if (offerToken == null)
            {
                failReason = $"No offer token found for base plan '{productId}'. Available offers: {string.Join(", ", offerDetails?.Select(o => $"{o.BasePlanId}/{o.OfferId}") ?? [])}";
                return false;
            }

            var productDetailsParamsBuilder = BillingFlowParams.ProductDetailsParams.NewBuilder()
                .SetProductDetails(details)
                .SetOfferToken(offerToken);

            var flowParams = BillingFlowParams.NewBuilder()
                .SetProductDetailsParamsList([productDetailsParamsBuilder.Build()])
                .Build();

            var activity = Platform.CurrentActivity;
            if (activity == null)
            {
                failReason = "No current activity.";
                return false;
            }

            _purchaseTcs = new TaskCompletionSource<bool>();
            var launchResult = _client.LaunchBillingFlow(activity, flowParams);
            if (launchResult.ResponseCode != BillingResponseCode.Ok)
            {
                failReason = $"LaunchBillingFlow failed: {launchResult.ResponseCode} — {launchResult.DebugMessage}";
                _purchaseTcs = null;
                return false;
            }

            return await _purchaseTcs.Task;
        }
        catch (Exception ex)
        {
            failReason = ex.ToString();
            return false;
        }
        finally
        {
            if (failReason != null)
            {
                AndroidLog.Error("Khepri.Billing", failReason);
            }
        }
    }

    public Task<bool> RestorePurchasesAsync() => IsSubscribedAsync();
}

internal sealed class ConnectionStateListener : Java.Lang.Object, IBillingClientStateListener
{
    private readonly BillingSubscriptionService _service;

    public ConnectionStateListener(BillingSubscriptionService service) => _service = service;

    public void OnBillingSetupFinished(AndroidBillingResult result)
        => _service.OnConnected(result.ResponseCode == BillingResponseCode.Ok);

    public void OnBillingServiceDisconnected()
        => _service.OnConnected(false);
}

internal sealed class PurchaseListener : Java.Lang.Object, IPurchasesUpdatedListener
{
    private readonly BillingSubscriptionService _service;

    public PurchaseListener(BillingSubscriptionService service) => _service = service;

    public void OnPurchasesUpdated(AndroidBillingResult result, IList<Purchase>? purchases)
    {
        if (result.ResponseCode != BillingResponseCode.Ok)
        {
            _service.OnPurchaseResult(false);
            return;
        }

        var purchase = purchases?.FirstOrDefault(p =>
            p.PurchaseState == PurchaseState.Purchased ||
            p.PurchaseState == PurchaseState.Pending);

        if (purchase == null)
        {
            _service.OnPurchaseResult(false);
            return;
        }

        // Acknowledge the purchase so Google doesn't auto-refund it after 3 days.
        if (purchase.PurchaseState == PurchaseState.Purchased && !purchase.IsAcknowledged)
        {
            var ackParams = AcknowledgePurchaseParams.NewBuilder()
                .SetPurchaseToken(purchase.PurchaseToken)
                .Build();
            _service.AcknowledgeAsync(ackParams);
        }

        _service.OnPurchaseResult(true);
    }
}
