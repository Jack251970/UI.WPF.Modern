// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Flow.Bar.Controls.NavigationView;

public sealed class NavigationViewItemExpandingEventArgs : EventArgs
{
    internal NavigationViewItemExpandingEventArgs(NavigationView navigationView)
    {
        m_navigationView = navigationView;
    }

    public NavigationViewItemBase ExpandingItemContainer { get; internal set; }

    public object ExpandingItem
    {
        get
        {
            if (m_expandingItem != null)
            {
                return m_expandingItem;
            }

            if (m_navigationView is { })
            {
                m_expandingItem = NavigationView.MenuItemFromContainer(ExpandingItemContainer);
                return m_expandingItem;
            }

            return null;
        }
    }

    private object m_expandingItem;
    private readonly NavigationView m_navigationView;
}
