// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using Khepri.Domain.Timelapse;

namespace Khepri.Application.Timelapse;

/// <summary>
/// Wraps a <see cref="TimelapseProject"/> with mutable selection state for the
/// project list UI.
/// </summary>
public sealed class ProjectDisplayItem(TimelapseProject project) : INotifyPropertyChanged
{
    private bool _isSelected;

    public TimelapseProject Project { get; } = project;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
