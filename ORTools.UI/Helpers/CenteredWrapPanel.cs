using System;
using System.Windows;
using System.Windows.Controls;

namespace ORTools.UI.Helpers;

public class CenteredWrapPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        Size curLineSize = new Size();
        Size panelSize = new Size();

        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Size sz = child.DesiredSize;

            if (curLineSize.Width + sz.Width > availableSize.Width && curLineSize.Width > 0)
            {
                panelSize.Width = Math.Max(curLineSize.Width, panelSize.Width);
                panelSize.Height += curLineSize.Height;
                curLineSize = sz;
            }
            else
            {
                curLineSize.Width += sz.Width;
                curLineSize.Height = Math.Max(sz.Height, curLineSize.Height);
            }
        }

        panelSize.Width = Math.Max(curLineSize.Width, panelSize.Width);
        panelSize.Height += curLineSize.Height;

        return panelSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int firstInLine = 0;
        Size curLineSize = new Size();
        double accumulatedHeight = 0;

        for (int i = 0; i < InternalChildren.Count; i++)
        {
            UIElement child = InternalChildren[i];
            Size sz = child.DesiredSize;

            if (curLineSize.Width + sz.Width > finalSize.Width && curLineSize.Width > 0)
            {
                ArrangeLine(accumulatedHeight, curLineSize, firstInLine, i, finalSize.Width);
                accumulatedHeight += curLineSize.Height;
                curLineSize = sz;
                firstInLine = i;
            }
            else
            {
                curLineSize.Width += sz.Width;
                curLineSize.Height = Math.Max(sz.Height, curLineSize.Height);
            }
        }

        if (firstInLine < InternalChildren.Count)
        {
            ArrangeLine(accumulatedHeight, curLineSize, firstInLine, InternalChildren.Count, finalSize.Width);
        }

        return finalSize;
    }

    private void ArrangeLine(double y, Size lineSize, int start, int end, double finalWidth)
    {
        double x = Math.Max(0, (finalWidth - lineSize.Width) / 2);
        for (int i = start; i < end; i++)
        {
            UIElement child = InternalChildren[i];
            child.Arrange(new Rect(x, y, child.DesiredSize.Width, lineSize.Height));
            x += child.DesiredSize.Width;
        }
    }
}
