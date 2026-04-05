// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri;

public partial class App : Microsoft.Maui.Controls.Application
{
	private readonly SplashPage _splash;

	public App(SplashPage splash)
	{
		InitializeComponent();
		_splash = splash;
	}

	protected override Window CreateWindow(IActivationState? activationState)
		=> new Window(_splash);
}
