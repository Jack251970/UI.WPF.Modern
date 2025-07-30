// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using iNKORE.UI.WPF.Modern.Common;
using iNKORE.UI.WPF.Modern.Controls.Helpers;
using iNKORE.UI.WPF.Modern.Input;
using static Flow.Bar.Controls.NavigationView.CppWinRTHelpers;
using IControlProtected = Flow.Bar.Controls.NavigationView.CppWinRTHelpers.IControlProtected;
using ControlHelper = iNKORE.UI.WPF.Modern.Controls.Helpers.ControlHelper;

namespace Flow.Bar.Controls.NavigationView;

public class NavigationViewItemPresenter : ContentControl, IControlProtected
{
    private const string C_contentGrid = "PresenterContentRootGrid";

    private const string C_iconBoxColumnDefinitionName = "IconColumn";

    static NavigationViewItemPresenter()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(NavigationViewItemPresenter),
            new FrameworkPropertyMetadata(typeof(NavigationViewItemPresenter)));

        HorizontalContentAlignmentProperty.OverrideMetadata(
            typeof(NavigationViewItemPresenter),
            new FrameworkPropertyMetadata(HorizontalAlignment.Center));

        VerticalContentAlignmentProperty.OverrideMetadata(
            typeof(NavigationViewItemPresenter),
            new FrameworkPropertyMetadata(VerticalAlignment.Center));
    }

    public NavigationViewItemPresenter()
    {
        InputHelper.SetIsTapEnabled(this, true);
    }

    #region Icon

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(
            nameof(Icon),
            typeof(object),
            typeof(NavigationViewItemPresenter),
            null);

    public object Icon
    {
        get => (object)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    #endregion

    #region UseSystemFocusVisuals

    public static readonly DependencyProperty UseSystemFocusVisualsProperty =
        FocusVisualHelper.UseSystemFocusVisualsProperty.AddOwner(typeof(NavigationViewItemPresenter));

    public bool UseSystemFocusVisuals
    {
        get => (bool)GetValue(UseSystemFocusVisualsProperty);
        set => SetValue(UseSystemFocusVisualsProperty, value);
    }

    #endregion

    #region CornerRadius

    public static readonly DependencyProperty CornerRadiusProperty =
        ControlHelper.CornerRadiusProperty.AddOwner(typeof(NavigationViewItemPresenter));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    #endregion

    public override void OnApplyTemplate()
    {
        // Retrieve pointers to stable controls 
        m_helper.Init(this);

        if (GetTemplateChildT<Grid>(C_contentGrid, this) is { } contentGrid)
        {
            m_contentGrid = contentGrid;
        }

        if (GetNavigationViewItem() is { } navigationViewItem)
        {
            navigationViewItem.UpdateVisualStateNoTransition();
            navigationViewItem.UpdateIsClosedCompact();

            // We probably switched displaymode, so restore width now, otherwise the next time we will restore is when the CompactPaneLength changes
            if (navigationViewItem.GetNavigationView() is { } navigationView)
            {
                UpdateCompactPaneLength(m_compactPaneLengthValue, true);
            }
        }

        UpdateMargin();
    }

    internal UIElement GetSelectionIndicator()
    {
        return m_helper.GetSelectionIndicator();
    }

    NavigationViewItem GetNavigationViewItem()
    {
        NavigationViewItem navigationViewItem = null;

        DependencyObject obj = this;

        if (SharedHelpers.GetAncestorOfType<NavigationViewItem>(VisualTreeHelper.GetParent(obj)) is { } item)
        {
            navigationViewItem = item;
        }
        return navigationViewItem;
    }

    internal void UpdateContentLeftIndentation(double leftIndentation)
    {
        m_leftIndentation = leftIndentation;
        UpdateMargin();
    }

    void UpdateMargin()
    {
        if (m_contentGrid is { } grid)
        {
            var oldGridMargin = grid.Margin;
            grid.Margin = new Thickness(m_leftIndentation, oldGridMargin.Top, oldGridMargin.Right, oldGridMargin.Bottom);
        }
    }

    internal void UpdateCompactPaneLength(double compactPaneLength, bool shouldUpdate)
    {
        m_compactPaneLengthValue = compactPaneLength;
        if (shouldUpdate)
        {
            if (GetTemplateChildT<ColumnDefinition>(C_iconBoxColumnDefinitionName, this) is { } iconGridColumn)
            {
                var gridLength = compactPaneLength;
                ColumnDefinitionHelper.SetPixelWidth(iconGridColumn, Math.Max(0.0, gridLength - 8));
            }
        }
    }

    DependencyObject IControlProtected.GetTemplateChild(string childName)
    {
        return GetTemplateChild(childName);
    }

    private double m_compactPaneLengthValue = 40;

    private readonly NavigationViewItemHelper<NavigationViewItemPresenter> m_helper = new();
    private Grid m_contentGrid;

    private double m_leftIndentation = 0;
}
