// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri.Infrastructure;

/// <summary>Formatted prices fetched from the store for the two subscription base plans.</summary>
/// <param name="Monthly">Formatted price string for the monthly plan (e.g. "CHF 2.99"), or null if unavailable.</param>
/// <param name="Annual">Formatted price string for the annual plan (e.g. "CHF 24.99"), or null if unavailable.</param>
public record SubscriptionPrices(string? Monthly, string? Annual);
