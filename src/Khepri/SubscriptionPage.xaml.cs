// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Khepri.Infrastructure;

namespace Khepri;

public partial class SubscriptionPage : ContentPage
{
    public const string MonthlyId = "khepri_monthly";
    public const string AnnualId = "khepri_annual";

    private readonly ISubscriptionService _billing;

    private string _selectedPlan = AnnualId;
    private TaskCompletionSource<bool>? _tcs;

    public SubscriptionPage(ISubscriptionService billing)
    {
        _billing = billing;
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = LoadPricesAsync();
    }

    private async Task LoadPricesAsync()
    {
        var prices = await _billing.GetPricesAsync();
        if (prices?.Monthly is string monthly)
        {
            MonthlyPrice.Text = monthly;
        }
        if (prices?.Annual is string annual)
        {
            AnnualPrice.Text = annual;
        }
    }

    // Called by SplashPage immediately after PushModalAsync.
    // Returns a Task that completes once the user has successfully subscribed.
    public Task<bool> WaitForSubscriptionAsync()
    {
        _tcs = new TaskCompletionSource<bool>();
        return _tcs.Task;
    }

    // Allow the hardware back button to dismiss the subscription page.
    protected override bool OnBackButtonPressed()
    {
        _ = Navigation.PopModalAsync(animated: false);
        _tcs?.TrySetResult(false);
        return true;
    }

    private void OnMonthlyTapped(object? sender, EventArgs e)
        => SelectPlan(MonthlyId);

    private void OnAnnualTapped(object? sender, EventArgs e)
        => SelectPlan(AnnualId);

    private void SelectPlan(string planId)
    {
        _selectedPlan = planId;
        var monthly = planId == MonthlyId;

        MonthlyBorder.Stroke = monthly ? Colors.White : Color.FromArgb("#444444");
        AnnualBorder.Stroke = monthly ? Color.FromArgb("#444444") : Colors.White;
        MonthlyCheck.IsVisible = monthly;
        AnnualCheck.IsVisible = !monthly;
    }

    private async void OnSubscribeTapped(object? sender, EventArgs e)
    {
        SubscribeButton.IsEnabled = false;
        try
        {
            var ok = await _billing.PurchaseAsync(_selectedPlan);
            if (ok)
            {
                await CompleteAsync();
            }
            else
            {
                await DisplayAlertAsync("Purchase Failed",
                    "The purchase could not be completed. Please try again.", "OK");
            }
        }
        finally
        {
            SubscribeButton.IsEnabled = true;
        }
    }

    private async void OnRestoreTapped(object? sender, EventArgs e)
    {
        var ok = await _billing.RestorePurchasesAsync();
        if (ok)
        {
            await CompleteAsync();
        }
        else
        {
            await DisplayAlertAsync("No Subscription Found",
                "No active subscription was found for your Google account.", "OK");
        }
    }

    private async Task CompleteAsync()
    {
        await Navigation.PopModalAsync(animated: false);
        _tcs?.TrySetResult(true);
    }
}
