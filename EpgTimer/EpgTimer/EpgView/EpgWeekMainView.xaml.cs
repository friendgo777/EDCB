﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace EpgTimer
{
    using EpgView;
    using ComboItem = KeyValuePair<UInt64, string>;

    /// <summary>
    /// EpgWeekMainView.xaml の相互作用ロジック
    /// </summary>
    public partial class EpgWeekMainView : EpgMainViewBase
    {
        protected override bool viewCustNeedTimeOnly { get { return viewInfo.NeedTimeOnlyWeek; } }
        private List<DateTime> dayList = new List<DateTime>();

        protected class EpgViewStateWeek : EpgViewState { public ulong? selectID = null; }
        public override EpgViewState GetViewState() { return new EpgViewStateWeek { viewMode = viewMode, selectID = GetSelectID() }; }
        protected EpgViewStateWeek RestoreState { get { return restoreState as EpgViewStateWeek ?? new EpgViewStateWeek(); } }

        public EpgWeekMainView()
        {
            InitializeComponent();
            SetControls(epgProgramView, timeView, weekDayView.scrollViewer, button_now);

            base.InitCommand();

            //コマンド集の初期化の続き、ボタンの設定
            mBinds.SetCommandToButton(button_go_Main, EpgCmds.ViewChgMode, 0);
        }

        //週間番組表での時刻表現用のメソッド。
        protected override DateTime GetViewTime(DateTime time)
        {
            return new DateTime(2001, 1, time.Hour >= viewInfo.StartTimeWeek ? 1 : 2).Add(time.TimeOfDay);
        }
        private DateTime GetViewDay(DateTime time)
        {
            return time.AddHours(-viewInfo.StartTimeWeek).Date;
        }

        /// <summary>予約情報の再描画</summary>
        protected override void ReloadReserveViewItem()
        {
            try
            {
                reserveList.Clear();

                UInt64 selectID = GetSelectID(true);
                foreach (ReserveData info in CommonManager.Instance.DB.ReserveList.Values)
                {
                    if (selectID == info.Create64Key())
                    {
                        ProgramViewItem dummy = null;
                        ReserveViewItem resItem = AddReserveViewItem(info, ref dummy);
                        if (resItem != null)
                        {
                            //横位置の設定
                            resItem.Width = Settings.Instance.ServiceWidth;
                            resItem.LeftPos = resItem.Width * dayList.BinarySearch(GetViewDay(info.StartTime));

                            //範囲外は削除する。日を追加するのは簡単だが、viewCustNeedTimeOnly==trueで時間の方を追加するのが面倒すぎる。
                            if (resItem.LeftPos < 0) reserveList.Remove(resItem);
                        }
                    }
                }

                epgProgramView.SetReserveList(reserveList);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace); }
        }

        /// <summary>番組情報の再描画</summary>
        protected override void ReloadProgramViewItem()
        {
            serviceChanging = true;
            try
            {
                //表示していたサービスがあれば維持
                comboBox_service.ItemsSource = serviceEventListOrderAdjust.Select(item => new ComboItem(item.serviceInfo.Create64Key(), item.serviceInfo.service_name));
                comboBox_service.SelectedValue = RestoreState.selectID ?? GetSelectID();
                if (comboBox_service.SelectedIndex < 0) comboBox_service.SelectedIndex = 0;

                UpdateProgramView();
                MoveNowTime();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace); }
            serviceChanging = false;
        }
        private void UpdateProgramView()
        {
            try
            {
                timeView.ClearInfo();
                weekDayView.ClearInfo();
                epgProgramView.ClearInfo();
                timeList.Clear();
                programList.Clear();
                NowLineDelete();
                dayList.Clear();

                UInt64 selectID = GetSelectID(true);
                if (selectID == 0) return;

                //リストの作成
                int idx = serviceEventList.FindIndex(item => item.serviceInfo.Create64Key() == selectID);
                if (idx < 0) return;

                serviceEventList[idx].eventList.ForEach(eventInfo =>
                {
                    //無いはずだが、ToDictionary()にせず、一応保険。
                    programList[eventInfo.CurrentPgUID()] = new ProgramViewItem(eventInfo);
                });

                //日付リスト構築
                dayList.AddRange(programList.Values.Select(d => GetViewDay(d.Data.start_time)).Distinct().OrderBy(day => day));

                //横位置の設定
                foreach (ProgramViewItem item in programList.Values)
                {
                    item.Width = Settings.Instance.ServiceWidth;
                    item.LeftPos = item.Width * dayList.BinarySearch(GetViewDay(item.Data.start_time));
                }

                //縦位置の設定
                if (viewCustNeedTimeOnly == false)
                {
                    ViewUtil.AddTimeList(timeList, new DateTime(2001, 1, 1, viewInfo.StartTimeWeek, 0, 0), 86400);
                }
                SetProgramViewItemVertical();

                epgProgramView.SetProgramList(programList.Values.ToList(),
                    dayList.Count * Settings.Instance.ServiceWidth,
                    timeList.Count * 60 * Settings.Instance.MinHeight);

                timeView.SetTime(timeList, true);
                weekDayView.SetDay(dayList);

                ReDrawNowLine();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace); }
        }

        bool serviceChanging = false;
        private void comboBox_service_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (serviceChanging == true) return;
            UpdateProgramView();
            ReloadReserveViewItem();
            UpdateStatus();
        }
        private UInt64 GetSelectID(bool alternativeSelect = false)
        {
            var idx = comboBox_service.SelectedIndex;
            if (idx < 0)
            {
                if (alternativeSelect == false || comboBox_service.Items.Count == 0) return 0;
                idx = 0;
            }
            return ((ComboItem)comboBox_service.Items[idx]).Key;
        }

        public override int MoveToReserveItem(ReserveData target, JumpItemStyle style = JumpItemStyle.MoveTo, bool dryrun = false)
        {
            //実際には切り替えないと分からない
            if (dryrun == true) return target == null ? -1 : viewInfo.ViewServiceList.IndexOf(target.Create64Key());
            if (target != null) ChangeViewService(target.Create64Key());
            return base.MoveToReserveItem(target, style);
        }
        public override int MoveToProgramItem(EpgEventInfo target, JumpItemStyle style = JumpItemStyle.MoveTo, bool dryrun = false)
        {
            if (dryrun == true) return target == null ? -1 : viewInfo.ViewServiceList.IndexOf(target.Create64Key());
            if (target != null) ChangeViewService(target.Create64Key());
            return base.MoveToProgramItem(target, style);
        }
        protected void ChangeViewService(UInt64 id)
        {
            var target = comboBox_service.Items.OfType<ComboItem>().FirstOrDefault(item => item.Key == id);
            if (target.Key != default(UInt64)) comboBox_service.SelectedItem = target;
        }
    }
}
