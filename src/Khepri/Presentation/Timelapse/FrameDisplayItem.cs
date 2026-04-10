// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using Khepri.Domain.Timelapse;

namespace Khepri.Presentation.Timelapse;

/// <summary>
/// Wraps a <see cref="TimelapseFrame"/> with a mutable display position so that
/// "Frame N" labels update after drag-to-reorder or deletion.
/// </summary>
public sealed class FrameDisplayItem(TimelapseFrame frame, int position) : INotifyPropertyChanged
{
    private int _position = position;
    private bool _isSelected;

    public TimelapseFrame Frame { get; } = frame;

    public int Position
    {
        get => _position;
        set
        {
            if (_position == value)
            {
                return;
            }

            _position = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
        }
    }

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

    public string Label => $"FRAME {_position}";

    // Flat path accessors — avoid chained paths in XAML bindings which may fall
    // back to string-path (non-compiled) bindings depending on the MAUI version.
    public string FrameFilePath => Frame.FilePath;
    public string FrameDisplayDate => Frame.CapturedAt.ToString("d MMM yyyy", System.Globalization.CultureInfo.CurrentCulture);

    public event PropertyChangedEventHandler? PropertyChanged;
}
