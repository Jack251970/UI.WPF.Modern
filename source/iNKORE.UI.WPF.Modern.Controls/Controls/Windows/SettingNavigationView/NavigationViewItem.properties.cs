// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Windows;
using iNKORE.UI.WPF.Modern.Controls.Helpers;

namespace Flow.Bar.Controls.NavigationView;

public partial class NavigationViewItem
{
    #region Icon

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(
            nameof(Icon),
            typeof(object),
            typeof(NavigationViewItem),
            new PropertyMetadata(OnIconPropertyChanged));

    public object Icon
    {
        get => (object)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    private static void OnIconPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var owner = (NavigationViewItem)sender;
        owner.OnIconPropertyChanged(args);
    }

    #endregion

    #region CompactPaneLength

    private static readonly DependencyPropertyKey s_compactPaneLengthPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CompactPaneLength),
            typeof(double),
            typeof(NavigationViewItem),
            new PropertyMetadata(48.0));

    public static readonly DependencyProperty CompactPaneLengthProperty =
        s_compactPaneLengthPropertyKey.DependencyProperty;

    public double CompactPaneLength
    {
        get => (double)GetValue(CompactPaneLengthProperty);
        private set => SetValue(s_compactPaneLengthPropertyKey, value);
    }

    #endregion

    #region CornerRadius

    public static readonly DependencyProperty CornerRadiusProperty =
        ControlHelper.CornerRadiusProperty.AddOwner(typeof(NavigationViewItem));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    #endregion
}
