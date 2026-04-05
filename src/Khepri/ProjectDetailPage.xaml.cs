// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Khepri.Presentation.Timelapse;

namespace Khepri;

public partial class ProjectDetailPage : ContentPage
{
    public ProjectDetailPage(ProjectDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
