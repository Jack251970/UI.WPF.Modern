// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Windows;
using IControlProtected = Flow.Bar.Controls.NavigationView.CppWinRTHelpers.IControlProtected;

namespace Flow.Bar.Controls.NavigationView;

enum NavigationViewVisualStateDisplayMode
{
    Compact,
    Expanded,
    Minimal,
    MinimalWithBackButton
}

enum NavigationViewRepeaterPosition
{
    LeftNav,
    LeftFooter
}

// Since RS5, a lot of functions in NavigationViewItem is moved to NavigationViewItemPresenter. So they both share some common codes.
// This class helps to initialize and maintain the status of SelectionIndicator and ToolTip
class NavigationViewItemHelper<T>
{
    public NavigationViewItemHelper()
    {
    }

    public UIElement GetSelectionIndicator()
    {
        return m_selectionIndicator;
    }

    public void Init(IControlProtected controlProtected)
    {
        m_selectionIndicator = controlProtected.GetTemplateChild(C_selectionIndicatorName) as UIElement;
    }

    UIElement m_selectionIndicator;

    private const string C_selectionIndicatorName = "SelectionIndicator";
}
