using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodeAtlasVSIX
{
    class ResourceSetter
    {
        FrameworkElement m_rootControl;

        public ResourceSetter(FrameworkElement rootControl)
        {
            m_rootControl = rootControl;
        }

        public void SetStyle()
        {
            WalkDownLogicalTree(m_rootControl);
        }

        void WalkDownLogicalTree(object current)
        {
            DependencyObject depObj = current as DependencyObject;
            if (depObj == null)
            {
                return;
            }

            bool res = false;
            res |= SetElementStyle(depObj as TextBox, VsResourceKeys.TextBoxStyleKey);
            res |= SetElementStyle(depObj as Label, VsResourceKeys.ThemedDialogLabelStyleKey);
            res |= SetElementStyle(depObj as Button, VsResourceKeys.ButtonStyleKey);
            //res |= SetElementStyle(depObj as Menu, VsResourceKeys.ThemedDialogDefaultStylesKey);
            //res |= SetElementStyle(depObj as MenuItem, VsResourceKeys.ThemedDialogDefaultStylesKey);
            res |= SetElementStyle(depObj as ListView, VsResourceKeys.ThemedDialogListViewStyleKey);
            res |= SetElementStyle(depObj as ListViewItem, VsResourceKeys.ThemedDialogListViewItemStyleKey);
            res |= SetElementStyle(depObj as ListBox, VsResourceKeys.ThemedDialogListBoxStyleKey);
            res |= SetElementStyle(depObj as RadioButton, VsResourceKeys.ThemedDialogRadioButtonStyleKey);
            //res |= SetElementStyle(depObj as TabItem, VsResourceKeys.ButtonStyleKey);

            foreach (object logicalChild in LogicalTreeHelper.GetChildren(depObj))
                WalkDownLogicalTree(logicalChild);
        }

        bool SetElementStyle(FrameworkElement element, object resourceKey)
        {
            if (element == null)
            {
                return false;
            }
            var style = m_rootControl.TryFindResource(resourceKey) as Style;
            if (style != null)
            {
                element.Style = style;
            }
            return true;
        }
    }
}
