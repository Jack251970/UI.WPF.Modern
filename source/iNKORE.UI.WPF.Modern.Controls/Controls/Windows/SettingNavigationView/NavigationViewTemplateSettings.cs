// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Windows;

namespace Flow.Bar.Controls.NavigationView;

public class NavigationViewTemplateSettings : DependencyObject
{
    #region TopPadding

    private static readonly DependencyPropertyKey s_topPaddingPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(TopPadding),
            typeof(double),
            typeof(NavigationViewTemplateSettings),
            new PropertyMetadata(0.0));

    public static readonly DependencyProperty TopPaddingProperty =
        s_topPaddingPropertyKey.DependencyProperty;

    public double TopPadding
    {
        get => (double)GetValue(TopPaddingProperty);
        internal set => SetValue(s_topPaddingPropertyKey, value);
    }

    #endregion

    #region PaneToggleButtonVisibility

    private static readonly DependencyPropertyKey s_paneToggleButtonVisibilityPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(PaneToggleButtonVisibility),
            typeof(Visibility),
            typeof(NavigationViewTemplateSettings),
            new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty PaneToggleButtonVisibilityProperty =
        s_paneToggleButtonVisibilityPropertyKey.DependencyProperty;

    public Visibility PaneToggleButtonVisibility
    {
        get => (Visibility)GetValue(PaneToggleButtonVisibilityProperty);
        internal set => SetValue(s_paneToggleButtonVisibilityPropertyKey, value);
    }

    #endregion

    #region BackButtonVisibility

    private static readonly DependencyPropertyKey s_backButtonVisibilityPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(BackButtonVisibility),
            typeof(Visibility),
            typeof(NavigationViewTemplateSettings),
            new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty BackButtonVisibilityProperty =
        s_backButtonVisibilityPropertyKey.DependencyProperty;

    public Visibility BackButtonVisibility
    {
        get => (Visibility)GetValue(BackButtonVisibilityProperty);
        internal set => SetValue(s_backButtonVisibilityPropertyKey, value);
    }

    #endregion

    #region LeftPaneVisibility

    private static readonly DependencyPropertyKey s_leftPaneVisibilityPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(LeftPaneVisibility),
            typeof(Visibility),
            typeof(NavigationViewTemplateSettings),
            new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty LeftPaneVisibilityProperty =
        s_leftPaneVisibilityPropertyKey.DependencyProperty;

    public Visibility LeftPaneVisibility
    {
        get => (Visibility)GetValue(LeftPaneVisibilityProperty);
        internal set => SetValue(s_leftPaneVisibilityPropertyKey, value);
    }

    #endregion

    #region OpenPaneWidth

    private static readonly DependencyPropertyKey s_openPaneWidthPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(OpenPaneWidth),
            typeof(double),
            typeof(NavigationViewTemplateSettings),
            null);

    public static readonly DependencyProperty OpenPaneWidthProperty =
        s_openPaneWidthPropertyKey.DependencyProperty;

    public double OpenPaneWidth
    {
        get => (double)GetValue(OpenPaneWidthProperty);
        internal set => SetValue(s_openPaneWidthPropertyKey, value);
    }

    #endregion

    #region PaneToggleButtonWidth

    private static readonly DependencyPropertyKey s_paneToggleButtonWidthPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(PaneToggleButtonWidth),
            typeof(double),
            typeof(NavigationViewTemplateSettings),
            null);

    public static readonly DependencyProperty PaneToggleButtonWidthProperty =
        s_paneToggleButtonWidthPropertyKey.DependencyProperty;

    public double PaneToggleButtonWidth
    {
        get => (double)GetValue(PaneToggleButtonWidthProperty);
        internal set => SetValue(s_paneToggleButtonWidthPropertyKey, value);
    }

    #endregion

    #region SmallerPaneToggleButtonWidth

    private static readonly DependencyPropertyKey s_smallerPaneToggleButtonWidthPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(SmallerPaneToggleButtonWidth),
            typeof(double),
            typeof(NavigationViewTemplateSettings),
            null);

    public static readonly DependencyProperty SmallerPaneToggleButtonWidthProperty =
        s_smallerPaneToggleButtonWidthPropertyKey.DependencyProperty;

    public double SmallerPaneToggleButtonWidth
    {
        get => (double)GetValue(SmallerPaneToggleButtonWidthProperty);
        internal set => SetValue(s_smallerPaneToggleButtonWidthPropertyKey, value);
    }

    #endregion
}
