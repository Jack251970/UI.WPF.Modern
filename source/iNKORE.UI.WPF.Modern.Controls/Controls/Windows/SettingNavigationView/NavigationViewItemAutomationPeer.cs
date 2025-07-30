// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace Flow.Bar.Controls.NavigationView;

public class NavigationViewItemAutomationPeer(NavigationViewItem owner) :
    FrameworkElementAutomationPeer(owner),
    IInvokeProvider,
    ISelectionItemProvider,
    IExpandCollapseProvider
{
    protected override string GetNameCore()
    {
        string returnHString = base.GetNameCore();

        // If a name hasn't been provided by AutomationProperties.Name in markup:
        if (string.IsNullOrEmpty(returnHString))
        {
            if (Owner is NavigationViewItem lvi)
            {
                returnHString = TryGetStringRepresentationFromObject(lvi.Content);
            }
        }

        return returnHString;
    }

    private static string TryGetStringRepresentationFromObject(object obj)
    {
        return obj?.ToString() ?? string.Empty;
    }

    public override object GetPattern(PatternInterface pattern)
    {
        if (pattern == PatternInterface.SelectionItem ||
            // Only provide expand collapse pattern if we have children!
            (pattern == PatternInterface.ExpandCollapse && HasChildren()))
        {
            return this;
        }

        return base.GetPattern(pattern);
    }

    protected override string GetClassNameCore()
    {
        return nameof(NavigationViewItem);
    }

    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return AutomationControlType.ListItem;
    }

#if NET48_OR_NEWER
    protected override int GetPositionInSetCore()
    {
        return GetPositionOrSetCountInLeftNavHelper(AutomationOutput.Position);
    }

    protected override int GetSizeOfSetCore()
    {
        return GetPositionOrSetCountInLeftNavHelper(AutomationOutput.Size);
    }

    // Get either the position or the size of the set for this particular item in the case of left nav. 
    // We go through all the items and then we determine if the listviewitem from the left listview can be a navigation view item header
    // or a navigation view item. If it's the former, we just reset the count. If it's the latter, we increment the counter.
    // In case of calculating the position, if this is the NavigationViewItemAutomationPeer we're iterating through we break the loop.
    int GetPositionOrSetCountInLeftNavHelper(AutomationOutput automationOutput)
    {
        int returnValue = 0;

        if (GetParentItemsRepeater() is { } repeater)
        {
            if (FrameworkElementAutomationPeer.CreatePeerForElement(repeater) is AutomationPeer parent)
            {
                if (parent.GetChildren() is { } children)
                {
                    int index = 0;
                    bool itemFound = false;

                    foreach (var child in children)
                    {
                        if (repeater.TryGetElement(index) is { } dependencyObject)
                        {
                            if (dependencyObject is NavigationViewItemHeader)
                            {
                                if (automationOutput == AutomationOutput.Size && itemFound)
                                {
                                    break;
                                }
                                else
                                {
                                    returnValue = 0;
                                }
                            }
                            else if (dependencyObject is NavigationViewItem navviewItem)
                            {
                                if (navviewItem.Visibility == System.Windows.Visibility.Visible)
                                {
                                    returnValue++;

                                    if (FrameworkElementAutomationPeer.FromElement(navviewItem) == (this))
                                    {
                                        if (automationOutput == AutomationOutput.Position)
                                        {
                                            break;
                                        }
                                        else
                                        {
                                            itemFound = true;
                                        }
                                    }
                                }
                            }
                        }
                        index++;
                    }
                }
            }
        }

        return returnValue;
    }

    iNKORE.UI.WPF.Modern.Controls.ItemsRepeater GetParentItemsRepeater()
    {
        if (GetParentNavigationView() is { })
        {
            if (Owner is NavigationViewItemBase navigationViewItem)
            {
                return NavigationView.GetParentItemsRepeaterForContainer(navigationViewItem);
            }
        }
        return null;
    }
#endif

    void IInvokeProvider.Invoke()
    {
        if (GetParentNavigationView() is { } navView)
        {
            if (Owner is NavigationViewItem navigationViewItem)
            {
                navView.OnNavigationViewItemInvoked(navigationViewItem);
            }
        }
    }

    ExpandCollapseState IExpandCollapseProvider.ExpandCollapseState
    {
        get
        {
            var state = ExpandCollapseState.LeafNode;
            if (Owner is NavigationViewItem navigationViewItem)
            {
                state = navigationViewItem.IsExpanded ?
                    ExpandCollapseState.Expanded :
                    ExpandCollapseState.Collapsed;
            }

            return state;
        }
    }

    void IExpandCollapseProvider.Collapse()
    {
        if (GetParentNavigationView() is { })
        {
            if (Owner is NavigationViewItem navigationViewItem)
            {
                NavigationView.Collapse(navigationViewItem);
                RaiseExpandCollapseAutomationEvent(ExpandCollapseState.Collapsed);
            }
        }
    }

    void IExpandCollapseProvider.Expand()
    {
        if (GetParentNavigationView() is { })
        {
            if (Owner is NavigationViewItem navigationViewItem)
            {
                NavigationView.Expand(navigationViewItem);
                RaiseExpandCollapseAutomationEvent(ExpandCollapseState.Expanded);
            }
        }
    }

    internal void RaiseExpandCollapseAutomationEvent(ExpandCollapseState newState)
    {
        if (AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged))
        {
            ExpandCollapseState oldState = (newState == ExpandCollapseState.Expanded) ?
                ExpandCollapseState.Collapsed :
                ExpandCollapseState.Expanded;

            // box_value(oldState) doesn't work here, use ReferenceWithABIRuntimeClassName to make Narrator can unbox it.
            RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                oldState,
                newState);
        }
    }

    NavigationView GetParentNavigationView()
    {
        NavigationView navigationView = null;

        if (Owner is NavigationViewItemBase navigationViewItem)
        {
            navigationView = navigationViewItem.GetNavigationView();
        }
        return navigationView;
    }

    bool ISelectionItemProvider.IsSelected
    {
        get
        {
            if (Owner is NavigationViewItem nvi)
            {
                return nvi.IsSelected;
            }
            return false;
        }
    }

    IRawElementProviderSimple ISelectionItemProvider.SelectionContainer
    {
        get
        {
            if (GetParentNavigationView() is { } navview)
            {
                if (FrameworkElementAutomationPeer.CreatePeerForElement(navview) is { } peer)
                {
                    return ProviderFromPeer(peer);
                }
            }

            return null;
        }
    }

    void ISelectionItemProvider.AddToSelection()
    {
        ChangeSelection(true);
    }

    void ISelectionItemProvider.Select()
    {
        ChangeSelection(true);
    }

    void ISelectionItemProvider.RemoveFromSelection()
    {
        ChangeSelection(false);
    }

    void ChangeSelection(bool isSelected)
    {
        if (Owner is NavigationViewItem nvi)
        {
            nvi.IsSelected = isSelected;
        }
    }

    bool HasChildren()
    {
        if (Owner is NavigationViewItem navigationViewItem)
        {
            return navigationViewItem.HasChildren();
        }
        return false;
    }

    enum AutomationOutput
    {
        Position,
        Size,
    }
}
