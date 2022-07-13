using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SizeBench.GUI.Controls;

/// <summary>
/// Extended TabControl which saves the displayed item so you don't get the performance hit of
/// unloading and reloading the VisualTree when switching tabs
/// </summary>
/// <remarks>
/// Based on example from http://stackoverflow.com/a/9802346, which in turn is based on
/// http://www.pluralsight-training.net/community/blogs/eburke/archive/2009/04/30/keeping-the-wpf-tab-control-from-destroying-its-children.aspx
/// with some modifications so it reuses a TabItem's ContentPresenter when doing drag/drop operations
/// </remarks>
[ExcludeFromCodeCoverage] // Testing UI types is pretty challenging in the existing infra
[TemplatePart(Name = "PART_ItemsHolder", Type = typeof(Panel))]
public class NonReloadingTabControl : TabControl
{
    private Panel? itemsHolderPanel;

    public NonReloadingTabControl()
    {
        this.DefaultStyleKey = typeof(NonReloadingTabControl);
        // This is necessary so that we get the initial databound selected item
        this.ItemContainerGenerator.StatusChanged += ItemContainerGeneratorStatusChanged;
    }

    /// <summary>
    /// If containers are done, generate the selected item
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ItemContainerGeneratorStatusChanged(object? sender, EventArgs e)
    {
        if (this.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
        {
            this.ItemContainerGenerator.StatusChanged -= ItemContainerGeneratorStatusChanged;
            UpdateSelectedItem();
        }
    }

    /// <summary>
    /// Get the ItemsHolder and generate any children
    /// </summary>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        this.itemsHolderPanel = GetTemplateChild("PART_ItemsHolder") as Panel;
        UpdateSelectedItem();
    }

    /// <summary>
    /// When the items change we remove any generated panel children and add any new ones as necessary
    /// </summary>
    /// <param name="e">The <see cref="NotifyCollectionChangedEventArgs"/> instance containing the event data.</param>
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnItemsChanged(e);

        if (this.itemsHolderPanel is null)
        {
            return;
        }

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Reset:
                this.itemsHolderPanel.Children.Clear();
                break;

            case NotifyCollectionChangedAction.Add:
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                {
                    foreach (var item in e.OldItems)
                    {
                        var cp = FindChildContentPresenter(item);
                        if (cp != null)
                        {
                            this.itemsHolderPanel.Children.Remove(cp);
                        }
                    }
                }

                // Don't do anything with new items because we don't want to
                // create visuals that aren't being shown

                UpdateSelectedItem();
                break;

            case NotifyCollectionChangedAction.Replace:
                throw new NotImplementedException("Replace not implemented yet");
        }
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        UpdateSelectedItem();
    }

    private void UpdateSelectedItem()
    {
        if (this.itemsHolderPanel is null)
        {
            return;
        }

        // Generate a ContentPresenter if necessary
        var item = GetSelectedTabItem();
        if (item != null)
        {
            CreateChildContentPresenter(item);
        }

        // show the right child
        foreach (ContentPresenter? child in this.itemsHolderPanel.Children)
        {
            child!.Visibility = (((TabItem)child.Tag).IsSelected) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private ContentPresenter? CreateChildContentPresenter(object? item)
    {
        if (item is null || this.itemsHolderPanel is null)
        {
            return null;
        }

        var cp = FindChildContentPresenter(item);

        if (cp != null)
        {
            return cp;
        }

        var tabItem = item as TabItem;
        cp = new ContentPresenter
        {
            Content = (tabItem != null) ? tabItem.Content : item,
            ContentTemplate = this.ContentTemplate,
            ContentTemplateSelector = this.ContentTemplateSelector,
            ContentStringFormat = this.ContentStringFormat,
            Visibility = Visibility.Collapsed,
            Tag = tabItem ?? (this.ItemContainerGenerator.ContainerFromItem(item))
        };
        this.itemsHolderPanel.Children.Add(cp);
        return cp;
    }

    private ContentPresenter? FindChildContentPresenter(object? data)
    {
        if (data is TabItem tabItem)
        {
            data = tabItem.Content;
        }

        if (data is null)
        {
            return null;
        }

        if (this.itemsHolderPanel is null)
        {
            return null;
        }

        foreach (ContentPresenter? cp in this.itemsHolderPanel.Children)
        {
            if (cp!.Content == data)
            {
                return cp;
            }
        }

        return null;
    }

    protected TabItem? GetSelectedTabItem()
    {
        var selectedItem = this.SelectedItem;
        if (selectedItem is null)
        {
            return null;
        }

#pragma warning disable CA1508 // Code Analysis seems to think that selectedItem is always a TabItem, but that's not true - disabling this warning here.
        var item = selectedItem as TabItem ?? this.ItemContainerGenerator.ContainerFromIndex(this.SelectedIndex) as TabItem;
#pragma warning restore CA1508

        return item;
    }
}
