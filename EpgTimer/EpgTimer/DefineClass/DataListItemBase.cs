﻿using System;
using System.Windows.Media;
using System.Windows.Controls;

namespace EpgTimer
{
    public class DataListItemBase : GridViewSorterItem
    {
        public virtual object DataObj { get { return null; } }
        public virtual TextBlock ToolTipView
        {
            get { return Settings.Instance.NoToolTip == true ? null : ToolTipViewAlways; }
        }
        public virtual TextBlock ToolTipViewAlways
        {
            get { return ViewUtil.GetTooltipBlockStandard(ConvertInfoText()); }
        }
        public virtual String ConvertInfoText(object param = null) { return ""; }

        public virtual int NowJumpingTable { set; get; }
        public virtual Brush ForeColor
        {
            //番組表へジャンプ時の強調表示
            get { return NowJumpingTable == 1 ? Brushes.Red : CommonManager.Instance.ListDefForeColor; }
        }
        public virtual Brush BackColor
        {
            get { return NowJumpingTable == 2 ? Brushes.Red : CommonManager.Instance.ResBackColor[0]; }
        }
        public virtual Brush BorderBrush { get { return null; } }
    }
}
