using System.Windows;
using System.Windows.Controls;
using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.GUI.Controls.TypeLayoutTreeView;

public sealed class TypeLayoutDataTemplateSelector : DataTemplateSelector
{
    public static TypeLayoutDataTemplateSelector Instance { get; } = new TypeLayoutDataTemplateSelector();

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (container is FrameworkElement element && item != null)
        {
            if (item is TypeLayoutItemViewModel or TypeLayoutItemDiffViewModel)
            {
                if (item == TypeLayoutItemViewModel.PlaceholderLoadingItem ||
                    item == TypeLayoutItemDiffViewModel.PlaceholderLoadingItem)
                {
                    return element.FindResource("LoadingItemTemplate") as DataTemplate;
                }
                else
                {
                    return element.FindResource("ClassTemplate") as HierarchicalDataTemplate;
                }
            }
            else if (item is TypeLayoutItemViewModel.MemberViewModel memVM)
            {
                var underlyingType = GetUnderlyingType(memVM.Member.Type);

                // Basic types ("int", "const unsigned int" and so on) and functions don't have members that make sense to
                // dig into, so they're plain templates.  Everything else could have children so it gets the hierarchical
                // data template.
                if (underlyingType?.CanLoadLayout == true && memVM.ShouldGenerateHyperlinks)
                {
                    return element.FindResource("MemberWithLinkedTypeTemplate") as HierarchicalDataTemplate;
                }
                else
                {
                    return element.FindResource("MemberTemplate") as DataTemplate;
                }
            }
            else if (item is TypeLayoutItemDiffViewModel.MemberDiffViewModel memDiffVM)
            {
                var underlyingBeforeType = GetUnderlyingType(memDiffVM.Member.BeforeMember?.Type);
                var underlyingAfterType = GetUnderlyingType(memDiffVM.Member.AfterMember?.Type);

                // Basic types ("int", "const unsigned int" and so on) and functions don't have members that make sense to
                // dig into, so they're plain templates.  Everything else could have children so it gets the hierarchical
                // data template.
                if (underlyingBeforeType?.CanLoadLayout == true ||
                    underlyingAfterType?.CanLoadLayout == true)
                {
                    return element.FindResource("MemberWithLinkedTypeTemplate") as HierarchicalDataTemplate;
                }
                else
                {
                    return element.FindResource("MemberTemplate") as DataTemplate;
                }
            }
        }

        return null;
    }

    private static TypeSymbol? GetUnderlyingType(TypeSymbol? typeSymbol)
    {
        if (typeSymbol is PointerTypeSymbol ptrType)
        {
            return GetUnderlyingType(ptrType.PointerTargetType);
        }
        else if (typeSymbol is ModifiedTypeSymbol modType)
        {
            return GetUnderlyingType(modType.UnmodifiedTypeSymbol);
        }
        else if (typeSymbol is ArrayTypeSymbol arrType)
        {
            return GetUnderlyingType(arrType.ElementType);
        }
        else
        {
            return typeSymbol;
        }
    }
}
