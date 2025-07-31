// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using iNKORE.UI.WPF.Helpers;
using iNKORE.UI.WPF.Modern.Common;
using iNKORE.UI.WPF.Modern.Controls;
using iNKORE.UI.WPF.Modern.Controls.Primitives;
using iNKORE.UI.WPF.Modern.Input;
using iNKORE.UI.WPF.Modern.Media.Animation;
using static Flow.Bar.Controls.NavigationView.CppWinRTHelpers;
using IControlProtected = Flow.Bar.Controls.NavigationView.CppWinRTHelpers.IControlProtected;

#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable IDE1006 // Naming Styles

namespace Flow.Bar.Controls.NavigationView;

internal enum NavigationRecommendedTransitionDirection
{
    FromOverflow, // mapping to SlideNavigationTransitionInfo FromLeft
    FromLeft, // SlideNavigationTransitionInfo
    FromRight, // SlideNavigationTransitionInfo
    Default // Currently it's mapping to EntranceNavigationTransitionInfo and is subject to change.
}

public partial class NavigationView : ContentControl, IControlProtected
{
    // General items
    private const string c_togglePaneButtonName = "TogglePaneButton";
    private const string c_rootSplitViewName = "RootSplitView";
    private const string c_menuItemsHost = "MenuItemsHost";
    private const string c_paneContentGridName = "PaneContentGrid";
    private const string c_rootGridName = "RootGrid";
    private const string c_contentGridName = "ContentGrid";
    private const string c_searchButtonName = "PaneAutoSuggestButton";
    private const string c_togglePaneTopPadding = "TogglePaneTopPadding";
    private const string c_contentPaneTopPadding = "ContentPaneTopPadding";
    private const string c_navViewBackButton = "NavigationViewBackButton";
    private const string c_navViewBackButtonToolTip = "NavigationViewBackButtonToolTip";
    private const string c_navViewCloseButton = "NavigationViewCloseButton";
    private const string c_navViewCloseButtonToolTip = "NavigationViewCloseButtonToolTip";

    // DisplayMode Left specific items
    private const string c_leftNavPaneAutoSuggestBoxPresenter = "PaneAutoSuggestBoxPresenter";

    private const string c_itemsContainer = "ItemsContainerGrid";
    private const string c_itemsContainerRow = "ItemsContainerRow";
    private const string c_menuItemsScrollViewer = "MenuItemsScrollViewer";

    private const int c_backButtonHeight = 40;
    private const int c_paneToggleButtonWidth = 40;
    private const int c_toggleButtonHeightWhenShouldPreserveNavigationViewRS3Behavior = 56;
    private const int c_backButtonRowDefinition = 1;

    private const int c_mainMenuBlockIndex = 0;

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new NavigationViewAutomationPeer(this);
    }

    private void UnhookEventsAndClearFields(bool isFromDestructor = false)
    {
        if (m_coreTitleBar != null)
        {
            m_coreTitleBar.LayoutMetricsChanged -= OnTitleBarMetricsChanged;
            m_coreTitleBar.IsVisibleChanged -= OnTitleBarIsVisibleChanged;
        }
        if (m_paneToggleButton != null)
        {
            m_paneToggleButton.Click -= OnPaneToggleButtonClick;
        }

        if (m_paneSearchButton != null)
        {
            m_paneSearchButton.Click -= OnPaneSearchButtonClick;
            m_paneSearchButton = null;
        }

        m_itemsContainerSizeChangedRevoker?.Revoke();

        if (m_leftNavRepeater != null)
        {
            m_leftNavRepeater.ElementPrepared -= OnRepeaterElementPrepared;
            m_leftNavRepeater.ElementClearing -= OnRepeaterElementClearing;
            m_leftNavRepeater.IsVisibleChanged -= OnRepeaterIsVisibleChanged;
            m_leftNavRepeaterGettingFocusHelper?.Dispose();
            m_leftNavRepeater = null;
        }

        m_menuItemsCollectionChangedRevoker?.Revoke();

        if (isFromDestructor)
        {
            m_selectionModel.SelectionChanged -= OnSelectionModelSelectionChanged;
        }
    }

    static NavigationView()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NavigationView), new FrameworkPropertyMetadata(typeof(NavigationView)));
    }

    public NavigationView()
    {
        SetValue(s_templateSettingsPropertyKey, new NavigationViewTemplateSettings());

        SizeChanged += OnSizeChanged;

        m_selectionModelSource = [null, null];

        var items = new ObservableCollection<object>();
        SetValue(MenuItemsProperty, items);

        var weakThis = new WeakReference<NavigationView>(this);

        Unloaded += OnUnloaded;
        Loaded += OnLoaded;

        m_selectionModel.SingleSelect = true;
        m_selectionModel.Source = m_selectionModelSource;
        m_selectionModel.SelectionChanged += OnSelectionModelSelectionChanged;
        m_selectionModel.ChildrenRequested += OnSelectionModelChildrenRequested;

        m_navigationViewItemsFactory = new NavigationViewItemsFactory();

        if (ShadowAssist.UseBitmapCache)
        {
            m_bitmapCache = new BitmapCache();
#if NET462_OR_NEWER
            m_bitmapCache.RenderAtScale = VisualTreeHelper.GetDpi(this).PixelsPerDip;
#endif
        }
    }

    private void OnSelectionModelChildrenRequested(SelectionModel selectionModel, SelectionModelChildrenRequestedEventArgs e)
    {
        // this is main menu
        if (e.SourceIndex.GetSize() == 1)
        {
            e.Children = e.Source;
        }
        else if (e.Source is NavigationViewItem)
        {
            // no children
            e.Children = null;
        }
    }

    private void OnSelectionModelSelectionChanged(SelectionModel selectionModel, SelectionModelSelectionChangedEventArgs e)
    {
        var selectedItem = selectionModel.SelectedItem;

        // Ignore this callback if:
        // 1. the SelectedItem property of NavigationView is already set to the item
        //    being passed in this callback. This is because the item has already been selected
        //    via API and we are just updating the m_selectionModel state to accurately reflect the new selection.
        // 2. Template has not been applied yet. SelectionModel's selectedIndex state will get properly updated
        //    after the repeater finishes loading.
        // TODO: Update SelectedItem comparison to work for the exact same item datasource scenario
        if (m_shouldIgnoreNextSelectionChange || selectedItem == SelectedItem || !m_appliedTemplate)
        {
            return;
        }

        bool setSelectedItem = true;
        if (setSelectedItem)
        {
            SetSelectedItemAndExpectItemInvokeWhenSelectionChangedIfNotInvokedFromAPI(selectedItem);
        }
    }

    // We only need to close the flyout if the selected item is a leaf node
    private void CloseFlyoutIfRequired(NavigationViewItem selectedItem)
    {
        bool init()
        {
            if (m_rootSplitView is { } splitView)
            {
                // Check if the pane is closed and if the splitview is in either compact mode.
                var splitViewDisplayMode = splitView.DisplayMode;
                return !splitView.IsPaneOpen && (splitViewDisplayMode == SplitViewDisplayMode.CompactOverlay || splitViewDisplayMode == SplitViewDisplayMode.CompactInline);
            }
            return false;
        }

        init();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // Stop update anything because of PropertyChange during OnApplyTemplate. Update them all together at the end of this function
        m_appliedTemplate = false;

        UnhookEventsAndClearFields();

        IControlProtected controlProtected = this;

        // Set up the pane toggle button click handler
        if (GetTemplateChild(c_togglePaneButtonName) is Button paneToggleButton)
        {
            m_paneToggleButton = paneToggleButton;
            paneToggleButton.Click += OnPaneToggleButtonClick;

            SetPaneToggleButtonAutomationName();

            WindowChrome.SetIsHitTestVisibleInChrome(paneToggleButton, true);
        }

        // Get a pointer to the root SplitView
        if (GetTemplateChild(c_rootSplitViewName) is SplitView splitView)
        {
            m_rootSplitView = splitView;
            splitView.IsPaneOpenChanged += OnSplitViewClosedCompactChanged;
            splitView.DisplayModeChanged += OnSplitViewClosedCompactChanged;

            if (SharedHelpers.IsRS3OrHigher()) // These events are new to RS3/v5 API
            {
                splitView.PaneClosed += OnSplitViewPaneClosed;
                splitView.PaneClosing += OnSplitViewPaneClosing;
                splitView.PaneOpened += OnSplitViewPaneOpened;
                splitView.PaneOpening += OnSplitViewPaneOpening;
            }

            UpdateIsClosedCompact();
        }

        // Change code to NOT do this if we're in top nav mode, to prevent it from being realized:
        if (GetTemplateChild(c_menuItemsHost) is ItemsRepeater leftNavRepeater)
        {
            m_leftNavRepeater = leftNavRepeater;

            // API is currently in preview, so setting this via code.
            // Disabling virtualization for now because of https://github.com/microsoft/microsoft-ui-xaml/issues/2095
            if (leftNavRepeater.Layout is StackLayout stackLayout)
            {
                var stackLayoutImpl = stackLayout;
                stackLayoutImpl.DisableVirtualization = true;
            }

            leftNavRepeater.ElementPrepared += OnRepeaterElementPrepared;
            leftNavRepeater.ElementClearing += OnRepeaterElementClearing;

            leftNavRepeater.IsVisibleChanged += OnRepeaterIsVisibleChanged;

            m_leftNavRepeaterGettingFocusHelper = new GettingFocusHelper(leftNavRepeater);
            m_leftNavRepeaterGettingFocusHelper.GettingFocus += OnRepeaterGettingFocus;

            leftNavRepeater.ItemTemplate = m_navigationViewItemsFactory;
        }

        m_leftNavPaneAutoSuggestBoxPresenter = GetTemplateChild(c_leftNavPaneAutoSuggestBoxPresenter) as ContentControl;

        // Get pointer to the pane content area, for use in the selection indicator animation
        m_paneContentGrid = GetTemplateChild(c_paneContentGridName) as UIElement;

        // Set automation name on search button
        if (GetTemplateChild(c_searchButtonName) is Button button)
        {
            m_paneSearchButton = button;
            button.Click += OnPaneSearchButtonClick;
        }

        if (GetTemplateChild(c_navViewBackButton) is Button backButton)
        {
            m_backButton = backButton;
            backButton.Click += OnBackButtonClicked;

            WindowChrome.SetIsHitTestVisibleInChrome(backButton, true);
        }

        // Register for changes in title bar layout
        if (CoreApplicationViewTitleBar.GetTitleBar(this) is { } coreTitleBar)
        {
            m_coreTitleBar = coreTitleBar;
            coreTitleBar.LayoutMetricsChanged += OnTitleBarMetricsChanged;
            coreTitleBar.IsVisibleChanged += OnTitleBarIsVisibleChanged;

            m_togglePaneTopPadding = GetTemplateChild(c_togglePaneTopPadding) as FrameworkElement;
            m_contentPaneTopPadding = GetTemplateChild(c_contentPaneTopPadding) as FrameworkElement;
        }

        if (GetTemplateChild(c_navViewBackButtonToolTip) is ToolTip backButtonToolTip)
        {
            backButtonToolTip.Content = "Back";
        }

        if (GetTemplateChild(c_navViewCloseButton) is Button closeButton)
        {
            m_closeButton = closeButton;
            closeButton.Click += OnPaneToggleButtonClick;

            WindowChrome.SetIsHitTestVisibleInChrome(closeButton, true);
        }

        if (GetTemplateChild(c_navViewCloseButtonToolTip) is ToolTip closeButtonToolTip)
        {
            closeButtonToolTip.Content = "Close navigation";
        }

        m_itemsContainerRow = GetTemplateChildT<RowDefinition>(c_itemsContainerRow, controlProtected);
        m_menuItemsScrollViewer = GetTemplateChildT<FrameworkElement>(c_menuItemsScrollViewer, controlProtected);

        m_itemsContainerSizeChangedRevoker?.Revoke();
        if (GetTemplateChildT<FrameworkElement>(c_itemsContainer, controlProtected) is { } itemsContainerRow)
        {
            m_itemsContainerSizeChangedRevoker = new FrameworkElementSizeChangedRevoker(itemsContainerRow, OnItemsContainerSizeChanged);
        }

        if (SharedHelpers.IsRS2OrHigher())
        {
            // Get hold of the outermost grid and enable XYKeyboardNavigationMode
            // However, we only want this to work in the content pane + the hamburger button (which is not inside the splitview)
            // so disable it on the grid in the content area of the SplitView
            if (GetTemplateChildT<Grid>(c_rootGridName, controlProtected) is { } rootGrid)
            {
                KeyboardNavigation.SetDirectionalNavigation(rootGrid, KeyboardNavigationMode.Contained);
            }

            if (GetTemplateChildT<Grid>(c_contentGridName, controlProtected) is { } contentGrid)
            {
                KeyboardNavigation.SetDirectionalNavigation(contentGrid, KeyboardNavigationMode.None);
            }
        }

        m_appliedTemplate = true;

        // Do initial setup
        UpdatePaneDisplayMode();
        UpdateHeaderVisibility();
        UpdateTitleBarPadding();
        UpdatePaneTabFocusNavigation();
        UpdateBackAndCloseButtonsVisibility();
        UpdatePaneVisibility();
        UpdateVisualState();
        UpdatePaneLayout();
        UpdatePaneOverlayGroup();
    }

    private void UpdateRepeaterItemsSource(bool forceSelectionModelUpdate)
    {
        object itemsSource;
        {
            object init()
            {
                if (MenuItemsSource is { } menuItemsSource)
                {
                    return menuItemsSource;
                }
                else
                {
                    UpdateSelectionForMenuItems();
                    return MenuItems;
                }
            };
            itemsSource = init();
        }

        // Selection Model has same representation of data regardless
        // of pane mode, so only update if the ItemsSource data itself
        // has changed.
        if (forceSelectionModelUpdate)
        {
            m_selectionModelSource[0] = itemsSource;
        }

        m_menuItemsCollectionChangedRevoker?.Revoke();
        m_menuItemsSource = new InspectingDataSource(itemsSource);
        m_menuItemsCollectionChangedRevoker = new ItemsSourceView.CollectionChangedRevoker(m_menuItemsSource, OnMenuItemsSourceCollectionChanged);

        UpdateLeftRepeaterItemSource(itemsSource);
    }

    private void UpdateLeftRepeaterItemSource(object items)
    {
        UpdateItemsRepeaterItemsSource(m_leftNavRepeater, items);
        // Left pane repeater has a new items source, update pane layout.
        UpdatePaneLayout();
    }

    private static void UpdateItemsRepeaterItemsSource(ItemsRepeater ir,
         object itemsSource)
    {
        if (ir != null)
        {
            ir.ItemsSource = itemsSource;
        }
    }

    private void UpdateFooterRepeaterItemsSource(bool sourceCollectionReset, bool sourceCollectionChanged)
    {
        if (!m_appliedTemplate) return;

        UpdateSelectionForMenuItems();

        if (sourceCollectionChanged || sourceCollectionReset)
        {
            var dataSource = new List<object>();

            m_selectionModelSource[1] = dataSource;
        }
    }

    private void OnNavigationViewItemIsSelectedPropertyChanged(DependencyObject sender, DependencyProperty args)
    {
        if (sender is NavigationViewItem nvi)
        {
            // Check whether the container that triggered this call back is the selected container
            bool isContainerSelectedInModel = IsContainerTheSelectedItemInTheSelectionModel(nvi);
            bool isSelectedInContainer = nvi.IsSelected;

            if (isSelectedInContainer && !isContainerSelectedInModel)
            {
                var indexPath = GetIndexPathForContainer(nvi);
                UpdateSelectionModelSelection(indexPath);
            }
            else if (!isSelectedInContainer && isContainerSelectedInModel)
            {
                var indexPath = GetIndexPathForContainer(nvi);
                var indexPathFromModel = m_selectionModel.SelectedIndex;

                if (indexPathFromModel != null && indexPath.CompareTo(indexPathFromModel) == 0)
                {
                    m_selectionModel.DeselectAt(indexPath);
                }
            }
        }
    }

    private void RaiseItemInvokedForNavigationViewItem(NavigationViewItem nvi)
    {
        object nextItem = null;
        var parentIR = GetParentItemsRepeaterForContainer(nvi);

        if (parentIR.ItemsSourceView is { } itemsSourceView)
        {
            var inspectingDataSource = (InspectingDataSource)itemsSourceView;
            var itemIndex = parentIR.GetElementIndex(nvi);

            // Check that index is NOT -1, meaning it is actually realized
            if (itemIndex != -1)
            {
                // Something went wrong, item might not be realized yet.
                nextItem = inspectingDataSource.GetAt(itemIndex);
            }
        }

        // Determine the recommeded transition direction.
        // Any transitions other than `Default` only apply in top nav scenarios.

        NavigationRecommendedTransitionDirection recommendedDirection;
        {
            static NavigationRecommendedTransitionDirection init()
            {
                return NavigationRecommendedTransitionDirection.Default;
            };
            recommendedDirection = init();
        }

        RaiseItemInvoked(nextItem, nvi, recommendedDirection);
    }

    internal void OnNavigationViewItemInvoked(NavigationViewItem nvi)
    {
        m_shouldRaiseItemInvokedAfterSelection = true;

        var selectedItem = SelectedItem;
        bool updateSelection = m_selectionModel != null;
        if (updateSelection)
        {
            var ip = GetIndexPathForContainer(nvi);

            // Determine if we will update collapse/expand which will happen iff the item has children
            UpdateSelectionModelSelection(ip);
        }

        // Item was invoked but already selected, so raise event here.
        if (selectedItem == SelectedItem)
        {
            RaiseItemInvokedForNavigationViewItem(nvi);
        }

        ClosePaneIfNeccessaryAfterItemIsClicked(nvi);

        if (updateSelection)
        {
            CloseFlyoutIfRequired(nvi);
        }
    }

    private bool IsRootItemsRepeater(DependencyObject element)
    {
        if (element != null)
        {
            return element == m_leftNavRepeater;
        }
        return false;
    }

    private ItemsRepeater GetParentRootItemsRepeaterForContainer(NavigationViewItemBase nvib)
    {
        var parentIR = GetParentItemsRepeaterForContainer(nvib);
        var currentNvib = nvib;
        while (!IsRootItemsRepeater(parentIR))
        {
            currentNvib = GetParentNavigationViewItemForContainer(currentNvib);
            if (currentNvib is null)
            {
                return null;
            }

            parentIR = GetParentItemsRepeaterForContainer(currentNvib);
        }
        return parentIR;
    }

    internal static ItemsRepeater GetParentItemsRepeaterForContainer(NavigationViewItemBase nvib)
    {
        if (VisualTreeHelper.GetParent(nvib) is { } parent)
        {
            if (parent is ItemsRepeater parentIR)
            {
                return parentIR;
            }
        }
        return null;
    }

    private NavigationViewItem GetParentNavigationViewItemForContainer(NavigationViewItemBase nvib)
    {
        // TODO: This scenario does not find parent items when in a flyout, which causes problems if item if first loaded
        // straight in the flyout. Fix. This logic can be merged with the 'GetIndexPathForContainer' logic below.
        DependencyObject parent = GetParentItemsRepeaterForContainer(nvib);
        if (!IsRootItemsRepeater(parent))
        {
            while (parent != null)
            {
                parent = VisualTreeHelper.GetParent(parent);
                if (parent is NavigationViewItem nvi)
                {
                    return nvi;
                }
            }
        }
        return null;
    }

    private IndexPath GetIndexPathForContainer(NavigationViewItemBase nvib)
    {
        var path = new List<int>();

        DependencyObject child = nvib;
        var parent = VisualTreeHelper.GetParent(child);
        if (parent == null)
        {
            return IndexPath.CreateFromIndices(path);
        }

        // Search through VisualTree for a root itemsrepeater
        while (parent != null && !IsRootItemsRepeater(parent))
        {
            if (parent is ItemsRepeater parentIR)
            {
                if (child is UIElement childElement)
                {
                    path.Insert(0, parentIR.GetElementIndex(childElement));
                }
            }
            child = parent;
            parent = VisualTreeHelper.GetParent(parent);
        }

        // If item is in one of the disconnected ItemRepeaters, account for that in IndexPath calculations
        {
            if (parent is ItemsRepeater parentIR)
            {
                path.Insert(0, parentIR.GetElementIndex(child as UIElement));
            }
        }

        path.Insert(0, c_mainMenuBlockIndex);

        return IndexPath.CreateFromIndices(path);
    }

    internal void OnRepeaterElementPrepared(ItemsRepeater ir, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is NavigationViewItemBase nvib)
        {
            var nvibImpl = nvib;
            nvibImpl.SetNavigationViewParent(this);
            nvibImpl.IsTopLevelItem = IsTopLevelItem(nvib);

            // Visual state info propagation
            NavigationViewRepeaterPosition position;
            {
                NavigationViewRepeaterPosition init()
                {
                    return NavigationViewRepeaterPosition.LeftNav;
                }
                position = init();
            }
            nvibImpl.Position = position;

            if (GetParentNavigationViewItemForContainer(nvib) is { } parentNVI)
            {
                var parentNVIImpl = parentNVI;
                var itemDepth = parentNVIImpl.Depth + 1;
                nvibImpl.Depth = itemDepth;
            }
            else
            {
                nvibImpl.Depth = 0;
            }

            // Apply any custom container styling
            ApplyCustomMenuItemContainerStyling(nvib, ir, args.Index);

            if (args.Element is NavigationViewItem nvi)
            {
                // Propagate depth to children items if they exist
                int childDepth;
                {
                    int init()
                    {
                        return nvibImpl.Depth + 1;

                    }
                    childDepth = init();
                }

                // Register for item events
                InputHelper.AddTappedHandler(nvi, OnNavigationViewItemTapped);
                nvi.KeyDown += OnNavigationViewItemKeyDown;
                nvi.IsSelectedChanged += OnNavigationViewItemIsSelectedPropertyChanged;
            }
        }
    }

    private void ApplyCustomMenuItemContainerStyling(NavigationViewItemBase nvib, ItemsRepeater ir, int index)
    {
        if (MenuItemContainerStyle is { } menuItemContainerStyle)
        {
            nvib.Style = menuItemContainerStyle;
        }
        else if (MenuItemContainerStyleSelector is { } menuItemContainerStyleSelector)
        {
            if (ir.ItemsSourceView is { } itemsSourceView)
            {
                if (itemsSourceView.GetAt(index) is { } item)
                {
                    if (menuItemContainerStyleSelector.SelectStyle(item, nvib) is { } selectedStyle)
                    {
                        nvib.Style = selectedStyle;
                    }
                }
            }
        }
    }

    internal void OnRepeaterElementClearing(ItemsRepeater ir, ItemsRepeaterElementClearingEventArgs args)
    {
        if (args.Element is NavigationViewItemBase nvib)
        {
            var nvibImpl = nvib;
            nvibImpl.Depth = 0;
            nvibImpl.IsTopLevelItem = false;
            if (nvib is NavigationViewItem nvi)
            {
                // Revoke all the events that we were listing to on the item
                InputHelper.RemoveTappedHandler(nvi, OnNavigationViewItemTapped);
                nvi.KeyDown -= OnNavigationViewItemKeyDown;
                nvi.IsSelectedChanged -= OnNavigationViewItemIsSelectedPropertyChanged;
            }
        }
    }

    internal NavigationViewItemsFactory GetNavigationViewItemsFactory() { return m_navigationViewItemsFactory; }

    protected override Size MeasureOverride(Size availableSize)
    {
        LayoutUpdated -= OnLayoutUpdated;
        LayoutUpdated += OnLayoutUpdated;
        m_layoutUpdatedToken = true;

        return base.MeasureOverride(availableSize);
    }

    private void OnLayoutUpdated(object sender, object e)
    {
        // We only need to handle once after MeasureOverride, so revoke the token.
        LayoutUpdated -= OnLayoutUpdated;
        m_layoutUpdatedToken = false;

        if (m_orientationChangedPendingAnimation)
        {
            m_orientationChangedPendingAnimation = false;
            AnimateSelectionChanged(SelectedItem);
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs args)
    {
        var width = args.NewSize.Width;
        UpdateOpenPaneWidth(width);
        UpdateAdaptiveLayout(width);
        UpdateTitleBarPadding();
        UpdateBackAndCloseButtonsVisibility();
        UpdatePaneLayout();
        UpdatePaneOverlayGroup();
        UpdatePaneButtonsWidths();
    }

    private void OnItemsContainerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePaneLayout();
    }

    private void UpdateOpenPaneWidth(double width)
    {
        if (m_rootSplitView != null)
        {
            var m_openPaneWidth = Math.Max(0.0, Math.Min(width, OpenPaneLength));

            TemplateSettings.OpenPaneWidth = m_openPaneWidth;
        }
    }

    // forceSetDisplayMode: On first call to SetDisplayMode, force setting to initial values
    private void UpdateAdaptiveLayout(double width, bool forceSetDisplayMode = false)
    {
        if (m_rootSplitView == null)
        {
            return;
        }

        // If we decide we want it to animate open/closed when you resize the
        // window we'll have to change how we figure out the initial state
        // instead of this:
        m_initialListSizeStateSet = false; // see UpdateIsClosedCompact()

        NavigationViewDisplayMode displayMode;

        if (width >= ExpandedMinimalModeThresholdWidth)
        {
            displayMode = NavigationViewDisplayMode.Expanded;
            UpdatePaneToggleButtonVisibility(false);
        }
        else
        {
            displayMode = NavigationViewDisplayMode.Minimal;
            UpdatePaneToggleButtonVisibility(true);
        }

        if (!forceSetDisplayMode && m_initialNonForcedModeUpdate)
        {
            if (displayMode == NavigationViewDisplayMode.Minimal ||
                displayMode == NavigationViewDisplayMode.Compact)
            {
                ClosePane();
            }
            m_initialNonForcedModeUpdate = false;
        }

        var previousMode = DisplayMode;
        SetDisplayMode(displayMode, forceSetDisplayMode);

        if (displayMode == NavigationViewDisplayMode.Expanded && IsPaneVisible)
        {
            if (!m_wasForceClosed)
            {
                OpenPane();
            }
        }

        if (previousMode == NavigationViewDisplayMode.Expanded
            && (displayMode == NavigationViewDisplayMode.Compact ||
                displayMode == NavigationViewDisplayMode.Minimal))
        {
            m_initialListSizeStateSet = false;
            ClosePane();
        }

        if (displayMode == NavigationViewDisplayMode.Expanded
            && (previousMode == NavigationViewDisplayMode.Compact ||
                previousMode == NavigationViewDisplayMode.Minimal))
        {
            OpenPane();
        }
    }

    private void UpdatePaneLayout()
    {
        double totalAvailableHeight;
        {
            totalAvailableHeight = init();
            double init()
            {
                if (m_itemsContainerRow is { } paneContentRow)
                {
                    // 20px is the padding between the two item lists
                    return paneContentRow.ActualHeight - 29;
                }
                return 0.0;
            }
        }

        // Only continue if we have a positive amount of space to manage.
        if (totalAvailableHeight > 0)
        {
            // We need this value more than twice, so cache it.
            var totalAvailableHeightHalf = totalAvailableHeight / 2;

            double heightForMenuItems;
            {
                heightForMenuItems = init();
                double init()
                {
                    // We know the actual height of footer items, so use that to determine how to split pane.
                    if (m_leftNavRepeater is { } menuItems)
                    {
                        var menuItemsActualHeight = menuItems.ActualHeight;

                        if (totalAvailableHeight >= menuItemsActualHeight ||
                            0 <= totalAvailableHeightHalf)
                        {
                            // We have enough space for two so let everyone get as much as they need.
                            // Or menu items exceed over the half, so let's limit them.
                            return totalAvailableHeight;
                        }
                        else
                        {
                            // Both are more than half the height, so split evenly.
                            return totalAvailableHeightHalf;
                        }
                    }
                    else
                    {
                        // Couldn't determine the menuItems.
                        // Let's just take all the height and let the other repeater deal with it.
                        return totalAvailableHeight;
                    }
                }
            }

            if (m_menuItemsScrollViewer is { } menuItemsScrollViewer)
            {
                // Update max height for menu items.
                menuItemsScrollViewer.MaxHeight = heightForMenuItems;
            }
        }
    }

    private void OnPaneToggleButtonClick(object sender, RoutedEventArgs args)
    {
        if (IsPaneOpen)
        {
            m_wasForceClosed = true;
            ClosePane();
        }
        else
        {
            m_wasForceClosed = false;
            OpenPane();
        }
    }

    private void OnPaneSearchButtonClick(object sender, RoutedEventArgs args)
    {
        m_wasForceClosed = false;
        OpenPane();

        if (AutoSuggestBox is { } autoSuggestBox)
        {
            Dispatcher.BeginInvoke(() =>
            {
                autoSuggestBox.Focus();
            }, DispatcherPriority.Loaded);
        }
    }

    // Call this when you want an uncancellable open
    private void OpenPane()
    {
        try
        {
            m_isOpenPaneForInteraction = true;
            IsPaneOpen = true;
        }
        finally
        {
            m_isOpenPaneForInteraction = false;
        }
    }

    // Call this when you want an uncancellable close
    private void ClosePane()
    {
        try
        {
            m_isOpenPaneForInteraction = true;
            IsPaneOpen = false; // the SplitView is two-way bound to this value 
        }
        finally
        {
            m_isOpenPaneForInteraction = false;
        }
    }

    // Call this when NavigationView itself is going to trigger a close
    // where you will stop the close if the cancel is triggered
    private bool AttemptClosePaneLightly()
    {
        bool pendingPaneClosingCancel = false;

        if (SharedHelpers.IsRS3OrHigher())
        {
            var eventArgs = new NavigationViewPaneClosingEventArgs();
            PaneClosing?.Invoke(this, eventArgs);
            pendingPaneClosingCancel = eventArgs.Cancel;
        }

        if (!pendingPaneClosingCancel || m_wasForceClosed)
        {
            m_blockNextClosingEvent = true;
            ClosePane();
            return true;
        }

        return false;
    }

    private void OnSplitViewClosedCompactChanged(DependencyObject sender, DependencyProperty args)
    {
        if (args == SplitView.IsPaneOpenProperty ||
            args == SplitView.DisplayModeProperty)
        {
            UpdateIsClosedCompact();
        }
    }

    private void OnSplitViewPaneClosed(DependencyObject sender, object obj)
    {
        PaneClosed?.Invoke(this, null);
    }

    private void OnSplitViewPaneClosing(DependencyObject sender, SplitViewPaneClosingEventArgs args)
    {
        bool pendingPaneClosingCancel = false;
        if (PaneClosing != null)
        {
            if (!m_blockNextClosingEvent) // If this is true, we already sent one out "manually" and don't need to forward SplitView's event
            {
                var eventArgs = new NavigationViewPaneClosingEventArgs();
                eventArgs.SplitViewClosingArgs(args);
                PaneClosing(this, eventArgs);
                pendingPaneClosingCancel = eventArgs.Cancel;
            }
            else
            {
                m_blockNextClosingEvent = false;
            }
        }

        if (!pendingPaneClosingCancel) // will be set in above event!
        {
            if (m_rootSplitView is { } splitView)
            {
                if (m_leftNavRepeater is { })
                {
                    if (splitView.DisplayMode == SplitViewDisplayMode.CompactOverlay || splitView.DisplayMode == SplitViewDisplayMode.CompactInline)
                    {
                        // See UpdateIsClosedCompact 'RS3+ animation timing enhancement' for explanation:
                        VisualStateManager.GoToState(this, "ListSizeCompact", useTransitions: true);
                    }
                }
            }
        }
    }

    private void OnSplitViewPaneOpened(DependencyObject sender, object obj)
    {
        PaneOpened?.Invoke(this, null);
    }

    private void OnSplitViewPaneOpening(DependencyObject sender, object obj)
    {
        if (m_leftNavRepeater != null)
        {
            // See UpdateIsClosedCompact 'RS3+ animation timing enhancement' for explanation:
            VisualStateManager.GoToState(this, "ListSizeFull", useTransitions: true);
        }

        PaneOpening?.Invoke(this, null);
    }

    private void UpdateIsClosedCompact(bool useTransitions = true)
    {
        if (m_rootSplitView is { } splitView)
        {
            // Check if the pane is closed and if the splitview is in either compact mode.
            var splitViewDisplayMode = splitView.DisplayMode;
            m_isClosedCompact = !splitView.IsPaneOpen && (splitViewDisplayMode == SplitViewDisplayMode.CompactOverlay || splitViewDisplayMode == SplitViewDisplayMode.CompactInline);
            VisualStateManager.GoToState(this, m_isClosedCompact ? "ClosedCompact" : "NotClosedCompact", useTransitions);

            // Set the initial state of the list size
            if (!m_initialListSizeStateSet)
            {
                m_initialListSizeStateSet = true;
                VisualStateManager.GoToState(this, m_isClosedCompact ? "ListSizeCompact" : "ListSizeFull", useTransitions);
            }

            UpdateTitleBarPadding();
            UpdateBackAndCloseButtonsVisibility();
        }
    }

    private void UpdatePaneButtonsWidths()
    {
        double newButtonWidths;
        {
            double init()
            {
                if (DisplayMode == NavigationViewDisplayMode.Minimal)
                {
                    return c_paneToggleButtonWidth;
                }
                return CompactPaneLength;
            }
            newButtonWidths = init();
        }

        TemplateSettings.PaneToggleButtonWidth = newButtonWidths;
        TemplateSettings.SmallerPaneToggleButtonWidth = Math.Max(0, newButtonWidths-8);
    }

    private void OnBackButtonClicked(object sender, RoutedEventArgs args)
    {
        var eventArgs = new NavigationViewBackRequestedEventArgs();
        BackRequested?.Invoke(this, eventArgs);
    }

    private bool IsOverlay()
    {
        if (m_rootSplitView is { } splitView)
        {
            return splitView.DisplayMode == SplitViewDisplayMode.Overlay;
        }
        else
        {
            return false;
        }
    }

    private bool IsLightDismissible()
    {
        if (m_rootSplitView is { } splitView)
        {
            return splitView.DisplayMode != SplitViewDisplayMode.Inline && splitView.DisplayMode != SplitViewDisplayMode.CompactInline;
        }
        else
        {
            return false;
        }
    }

    private bool ShouldShowBackButton()
    {
        if (m_backButton != null && !ShouldPreserveNavigationViewRS3Behavior())
        {
            if (DisplayMode == NavigationViewDisplayMode.Minimal && IsPaneOpen)
            {
                return false;
            }

            return ShouldShowBackOrCloseButton();
        }

        return false;
    }

    private bool ShouldShowCloseButton()
    {
        if (m_backButton != null && !ShouldPreserveNavigationViewRS3Behavior() && m_closeButton != null)
        {
            if (!IsPaneOpen)
            {
                return false;
            }

            if (DisplayMode != NavigationViewDisplayMode.Minimal)
            {
                return false;
            }

            return ShouldShowBackOrCloseButton();
        }

        return false;
    }

    private bool ShouldShowBackOrCloseButton()
    {
        return IsBackButtonVisible;
    }

    // The automation name and tooltip for the pane toggle button changes depending on whether it is open or closed
    // put the logic here as it will be called in a couple places
    private void SetPaneToggleButtonAutomationName()
    {
        string navigationName;
        if (IsPaneOpen)
        {
            navigationName = "Close navigation";
        }
        else
        {
            navigationName = "Open navigation";
        }

        if (m_paneToggleButton is { } paneToggleButton)
        {
            AutomationProperties.SetName(paneToggleButton, navigationName);
            var toolTip = new ToolTip
            {
                Content = navigationName
            };
            ToolTipService.SetToolTip(paneToggleButton, toolTip);
        }
    }

    private static readonly Point s_frame1point1 = new(0.9, 0.1);
    private static readonly Point s_frame1point2 = new(1.0, 0.2);
    private static readonly Point s_frame2point1 = new(0.1, 0.9);
    private static readonly Point s_frame2point2 = new(0.2, 1.0);

    // Please clear the field m_lastSelectedItemPendingAnimationInTopNav when calling this method to prevent garbage value and incorrect animation
    // when the layout is invalidated as it's called in OnLayoutUpdated.
    private void AnimateSelectionChanged(object nextItem)
    {
        UIElement prevIndicator = m_activeIndicator;
        UIElement nextIndicator = FindSelectionIndicator(nextItem);

        bool haveValidAnimation = false;
        // It's possible that AnimateSelectionChanged is called multiple times before the first animation is complete.
        // To have better user experience, if the selected target is the same, keep the first animation
        // If the selected target is not the same, abort the first animation and launch another animation.
        if (m_prevIndicator != null || m_nextIndicator != null) // There is ongoing animation
        {
            if (nextIndicator != null && m_nextIndicator == nextIndicator) // animate to the same target, just wait for animation complete
            {
                if (prevIndicator != null && prevIndicator != m_prevIndicator)
                {
                    ResetElementAnimationProperties(prevIndicator, 0.0);
                }
                haveValidAnimation = true;
            }
            else
            {
                // If the last animation is still playing, force it to complete.
                OnAnimationComplete(null, null);
            }
        }

        if (!haveValidAnimation)
        {
            UIElement paneContentGrid = m_paneContentGrid;

            if ((prevIndicator != nextIndicator) && paneContentGrid != null && prevIndicator != null && nextIndicator != null && SharedHelpers.IsAnimationsEnabled)
            {
                // Make sure both indicators are visible and in their original locations
                ResetElementAnimationProperties(prevIndicator, 1.0);
                ResetElementAnimationProperties(nextIndicator, 1.0);

                // get the item positions in the pane
                Point point = new(0, 0);
                double prevPos;
                double nextPos;

                Point prevPosPoint = prevIndicator.SafeTransformToVisual(paneContentGrid).Transform(point);
                Point nextPosPoint = nextIndicator.SafeTransformToVisual(paneContentGrid).Transform(point);
                Size prevSize = prevIndicator.RenderSize;
                Size nextSize = nextIndicator.RenderSize;

                bool areElementsAtSameDepth = false;
                prevPos = prevPosPoint.Y;
                nextPos = nextPosPoint.Y;
                areElementsAtSameDepth = prevPosPoint.X == nextPosPoint.X;

                var storyboard = new Storyboard { FillBehavior = FillBehavior.Stop };

                if (!areElementsAtSameDepth)
                {
                    bool isNextBelow = prevPosPoint.Y < nextPosPoint.Y;
                    if (prevIndicator.RenderSize.Height > prevIndicator.RenderSize.Width)
                    {
                        PlayIndicatorNonSameLevelAnimations(prevIndicator, true, !isNextBelow, storyboard.Children);
                    }
                    else
                    {
                        PlayIndicatorNonSameLevelTopPrimaryAnimation(prevIndicator, true, storyboard.Children);
                    }

                    if (nextIndicator.RenderSize.Height > nextIndicator.RenderSize.Width)
                    {
                        PlayIndicatorNonSameLevelAnimations(nextIndicator, false, isNextBelow, storyboard.Children);
                    }
                    else
                    {
                        PlayIndicatorNonSameLevelTopPrimaryAnimation(nextIndicator, false, storyboard.Children);
                    }

                }
                else
                {
                    double outgoingEndPosition = nextPos - prevPos;
                    double incomingStartPosition = prevPos - nextPos;

                    // Play the animation on both the previous and next indicators
                    PlayIndicatorAnimations(prevIndicator,
                        0,
                        outgoingEndPosition,
                        prevSize,
                        nextSize,
                        true,
                        storyboard.Children);
                    PlayIndicatorAnimations(nextIndicator,
                        incomingStartPosition,
                        0,
                        prevSize,
                        nextSize,
                        false,
                        storyboard.Children);
                }

                m_prevIndicator = prevIndicator;
                m_nextIndicator = nextIndicator;

                storyboard.Completed += OnAnimationComplete;

                storyboard.Begin(this, true);
                storyboard.Pause(this);
                storyboard.SeekAlignedToLastTick(this, TimeSpan.Zero, TimeSeekOrigin.BeginTime);
                Dispatcher.BeginInvoke(() =>
                {
                    storyboard.Resume(this);
                }, DispatcherPriority.Loaded);
            }
            else
            {
                // if all else fails, or if animations are turned off, attempt to correctly set the positions and opacities of the indicators.
                ResetElementAnimationProperties(prevIndicator, 0.0);
                ResetElementAnimationProperties(nextIndicator, 1.0);
            }

            m_activeIndicator = nextIndicator;
        }
    }

    private void PlayIndicatorNonSameLevelAnimations(UIElement indicator, bool isOutgoing, bool fromTop, TimelineCollection animations)
    {
        // Determine scaling of indicator (whether it is appearing or dissapearing)
        double beginScale = isOutgoing ? 1.0 : 0.0;
        double endScale = isOutgoing ? 0.0 : 1.0;
        var scaleAnim = new DoubleAnimationUsingKeyFrames
        {
            KeyFrames =
            {
                new DiscreteDoubleKeyFrame(beginScale, KeyTime.FromPercent(0.0)),
                new SplineDoubleKeyFrame(endScale, KeyTime.FromPercent(1.0), new KeySpline(new Point(0.8,0), s_frame2point2)),
            },
            Duration = TimeSpan.FromMilliseconds(600)
        };
        animations.Add(scaleAnim);

        // Determine where the indicator is animating from/to
        Size size = indicator.RenderSize;
        double dimension = size.Height;
        double newCenter = fromTop ? 0.0 : dimension;
        var indicatorCenterPoint = new Point
        {
            Y = newCenter
        };

        Storyboard.SetTarget(scaleAnim, indicator);
        Storyboard.SetTargetProperty(scaleAnim, s_scaleYPath);
        PrepareIndicatorForAnimation(indicator, indicatorCenterPoint);
    }

    private void PlayIndicatorNonSameLevelTopPrimaryAnimation(UIElement indicator, bool isOutgoing, TimelineCollection animations)
    {
        // Determine scaling of indicator (whether it is appearing or dissapearing)
        double beginScale = isOutgoing ? 1.0 : 0.0;
        double endScale = isOutgoing ? 0.0 : 1.0;
        var scaleAnim = new DoubleAnimationUsingKeyFrames
        {
            KeyFrames =
            {
                new DiscreteDoubleKeyFrame(beginScale, KeyTime.FromPercent(0.0)),
                new SplineDoubleKeyFrame(endScale, KeyTime.FromPercent(1.0), new KeySpline(new Point(0.8,0), s_frame2point2)),
            },
            Duration = TimeSpan.FromMilliseconds(600)
        };
        animations.Add(scaleAnim);

        // Determine where the indicator is animating from/to
        Size size = indicator.RenderSize;
        double newCenter = size.Width / 2;
        var indicatorCenterPoint = new Point
        {
            Y = newCenter
        };

        Storyboard.SetTarget(scaleAnim, indicator);
        Storyboard.SetTargetProperty(scaleAnim, s_scaleXPath);
        PrepareIndicatorForAnimation(indicator, indicatorCenterPoint);
    }

    private void PlayIndicatorAnimations(UIElement indicator, double from, double to, Size beginSize, Size endSize, bool isOutgoing, TimelineCollection animations)
    {
        Size size = indicator.RenderSize;
        double dimension = size.Height;

        double beginScale = 1.0;
        double endScale = 1.0;

        var posAnim = new DoubleAnimationUsingKeyFrames
        {
            KeyFrames =
            {
                new DiscreteDoubleKeyFrame(from < to ? from : (from + (dimension * (beginScale - 1))), KeyTime.FromPercent(0.0)),
                new DiscreteDoubleKeyFrame(from < to ? (to + (dimension * (endScale - 1))) : to, KeyTime.FromPercent(0.333)),
            },
            Duration = TimeSpan.FromMilliseconds(600)
        };
        Storyboard.SetTarget(posAnim, indicator);
        animations.Add(posAnim);

        var scaleAnim = new DoubleAnimationUsingKeyFrames
        {
            KeyFrames =
            {
                new DiscreteDoubleKeyFrame(beginScale, KeyTime.FromPercent(0.0)),
                new SplineDoubleKeyFrame(
                    Math.Abs(to - from) / dimension + (from < to ? endScale : beginScale),
                    KeyTime.FromPercent(0.333),
                    new KeySpline(s_frame1point1, s_frame1point2)),
                new SplineDoubleKeyFrame(endScale, KeyTime.FromPercent(1.0), new KeySpline(s_frame2point1, s_frame2point2)),
            },
            Duration = TimeSpan.FromMilliseconds(600)
        };
        Storyboard.SetTarget(scaleAnim, indicator);
        animations.Add(scaleAnim);

        var centerAnim = new DoubleAnimationUsingKeyFrames
        {
            KeyFrames =
            {
                new DiscreteDoubleKeyFrame(from < to ? 0.0 : dimension, KeyTime.FromPercent(0.0)),
                new DiscreteDoubleKeyFrame(from < to ? dimension : 0.0, KeyTime.FromPercent(1.0)),
            },
            Duration = TimeSpan.FromMilliseconds(200)
        };
        Storyboard.SetTarget(centerAnim, indicator);
        animations.Add(centerAnim);

        if (isOutgoing)
        {
            // fade the outgoing indicator so it looks nice when animating over the scroll area
            var opacityAnim = new DoubleAnimationUsingKeyFrames
            {
                KeyFrames =
                {
                    new DiscreteDoubleKeyFrame(1.0, KeyTime.FromPercent(0.0)),
                    new DiscreteDoubleKeyFrame(1.0, KeyTime.FromPercent(0.333)),
                    new SplineDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0), new KeySpline(s_frame2point1, s_frame2point2)),
                },
                Duration = TimeSpan.FromMilliseconds(600)
            };
            Storyboard.SetTarget(opacityAnim, indicator);
            Storyboard.SetTargetProperty(opacityAnim, s_opacityPath);
            animations.Add(opacityAnim);
        }

        Storyboard.SetTargetProperty(posAnim, s_translateYPath);
        Storyboard.SetTargetProperty(scaleAnim, s_scaleYPath);
        Storyboard.SetTargetProperty(centerAnim, s_centerYPath);

        PrepareIndicatorForAnimation(indicator);
    }

    private void PrepareIndicatorForAnimation(UIElement indicator, Point? centerPoint = null)
    {
        if (!(indicator.RenderTransform is TransformGroup transformGroup &&
              transformGroup.Children.Count == 2 &&
              transformGroup.Children[0] is ScaleTransform &&
              transformGroup.Children[1] is TranslateTransform))
        {
            indicator.RenderTransform = new TransformGroup
            {
                Children =
                {
                    new ScaleTransform(),
                    new TranslateTransform()
                }
            };
        }

        if (centerPoint.HasValue)
        {
            var scaleTransform = (ScaleTransform)((TransformGroup)indicator.RenderTransform).Children[0];
            scaleTransform.CenterX = centerPoint.Value.X;
            scaleTransform.CenterY = centerPoint.Value.Y;
        }

        if (ShadowAssist.UseBitmapCache && indicator.CacheMode == null)
        {
            indicator.CacheMode = m_bitmapCache;
        }
    }

    private void OnAnimationComplete(object sender, EventArgs args)
    {
        var indicator = m_prevIndicator;
        ResetElementAnimationProperties(indicator, 0.0);
        m_prevIndicator = null;

        indicator = m_nextIndicator;
        ResetElementAnimationProperties(indicator, 1.0);
        m_nextIndicator = null;
    }

    private static void ResetElementAnimationProperties(UIElement element, double desiredOpacity)
    {
        if (element != null)
        {
            element.Opacity = desiredOpacity;

            if (element.RenderTransform is TransformGroup transformGroup &&
                transformGroup.Children.Count == 2 &&
                transformGroup.Children[0] is ScaleTransform scaleTransform &&
                transformGroup.Children[1] is TranslateTransform translateTransform)
            {
                translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
                translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                scaleTransform.ClearValue(ScaleTransform.CenterXProperty);
                scaleTransform.ClearValue(ScaleTransform.CenterYProperty);
            }
            else
            {
                element.ClearValue(UIElement.RenderTransformProperty);
            }

            element.BeginAnimation(OpacityProperty, null);
        }
    }

    private NavigationViewItemBase NavigationViewItemBaseOrSettingsContentFromData(object data)
    {
        return GetContainerForData<NavigationViewItemBase>(data);
    }

    private NavigationViewItem NavigationViewItemOrSettingsContentFromData(object data)
    {
        return GetContainerForData<NavigationViewItem>(data);
    }

    private bool ShouldPreserveNavigationViewRS3Behavior()
    {
        // Since RS4, we support backbutton
        return m_backButton == null;
    }

    private UIElement FindSelectionIndicator(object item)
    {
        if (item != null)
        {
            if (NavigationViewItemOrSettingsContentFromData(item) is { } container)
            {
                if (container.GetSelectionIndicator() is { } indicator)
                {
                    return indicator;
                }
                else
                {
                    // Indicator was not found, so maybe the layout hasn't updated yet.
                    // So let's do that now.
                    container.UpdateLayout();
                    return container.GetSelectionIndicator();
                }
            }
        }
        return null;
    }

    private void RaiseSelectionChangedEvent(object nextItem, NavigationRecommendedTransitionDirection recommendedDirection = NavigationRecommendedTransitionDirection.Default)
    {
        var eventArgs = new NavigationViewSelectionChangedEventArgs
        {
            SelectedItem = nextItem
        };
        if (NavigationViewItemBaseOrSettingsContentFromData(nextItem) is { } container)
        {
            eventArgs.SelectedItemContainer = container;
        }
        eventArgs.RecommendedNavigationTransitionInfo = CreateNavigationTransitionInfo(recommendedDirection);
        SelectionChanged?.Invoke(this, eventArgs);
    }

    // SelectedItem change can be invoked by API or user's action like clicking. if it's not from API, m_shouldRaiseInvokeItemInSelectionChange would be true
    // If nextItem is selectionsuppressed, we should undo the selection. We didn't undo it OnSelectionChange because we want change by API has the same undo logic.
    private void ChangeSelection(object prevItem, object nextItem)
    {
        // Other transition other than default only apply to topnav
        // when clicking overflow on topnav, transition is from bottom
        // otherwise if prevItem is on left side of nextActualItem, transition is from left
        //           if prevItem is on right side of nextActualItem, transition is from right
        // click on Settings item is considered Default
        NavigationRecommendedTransitionDirection recommendedDirection;
        {
            static NavigationRecommendedTransitionDirection init()
            {
                return NavigationRecommendedTransitionDirection.Default;
            }
            recommendedDirection = init();
        }

        // Bug 17850504, Customer may use NavigationViewItem.IsSelected in ItemInvoke or SelectionChanged Event.
        // To keep the logic the same as RS4, ItemInvoke is before unselect the old item
        // And SelectionChanged is after we selected the new item.
        var selectedItem = SelectedItem;
        if (m_shouldRaiseItemInvokedAfterSelection)
        {
            // If selection changed inside ItemInvoked, the flag does not get said to false and the event get's raised again,so we need to set it to false now!
            m_shouldRaiseItemInvokedAfterSelection = false;
            RaiseItemInvoked(nextItem, NavigationViewItemOrSettingsContentFromData(nextItem), recommendedDirection);
        }
        // Selection was modified inside ItemInvoked, skip everything here!
        if (selectedItem != SelectedItem)
        {
            return;
        }
        UnselectPrevItem(prevItem, nextItem);
        ChangeSelectStatusForItem(nextItem, true /*selected*/);

        try
        {
            // Selection changed and we need to notify UIA
            // HOWEVER expand collapse can also trigger if an item can expand/collapse
            // There are multiple cases when selection changes:
            // - Through click on item with no children -> No expand/collapse change
            // - Through click on item with children -> Expand/collapse change
            // - Through API with item without children -> No expand/collapse change
            // - Through API with item with children -> No expand/collapse change
            if (!m_shouldIgnoreUIASelectionRaiseAsExpandCollapseWillRaise)
            {
                if (FrameworkElementAutomationPeer.FromElement(this) is AutomationPeer peer)
                {
                    var navViewItemPeer = (NavigationViewAutomationPeer)peer;
                    navViewItemPeer.RaiseSelectionChangedEvent(
                        prevItem, nextItem
                    );
                }
            }
        }
        finally
        {
            m_shouldIgnoreUIASelectionRaiseAsExpandCollapseWillRaise = false;
        }

        RaiseSelectionChangedEvent(nextItem, recommendedDirection);
        AnimateSelectionChanged(nextItem);

        if (NavigationViewItemOrSettingsContentFromData(nextItem) is { } nvi)
        {
            ClosePaneIfNeccessaryAfterItemIsClicked(nvi);
        }
    }

    private void UpdateSelectionModelSelection(IndexPath ip)
    {
        m_selectionModel.SelectAt(ip);
    }

    private void RaiseItemInvoked(object item,
        NavigationViewItemBase container = null,
        NavigationRecommendedTransitionDirection recommendedDirection = NavigationRecommendedTransitionDirection.Default)
    {
        var invokedItem = item;
        var invokedContainer = container;

        var eventArgs = new NavigationViewItemInvokedEventArgs();

        if (container != null)
        {
            invokedItem = container.Content;
        }
        else
        {
            // InvokedItem is container for Settings, but Content of item for other ListViewItem
            if (NavigationViewItemBaseOrSettingsContentFromData(item) is { } containerFromData)
            {
                invokedItem = containerFromData.Content;
                invokedContainer = containerFromData;
            }
        }
        eventArgs.InvokedItem = invokedItem;
        eventArgs.InvokedItemContainer = invokedContainer;
        eventArgs.RecommendedNavigationTransitionInfo = CreateNavigationTransitionInfo(recommendedDirection);
        ItemInvoked?.Invoke(this, eventArgs);
    }

    // forceSetDisplayMode: On first call to SetDisplayMode, force setting to initial values
    private void SetDisplayMode(NavigationViewDisplayMode displayMode, bool forceSetDisplayMode = false)
    {
        // Need to keep the VisualStateGroup "DisplayModeGroup" updated even if the actual
        // display mode is not changed. This is due to the fact that there can be a transition between
        // 'Minimal' and 'MinimalWithBackButton'.
        UpdateVisualStateForDisplayModeGroup(displayMode);

        if (forceSetDisplayMode || DisplayMode != displayMode)
        {
            // Update header visibility based on what the new display mode will be
            UpdateHeaderVisibility(displayMode);

            UpdatePaneTabFocusNavigation();

            RaiseDisplayModeChanged(displayMode);
        }
    }

    // To support TopNavigationView, DisplayModeGroup in visualstate(We call it VisualStateDisplayMode) is decoupled with DisplayMode.
    // The VisualStateDisplayMode is the combination of TopNavigationView, DisplayMode, PaneDisplayMode.
    // Here is the mapping:
    //    TopNav . Minimal
    //    PaneDisplayMode.Left || (PaneDisplayMode.Auto && DisplayMode.Expanded) . Expanded
    //    PaneDisplayMode.LeftCompact || (PaneDisplayMode.Auto && DisplayMode.Compact) . Compact
    //    Map others to Minimal or MinimalWithBackButton 
    private NavigationViewVisualStateDisplayMode GetVisualStateDisplayMode(NavigationViewDisplayMode displayMode)
    {
        if (displayMode == NavigationViewDisplayMode.Expanded)
        {
            return NavigationViewVisualStateDisplayMode.Expanded;
        }

        if (displayMode == NavigationViewDisplayMode.Compact)
        {
            return NavigationViewVisualStateDisplayMode.Compact;
        }

        // In minimal mode, when the NavView is closed, the HeaderContent doesn't have
        // its own dedicated space, and must 'share' the top of the NavView with the 
        // pane toggle button ('hamburger' button) and the back button.
        // When the NavView is open, the close button is taking space instead of the back button.
        if (ShouldShowBackButton() || ShouldShowCloseButton())
        {
            return NavigationViewVisualStateDisplayMode.MinimalWithBackButton;
        }
        else
        {
            return NavigationViewVisualStateDisplayMode.Minimal;
        }
    }

    private void UpdateVisualStateForDisplayModeGroup(NavigationViewDisplayMode displayMode, bool useTransitions = false)
    {
        if (m_rootSplitView is { } splitView)
        {
            var visualStateDisplayMode = GetVisualStateDisplayMode(displayMode);
            var visualStateName = "";
            var splitViewDisplayMode = SplitViewDisplayMode.Overlay;
            var visualStateNameMinimal = "Minimal";

            switch (visualStateDisplayMode)
            {
                case NavigationViewVisualStateDisplayMode.MinimalWithBackButton:
                    visualStateName = "MinimalWithBackButton";
                    splitViewDisplayMode = SplitViewDisplayMode.Overlay;
                    break;
                case NavigationViewVisualStateDisplayMode.Minimal:
                    visualStateName = visualStateNameMinimal;
                    splitViewDisplayMode = SplitViewDisplayMode.Overlay;
                    break;
                case NavigationViewVisualStateDisplayMode.Compact:
                    visualStateName = "Compact";
                    splitViewDisplayMode = SplitViewDisplayMode.CompactOverlay;
                    break;
                case NavigationViewVisualStateDisplayMode.Expanded:
                    visualStateName = "Expanded";
                    splitViewDisplayMode = SplitViewDisplayMode.CompactInline;
                    break;
            }

            // When the pane is made invisible we need to collapse the pane part of the SplitView
            if (!IsPaneVisible)
            {
                splitViewDisplayMode = SplitViewDisplayMode.CompactOverlay;
            }

            VisualStateManager.GoToState(this, visualStateName, useTransitions);
            splitView.DisplayMode = splitViewDisplayMode;
        }
    }

    private void OnNavigationViewItemTapped(object sender, TappedRoutedEventArgs args)
    {
        if (sender is NavigationViewItem nvi)
        {
            OnNavigationViewItemInvoked(nvi);
            nvi.Focus();
            args.Handled = true;
        }
    }

    private void OnNavigationViewItemKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter ||
            args.Key == Key.Space)
        {
            if (args.IsRepeat)
            {
                return;
            }
        }

        if (sender is NavigationViewItem nvi)
        {
            HandleKeyEventForNavigationViewItem(nvi, args);
        }
    }

    private void HandleKeyEventForNavigationViewItem(NavigationViewItem nvi, KeyEventArgs args)
    {
        var key = args.Key;
        switch (key)
        {
            case Key.Enter:
            case Key.Space:
                args.Handled = true;
                OnNavigationViewItemInvoked(nvi);
                break;
            case Key.Home:
                args.Handled = true;
                KeyboardFocusFirstItemFromItem(nvi);
                break;
            case Key.End:
                args.Handled = true;
                KeyboardFocusLastItemFromItem(nvi);
                break;
            case Key.Down:
                NavigationView.FocusNextDownItem(nvi, args);
                break;
            case Key.Up:
                FocusNextUpItem(nvi, args);
                break;
            case Key.Right:
                FocusNextRightItem(nvi, args);
                break;
        }
    }

    private void FocusNextUpItem(NavigationViewItem nvi, KeyEventArgs args)
    {
        if (args.OriginalSource != nvi)
        {
            return;
        }

        bool shouldHandleFocus = true;
        var nviImpl = nvi;
        var nextFocusableElement = FocusManagerEx.FindNextFocusableElement(FocusNavigationDirection.Up);

        if (nextFocusableElement is NavigationViewItem nextFocusableNVI)
        {
            var nextFocusableNVIImpl = nextFocusableNVI;

            if (nextFocusableNVIImpl.Depth == nviImpl.Depth)
            {
                // If we not at the top of the list for our current depth and the item above us has children, check whether we should move focus onto a child
                {
                    // Traversing up a list where XYKeyboardFocus will result in correct behavior
                    shouldHandleFocus = false;
                }
            }
        }

        // We are at the top of the list, focus on parent
        if (shouldHandleFocus && !args.Handled && nviImpl.Depth > 0)
        {
            if (GetParentNavigationViewItemForContainer(nvi) is { } parentContainer)
            {
                args.Handled = parentContainer.Focus(/*FocusState.Keyboard*/);
            }
        }
    }

    // If item has focusable children, move focus to first focusable child, otherise just defer to default XYKeyboardFocus behavior
    private static void FocusNextDownItem(NavigationViewItem nvi, KeyEventArgs args)
    {
        if (args.OriginalSource != nvi)
        {
            return;
        }

        // WPF
        if (!args.Handled)
        {
            args.Handled = nvi.MoveFocus(new TraversalRequest(FocusNavigationDirection.Down));
        }
    }

    // WPF
    private static void FocusNextRightItem(NavigationViewItem nvi, KeyEventArgs args)
    {
        args.Handled = nvi.MoveFocus(new TraversalRequest(FocusNavigationDirection.Right));
    }

    private void KeyboardFocusFirstItemFromItem(NavigationViewItemBase nvib)
    {
        UIElement firstElement;
        {
            UIElement init()
            {
                var parentIR = GetParentRootItemsRepeaterForContainer(nvib);
                return parentIR.TryGetElement(0);
            }
            firstElement = init();
        }

        if (firstElement is Control controlFirst)
        {
            controlFirst.Focus();
        }
    }

    private void KeyboardFocusLastItemFromItem(NavigationViewItemBase nvib)
    {
        var parentIR = GetParentRootItemsRepeaterForContainer(nvib);

        if (parentIR.ItemsSourceView is { } itemsSourceView)
        {
            var lastIndex = itemsSourceView.Count - 1;
            if (parentIR.TryGetElement(lastIndex) is { } lastElement)
            {
                if (lastElement is Control controlLast)
                {
                    controlLast.Focus(/*FocusState.Programmatic*/);
                }
            }
        }
    }

    private void OnRepeaterGettingFocus(object sender, GettingFocusEventArgs args)
    {
        // if focus change was invoked by tab key
        // and there is selected item in ItemsRepeater that gatting focus
        // we should put focus on selected item
        if (m_tabKeyPrecedesFocusChange && args.InputDevice == FocusInputDeviceKind.Keyboard && m_selectionModel.SelectedIndex != null)
        {
            if (args.OldFocusedElement is { } oldFocusedElement)
            {
                if (sender is ItemsRepeater newRootItemsRepeater)
                {
                    bool isFocusOutsideCurrentRootRepeater;
                    {
                        isFocusOutsideCurrentRootRepeater = init();
                        bool init()
                        {
                            bool isFocusOutsideCurrentRootRepeater = true;
                            var treeWalkerCursor = oldFocusedElement;

                            // check if last focused element was in same root repeater
                            while (treeWalkerCursor != null)
                            {
                                if (treeWalkerCursor is NavigationViewItemBase oldFocusedNavigationItemBase)
                                {
                                    var oldParentRootRepeater = GetParentRootItemsRepeaterForContainer(oldFocusedNavigationItemBase);
                                    isFocusOutsideCurrentRootRepeater = oldParentRootRepeater != newRootItemsRepeater;
                                    break;
                                }

                                treeWalkerCursor = VisualTreeHelper.GetParent(treeWalkerCursor);
                            }

                            return isFocusOutsideCurrentRootRepeater;
                        }
                    }

                    object rootRepeaterForSelectedItem;
                    {
                        rootRepeaterForSelectedItem = init();
                        object init()
                        {
                            return m_leftNavRepeater;
                        }
                    }

                    // If focus is coming from outside the root repeater,
                    // and selected item is within current repeater
                    // we should put focus on selected item
                    if (args is IGettingFocusEventArgs2 argsAsIGettingFocusEventArgs2)
                    {
                        if (newRootItemsRepeater == rootRepeaterForSelectedItem && isFocusOutsideCurrentRootRepeater)
                        {
                            var selectedContainer = GetContainerForIndexPath(m_selectionModel.SelectedIndex, true /* lastVisible */);
                            if (argsAsIGettingFocusEventArgs2.TrySetNewFocusedElement(selectedContainer))
                            {
                                args.Handled = true;
                            }
                        }
                    }
                }
            }
        }

        m_tabKeyPrecedesFocusChange = false;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        m_tabKeyPrecedesFocusChange = false;
        base.OnPreviewKeyDown(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var eventArgs = e;
        var key = eventArgs.Key;

        bool handled = false;
        m_tabKeyPrecedesFocusChange = false;

        switch (key)
        {
            case Key.Tab:
                // arrow keys navigation through ItemsRepeater don't get here
                // so handle tab key to distinguish between tab focus and arrow focus navigation
                m_tabKeyPrecedesFocusChange = true;
                break;
            case Key.Left:
                bool isAltPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);

                if (isAltPressed && IsPaneOpen && IsLightDismissible())
                {
                    handled = AttemptClosePaneLightly();
                }

                break;
        }

        eventArgs.Handled = handled;

        base.OnKeyDown(e);
    }

    internal static object MenuItemFromContainer(DependencyObject container)
    {
        if (container != null)
        {
            if (container is NavigationViewItemBase nvib)
            {
                if (GetParentItemsRepeaterForContainer(nvib) is { } parentRepeater)
                {
                    var containerIndex = parentRepeater.GetElementIndex(nvib);
                    if (containerIndex >= 0)
                    {
                        return GetItemFromIndex(parentRepeater, containerIndex);
                    }
                }
            }
        }
        return null;
    }

    internal DependencyObject ContainerFromMenuItem(object item)
    {
        if (item != null)
        {
            return NavigationViewItemBaseOrSettingsContentFromData(item);
        }

        return null;
    }

    internal SplitView GetSplitView()
    {
        return m_rootSplitView;
    }

    private static NavigationTransitionInfo CreateNavigationTransitionInfo(NavigationRecommendedTransitionDirection recommendedTransitionDirection)
    {
        // In current implementation, if click is from overflow item, just recommend FromRight Slide animation.
        if (recommendedTransitionDirection == NavigationRecommendedTransitionDirection.FromOverflow)
        {
            recommendedTransitionDirection = NavigationRecommendedTransitionDirection.FromRight;
        }

        if ((recommendedTransitionDirection == NavigationRecommendedTransitionDirection.FromLeft
            || recommendedTransitionDirection == NavigationRecommendedTransitionDirection.FromRight)
            && SharedHelpers.IsRS5OrHigher())
        {
            SlideNavigationTransitionInfo sliderNav = new();
            SlideNavigationTransitionEffect effect =
                recommendedTransitionDirection == NavigationRecommendedTransitionDirection.FromRight ?
                SlideNavigationTransitionEffect.FromRight :
                SlideNavigationTransitionEffect.FromLeft;
            // PR 1895355: Bug 17724768: Remove Side-to-Side navigation transition velocity key
            // https://microsoft.visualstudio.com/_git/os/commit/7d58531e69bc8ad1761cff938d8db25f6fb6a841
            // We want to use Effect, but it's not in all os of rs5. as a workaround, we only apply effect to the os which is already remove velocity key.
            if (sliderNav is ISlideNavigationTransitionInfo2)
            {
                sliderNav.Effect = effect;
            }
            return sliderNav;
        }
        else
        {
            EntranceNavigationTransitionInfo defaultInfo = new();
            return defaultInfo;
        }
    }

    private void OnMenuItemsSourceCollectionChanged(object sender, object args)
    {
        if (m_leftNavRepeater is { } repeater)
        {
            repeater.UpdateLayout();
        }
        UpdatePaneLayout();
    }

    private void OnSelectedItemPropertyChanged(DependencyPropertyChangedEventArgs args)
    {

        var newItem = args.NewValue;
        var oldItem = args.OldValue;

        ChangeSelection(oldItem, newItem);
    }

    private void SetSelectedItemAndExpectItemInvokeWhenSelectionChangedIfNotInvokedFromAPI(object item)
    {
        SelectedItem = item;
    }

    private void ChangeSelectStatusForItem(object item, bool selected)
    {
        if (NavigationViewItemOrSettingsContentFromData(item) is { } container)
        {
            // If we unselect an item, ListView doesn't tolerate setting the SelectedItem to null. 
            // Instead we remove IsSelected from the item itself, and it make ListView to unselect it.
            // If we select an item, we follow the unselect to simplify the code.
            container.IsSelected = selected;
        }
        else if (selected)
        {
            // If we are selecting an item and have not found a realized container for it,
            // we may need to manually resolve a container for this in order to update the
            // SelectionModel's selected IndexPath.
            var ip = GetIndexPathOfItem(item);
            if (ip != null && ip.GetSize() > 0)
            {
                // The SelectedItem property has already been updated. So we want to block any logic from executing
                // in the SelectionModel selection changed callback.
                try
                {
                    m_shouldIgnoreNextSelectionChange = true;
                    UpdateSelectionModelSelection(ip);
                }
                finally
                {
                    m_shouldIgnoreNextSelectionChange = false;
                }
            }
        }
    }

    private void UnselectPrevItem(object prevItem, object nextItem)
    {
        if (prevItem != null && prevItem != nextItem)
        {
            var setIgnoreNextSelectionChangeToFalse = !m_shouldIgnoreNextSelectionChange;
            try
            {
                m_shouldIgnoreNextSelectionChange = true;
                ChangeSelectStatusForItem(prevItem, false /*selected*/);
            }
            finally
            {
                if (setIgnoreNextSelectionChangeToFalse)
                {
                    m_shouldIgnoreNextSelectionChange = false;
                }
            }
        }
    }

    private void UpdatePaneOverlayGroup(bool useTransitions = true)
    {
        var splitView = m_rootSplitView;
        if (splitView != null)
        {
            if (IsPaneOpen && (splitView.DisplayMode == SplitViewDisplayMode.CompactOverlay || splitView.DisplayMode == SplitViewDisplayMode.Overlay))
            {
                VisualStateManager.GoToState(this, "PaneOverlaying", useTransitions);
            }
            else
            {
                VisualStateManager.GoToState(this, "PaneNotOverlaying", useTransitions);
            }
        }
    }

    private void UpdateVisualState(bool useTransitions = false)
    {
        if (m_appliedTemplate)
        {
            var box = AutoSuggestBox;
            VisualStateManager.GoToState(this, box != null ? "AutoSuggestBoxVisible" : "AutoSuggestBoxCollapsed", useTransitions);

            VisualStateManager.GoToState(this, "SettingsCollapsed", useTransitions);
        }
    }

    private static void CoerceToGreaterThanZero(ref double value)
    {
        // Property coercion for OpenPaneLength, CompactPaneLength, ExpandedMinimalModeThresholdWidth
        value = Math.Max(value, 0.0);
    }

    private void PropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        DependencyProperty property = args.Property;

        if (property == IsPaneOpenProperty)
        {
            OnIsPaneOpenChanged();
            UpdateVisualStateForDisplayModeGroup(DisplayMode);
        }
        else if (property == ExpandedMinimalModeThresholdWidthProperty)
        {
            UpdateAdaptiveLayout(ActualWidth);
        }
        else if (property == AlwaysShowHeaderProperty || property == HeaderProperty)
        {
            UpdateHeaderVisibility();
        }
        else if (property == SelectedItemProperty)
        {
            OnSelectedItemPropertyChanged(args);
        }
        else if (property == IsBackButtonVisibleProperty)
        {
            UpdateBackAndCloseButtonsVisibility();
            UpdateAdaptiveLayout(ActualWidth);

            // Enabling back button shifts grid instead of resizing, so let's update the layout.
            if (m_backButton is { } backButton)
            {
                backButton.UpdateLayout();
            }
            UpdatePaneLayout();
        }
        else if (property == MenuItemsSourceProperty)
        {
            UpdateRepeaterItemsSource(true /*forceSelectionModelUpdate*/);
        }
        else if (property == MenuItemsProperty)
        {
            UpdateRepeaterItemsSource(true /*forceSelectionModelUpdate*/);
        }
        else if (property == IsPaneVisibleProperty)
        {
            UpdatePaneVisibility();
            UpdateVisualStateForDisplayModeGroup(DisplayMode);

            // When NavView is in expaneded mode with fixed window size, setting IsPaneVisible to false doesn't closes the pane
            // We manually close/open it for this case
            if (!IsPaneVisible && IsPaneOpen)
            {
                ClosePane();
            }

            if (IsPaneVisible && DisplayMode == NavigationViewDisplayMode.Expanded && !IsPaneOpen)
            {
                OpenPane();
            }
        }
        else if (property == CompactPaneLengthProperty)
        {
            // Update pane-button-grid width when pane is closed and we are not in minimal
            UpdatePaneButtonsWidths();
        }
        else if (property == IsTitleBarAutoPaddingEnabledProperty)
        {
            UpdateTitleBarPadding();
        }
        else if (property == MenuItemTemplateProperty ||
            property == MenuItemTemplateSelectorProperty)
        {
            SyncItemTemplates();
        }
    }

    private void UpdateNavigationViewItemsFactory()
    {
        object newItemTemplate = MenuItemTemplate;
        newItemTemplate ??= MenuItemTemplateSelector;
        m_navigationViewItemsFactory.UserElementFactory(newItemTemplate);
    }

    private void SyncItemTemplates()
    {
        UpdateNavigationViewItemsFactory();
    }

    private void OnRepeaterIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            Dispatcher.BeginInvoke(() =>
            {
                var repeater = (ItemsRepeater)sender;
                if (repeater.IsLoaded)
                {
                    OnRepeaterLoaded(sender, null);
                }
            }, DispatcherPriority.Loaded);
        }
    }

    private void OnRepeaterLoaded(object sender, RoutedEventArgs args)
    {
        if (SelectedItem is { } item)
        {
            if (NavigationViewItemOrSettingsContentFromData(item) is { } navViewItem)
            {
                navViewItem.IsSelected = true;
            }

            AnimateSelectionChanged(item);
        }
    }

    // If app is .net app, the lifetime of NavigationView maybe depends on garbage collection.
    // Unlike other revoker, TitleBar is in global space and we need to stop receiving changed event when it's unloaded.
    // So we do hook it in Loaded and Unhook it in Unloaded
    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (m_coreTitleBar is { } coreTitleBar)
        {
            coreTitleBar.LayoutMetricsChanged -= OnTitleBarMetricsChanged;
            coreTitleBar.IsVisibleChanged -= OnTitleBarIsVisibleChanged;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (m_coreTitleBar is { } coreTitleBar)
        {
            coreTitleBar.LayoutMetricsChanged += OnTitleBarMetricsChanged;
            coreTitleBar.IsVisibleChanged += OnTitleBarIsVisibleChanged;
        }
        // Update pane buttons now since we the CompactPaneLength is actually known now.
        UpdatePaneButtonsWidths();
    }

    private void OnIsPaneOpenChanged()
    {
        var isPaneOpen = IsPaneOpen;
        if (isPaneOpen && m_wasForceClosed)
        {
            m_wasForceClosed = false; // remove the pane open flag since Pane is opened.
        }
        else if (!m_isOpenPaneForInteraction && !isPaneOpen)
        {
            if (m_rootSplitView is { } splitView)
            {
                // splitview.IsPaneOpen and nav.IsPaneOpen is two way binding. If nav.IsPaneOpen=false and splitView.IsPaneOpen=true,
                // then the pane has been closed by API and we treat it as a forced close.
                // If, however, splitView.IsPaneOpen=false, then nav.IsPaneOpen is just following the SplitView here and the pane
                // was closed, for example, due to app window resizing. We don't set the force flag in this situation.
                m_wasForceClosed = splitView.IsPaneOpen;
            }
            else
            {
                // If there is no SplitView (for example it hasn't been loaded yet) then nav.IsPaneOpen was set directly
                // so we treat it as a closed force.
                m_wasForceClosed = true;
            }
        }

        SetPaneToggleButtonAutomationName();
        UpdatePaneTabFocusNavigation();
        UpdatePaneOverlayGroup();

        UpdatePaneButtonsWidths();
    }

    private void UpdatePaneToggleButtonVisibility(bool visible)
    {
        TemplateSettings.PaneToggleButtonVisibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdatePaneDisplayMode()
    {
        if (!m_appliedTemplate)
        {
            return;
        }

        UpdateAdaptiveLayout(ActualWidth, true /*forceSetDisplayMode*/);

        UpdateContentBindingsForPaneDisplayMode();
        UpdateRepeaterItemsSource(false /*forceSelectionModelUpdate*/);
        UpdateFooterRepeaterItemsSource(false /*sourceCollectionReset*/, false /*sourceCollectionChanged*/);
        if (SelectedItem is { })
        {
            m_orientationChangedPendingAnimation = true;
        }
    }

    private void UpdatePaneVisibility(bool useTransitions = false)
    {
        if (IsPaneVisible)
        {
            TemplateSettings.LeftPaneVisibility = Visibility.Visible;

            VisualStateManager.GoToState(this, "PaneVisible", useTransitions);
        }
        else
        {
            TemplateSettings.LeftPaneVisibility = Visibility.Collapsed;

            VisualStateManager.GoToState(this, "PaneCollapsed", useTransitions);
        }
    }

    private void UpdateContentBindingsForPaneDisplayMode()
    {
        UIElement autoSuggestBoxContentControl;
        autoSuggestBoxContentControl = m_leftNavPaneAutoSuggestBoxPresenter;

        if (autoSuggestBoxContentControl != null)
        {
            SharedHelpers.SetBinding("AutoSuggestBox", autoSuggestBoxContentControl, ContentControl.ContentProperty);
        }
    }

    private void UpdateHeaderVisibility()
    {
        if (!m_appliedTemplate)
        {
            return;
        }

        UpdateHeaderVisibility(DisplayMode);
    }

    private void UpdateHeaderVisibility(NavigationViewDisplayMode displayMode, bool useTransitions = false)
    {
        // Ignore AlwaysShowHeader property in case DisplayMode is Minimal and it's not Top NavigationView
        bool showHeader = AlwaysShowHeader || displayMode == NavigationViewDisplayMode.Minimal;

        // Like bug 17517627, Customer like WallPaper Studio 10 expects a HeaderContent visual even if Header() is null. 
        // App crashes when they have dependency on that visual, but the crash is not directly state that it's a header problem.   
        // NavigationView doesn't use quirk, but we determine the version by themeresource.
        // As a workaround, we 'quirk' it for RS4 or before release. if it's RS4 or before, HeaderVisible is not related to Header().
        // If theme resource is RS5 or later, we will not show header if header is null.
        if (SharedHelpers.IsRS5OrHigher())
        {
            showHeader = Header != null && showHeader;
        }
        VisualStateManager.GoToState(this, showHeader ? "HeaderVisible" : "HeaderCollapsed", useTransitions);
    }

    private void UpdatePaneTabFocusNavigation()
    {
        if (!m_appliedTemplate)
        {
            return;
        }

        if (SharedHelpers.IsRS2OrHigher())
        {
            KeyboardNavigationMode mode = KeyboardNavigationMode.Local;

            if (m_rootSplitView is { } splitView)
            {
                // If the pane is open in an overlay (light-dismiss) mode, trap keyboard focus inside the pane
                if (IsPaneOpen && (splitView.DisplayMode == SplitViewDisplayMode.Overlay || splitView.DisplayMode == SplitViewDisplayMode.CompactOverlay))
                {
                    mode = KeyboardNavigationMode.Cycle;
                }
            }

            if (m_paneContentGrid is { } paneContentGrid)
            {
                //paneContentGrid.TabFocusNavigation(mode);
                KeyboardNavigation.SetTabNavigation(paneContentGrid, mode);
            }
        }
    }

    private void UpdateBackAndCloseButtonsVisibility()
    {
        if (!m_appliedTemplate)
        {
            return;
        }

        var shouldShowBackButton = ShouldShowBackButton();
        var backButtonVisibility = shouldShowBackButton ? Visibility.Visible : Visibility.Collapsed;

        TemplateSettings.BackButtonVisibility = backButtonVisibility;

        if (m_backButton is { } backButton)
        {
            backButton.Visibility = backButtonVisibility;
        }

        if (m_closeButton is { } closeButton)
        {
            var closeButtonVisibility = ShouldShowCloseButton() ? Visibility.Visible : Visibility.Collapsed;

            closeButton.Visibility = closeButtonVisibility;
        }

        if (m_paneContentGrid is { } paneContentGridAsUIE)
        {
            if (paneContentGridAsUIE is Grid paneContentGrid)
            {
                var rowDefs = paneContentGrid.RowDefinitions;

                if (rowDefs.Count >= c_backButtonRowDefinition)
                {
                    var rowDef = rowDefs[c_backButtonRowDefinition];

                    int backButtonRowHeight = 0;
                    if (!IsOverlay() && shouldShowBackButton)
                    {
                        backButtonRowHeight = c_backButtonHeight;
                    }
                    else if (ShouldPreserveNavigationViewRS3Behavior())
                    {
                        // This row represented the height of the hamburger+margin in RS3 and prior
                        backButtonRowHeight = c_toggleButtonHeightWhenShouldPreserveNavigationViewRS3Behavior;
                    }

                    var length = GridLengthHelper.FromPixels(backButtonRowHeight);
                    rowDef.Height = length;
                }
            }
        }

        UpdateTitleBarPadding();
    }

    private void UpdateSelectionForMenuItems()
    {
        // Allow customer to set selection by NavigationViewItem.IsSelected.
        // If there are more than two items are set IsSelected=true, the first one is actually selected.
        // If SelectedItem is set, IsSelected is ignored.
        //         <NavigationView.MenuItems>
        //              <NavigationViewItem Content = "Collection" IsSelected = "True" / >
        //         </NavigationView.MenuItems>
        if (SelectedItem == null)
        {
            // firstly check Menu items
            if (MenuItems is IList menuItems)
            {
                UpdateSelectedItemFromMenuItems(menuItems);
            }
        }
    }

    private bool UpdateSelectedItemFromMenuItems(IList menuItems, bool foundFirstSelected = false)
    {
        for (int i = 0; i < menuItems.Count; i++)
        {
            if (menuItems[i] is NavigationViewItem item)
            {
                if (item.IsSelected)
                {
                    if (!foundFirstSelected)
                    {
                        try
                        {
                            m_shouldIgnoreNextSelectionChange = true;
                            SelectedItem = item;
                            foundFirstSelected = true;
                        }
                        finally
                        {
                            m_shouldIgnoreNextSelectionChange = false;
                        }
                    }
                    else
                    {
                        item.IsSelected = false;
                    }
                }
            }
        }
        return foundFirstSelected;
    }

    private void OnTitleBarMetricsChanged(object sender, object args)
    {
        UpdateTitleBarPadding();
    }

    private void OnTitleBarIsVisibleChanged(CoreApplicationViewTitleBar sender, object args)
    {
        UpdateTitleBarPadding();
    }

    private void ClosePaneIfNeccessaryAfterItemIsClicked(NavigationViewItem selectedContainer)
    {
        if (IsPaneOpen &&
            DisplayMode != NavigationViewDisplayMode.Expanded &&
            !m_shouldIgnoreNextSelectionChange)
        {
            ClosePane();
        }
    }

    private void UpdateTitleBarPadding()
    {
        if (!m_appliedTemplate)
        {
            return;
        }

        double topPadding = 0;

        if (m_coreTitleBar is { } coreTitleBar)
        {
            bool needsTopPadding = false;

            // Do not set a top padding when the IsTitleBarAutoPaddingEnabled property is set to False.
            if (IsTitleBarAutoPaddingEnabled)
            {
                if (ShouldPreserveNavigationViewRS3Behavior())
                {
                    needsTopPadding = true;
                }
                else
                {
                    // For RS4 apps maintain the behavior that we shipped for RS4.
                    // We keep this behavior for app compact purposes.
                    needsTopPadding = !coreTitleBar.ExtendViewIntoTitleBar;
                }
            }

            if (needsTopPadding)
            {
                // Only add extra padding if the NavView is the "root" of the app,
                // but not if the app is expanding into the titlebar
                UIElement root = (Window.GetWindow(this) ?? Application.Current?.MainWindow)?.Content as UIElement;
                GeneralTransform gt = this.SafeTransformToVisual(root);
                Point pos = gt.Transform(new Point());

                if (pos.Y == 0.0)
                {
                    topPadding = coreTitleBar.Height;
                }
            }

            {
                if (m_togglePaneTopPadding is { } fe)
                {
                    fe.Height = topPadding;
                }
            }

            {
                if (m_contentPaneTopPadding is { } fe)
                {
                    fe.Height = topPadding;
                }
            }
        }

        if (TemplateSettings is { } templateSettings)
        {
            // 0.0 and 0.00000000 is not the same in double world.
            // Try to reduce the number of TopPadding update event.
            // Epsilon is 0.1 here.
            if (Math.Abs(templateSettings.TopPadding - topPadding) > 0.1)
            {
                TemplateSettings.TopPadding = topPadding;
            }
        }
    }

    private void RaiseDisplayModeChanged(NavigationViewDisplayMode displayMode)
    {
        SetValue(s_displayModePropertyKey, displayMode);
        var eventArgs = new NavigationViewDisplayModeChangedEventArgs
        {
            DisplayMode = displayMode
        };
        DisplayModeChanged?.Invoke(this, eventArgs);
    }

    private T GetContainerForData<T>(object data) where T : class
    {
        if (data == null)
        {
            return null;
        }

        if (data is T nvi)
        {
            return nvi;
        }

        // First conduct a basic top level search in main menu, which should succeed for a lot of scenarios.
        var mainRepeater = m_leftNavRepeater;
        var itemIndex = GetIndexFromItem(mainRepeater, data);
        if (itemIndex >= 0)
        {
            if (mainRepeater.TryGetElement(itemIndex) is { } container)
            {
                return container as T;
            }
        }

        // If unsuccessful, unfortunately we are going to have to search through the whole tree
        // TODO: Either fix or remove implementation for TopNav.
        // It may not be required due to top nav rarely having realized children in its default state.
        {
            if (SearchEntireTreeForContainer(mainRepeater, data) is { } container)
            {
                return container as T;
            }
        }

        return null;
    }

    private static UIElement SearchEntireTreeForContainer(ItemsRepeater rootRepeater, object data)
    {
        // TODO: Temporary inefficient solution that results in unnecessary time complexity, fix.
        var index = GetIndexFromItem(rootRepeater, data);
        if (index != -1)
        {
            return rootRepeater.TryGetElement(index);
        }

        return null;
    }

    private static int GetIndexFromItem(ItemsRepeater ir, object data)
    {
        if (ir != null)
        {
            if (ir.ItemsSourceView is { } itemsSourceView)
            {
                return itemsSourceView.IndexOf(data);
            }
        }
        return -1;
    }

    private static object GetItemFromIndex(ItemsRepeater ir, int index)
    {
        if (ir != null)
        {
            if (ir.ItemsSourceView is { } itemsSourceView)
            {
                return itemsSourceView.GetAt(index);
            }
        }
        return null;
    }

    private IndexPath GetIndexPathOfItem(object data)
    {
        if (data is NavigationViewItemBase nvib)
        {
            return GetIndexPathForContainer(nvib);
        }
        return new IndexPath([]);
    }

    private UIElement GetContainerForIndex(int index)
    {
        if (m_leftNavRepeater.TryGetElement(index) is { } container)
        {
            return container as NavigationViewItemBase;
        }
        return null;
    }

    private NavigationViewItemBase GetContainerForIndexPath(IndexPath ip, bool lastVisible = false)
    {
        if (ip != null && ip.GetSize() > 0)
        {
            if (GetContainerForIndex(ip.GetAt(1)) is { } container)
            {
                if (lastVisible)
                {
                    if (container is NavigationViewItem nvi)
                    {
                        return nvi;
                    }
                }

                // TODO: Fix below for top flyout scenario once the flyout is introduced in the XAML.
                // We want to be able to retrieve containers for items that are in the flyout.
                // This will return null if requesting children containers of
                // items in the primary list, or unrealized items in the overflow popup.
                // However this should not happen.
                return GetContainerForIndexPath(container, ip, lastVisible);
            }
        }
        return null;
    }

    private static NavigationViewItemBase GetContainerForIndexPath(UIElement firstContainer, IndexPath ip, bool lastVisible)
    {
        var container = firstContainer;
        if (ip.GetSize() > 2)
        {
            for (int i = 2; i < ip.GetSize(); i++)
            {
                bool succeededGettingNextContainer = false;
                if (container is NavigationViewItem nvi)
                {
                    if (lastVisible)
                    {
                        return nvi;
                    }
                }
                // If any of the above checks failed, it means something went wrong and we have an index for a non-existent repeater.
                if (!succeededGettingNextContainer)
                {
                    return null;
                }
            }
        }
        return container as NavigationViewItemBase;
    }

    private bool IsContainerTheSelectedItemInTheSelectionModel(NavigationViewItemBase nvib)
    {
        if (m_selectionModel.SelectedItem is { } selectedItem)
        {
            if (selectedItem is not NavigationViewItemBase selectedItemContainer)
            {
                selectedItemContainer = GetContainerForIndexPath(m_selectionModel.SelectedIndex);
            }

            return selectedItemContainer == nvib;
        }
        return false;
    }

    internal ItemsRepeater LeftNavRepeater()
    {
        return m_leftNavRepeater;
    }

    internal NavigationViewItem GetSelectedContainer()
    {
        if (SelectedItem is { } selectedItem)
        {
            if (selectedItem is NavigationViewItem selectedItemContainer)
            {
                return selectedItemContainer;
            }
            else
            {
                return NavigationViewItemOrSettingsContentFromData(selectedItem);
            }
        }
        return null;
    }

    private bool IsTopLevelItem(NavigationViewItemBase nvib)
    {
        return IsRootItemsRepeater(GetParentItemsRepeaterForContainer(nvib));
    }

    DependencyObject IControlProtected.GetTemplateChild(string childName)
    {
        return GetTemplateChild(childName);
    }

#if NET462_OR_NEWER
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);

        if (ShadowAssist.UseBitmapCache)
        {
            m_bitmapCache.RenderAtScale = newDpi.PixelsPerDip;
        }
    }
#endif

    private bool m_initialNonForcedModeUpdate = true;

    private readonly NavigationViewItemsFactory m_navigationViewItemsFactory;

    // Visual components
    private Button m_paneToggleButton;
    private SplitView m_rootSplitView;
    private RowDefinition m_itemsContainerRow;
    private FrameworkElement m_menuItemsScrollViewer;
    private UIElement m_paneContentGrid;
    private Button m_paneSearchButton;
    private Button m_backButton;
    private Button m_closeButton;
    private ItemsRepeater m_leftNavRepeater;

    // Indicator animations
    private UIElement m_prevIndicator;
    private UIElement m_nextIndicator;
    private UIElement m_activeIndicator;

    private FrameworkElement m_togglePaneTopPadding;
    private FrameworkElement m_contentPaneTopPadding;

    private CoreApplicationViewTitleBar m_coreTitleBar;

    private ContentControl m_leftNavPaneAutoSuggestBoxPresenter;

    // Event Tokens
    private bool m_layoutUpdatedToken;
    private FrameworkElementSizeChangedRevoker m_itemsContainerSizeChangedRevoker;

    private ItemsSourceView.CollectionChangedRevoker m_menuItemsCollectionChangedRevoker;

    private bool m_wasForceClosed = false;
    private bool m_isClosedCompact = false;
    private bool m_blockNextClosingEvent = false;
    private bool m_initialListSizeStateSet = false;

    private readonly SelectionModel m_selectionModel = new();
    private readonly List<object> m_selectionModelSource;

    private ItemsSourceView m_menuItemsSource = null;

    private bool m_appliedTemplate = false;

    // flag is used to stop recursive call. eg:
    // Customer select an item from SelectedItem property->ChangeSelection update ListView->LIstView raise OnSelectChange(we want stop here)->change property do do animation again.
    // Customer clicked listview->listview raised OnSelectChange->SelectedItem property changed->ChangeSelection->Undo the selection by SelectedItem(prevItem) (we want it stop here)->ChangeSelection again ->...
    private bool m_shouldIgnoreNextSelectionChange = false;
    // Flag indicating whether selection change should raise item invoked. This is needed to be able to raise ItemInvoked before SelectionChanged while SelectedItem should point to the clicked item
    private bool m_shouldRaiseItemInvokedAfterSelection = false;

    // There are three ways to change IsPaneOpen:
    // 1, customer call IsPaneOpen=true/false directly or nav.IsPaneOpen is binding with a variable and the value is changed.
    // 2, customer click ToggleButton or splitView.IsPaneOpen->nav.IsPaneOpen changed because of window resize
    // 3, customer changed PaneDisplayMode.
    // 2 and 3 are internal implementation and will call by ClosePane/OpenPane. the flag is to indicate 1 if it's false
    private bool m_isOpenPaneForInteraction = false;

    private bool m_shouldIgnoreUIASelectionRaiseAsExpandCollapseWillRaise = false;

    private bool m_orientationChangedPendingAnimation = false;

    private bool m_tabKeyPrecedesFocusChange = false;

    private GettingFocusHelper m_leftNavRepeaterGettingFocusHelper;

    private readonly BitmapCache m_bitmapCache;

    private static readonly PropertyPath s_opacityPath = new(OpacityProperty);
    private static readonly PropertyPath s_centerYPath = new("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.CenterY)");
    private static readonly PropertyPath s_scaleXPath = new("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)");
    private static readonly PropertyPath s_scaleYPath = new("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)");
    private static readonly PropertyPath s_translateYPath = new("(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform.Y)");
}
