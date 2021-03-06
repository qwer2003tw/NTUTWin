﻿using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// 空白頁項目範本已記錄在 http://go.microsoft.com/fwlink/?LinkId=234238

namespace NTUTWin
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class MidAlertPage : Page
    {
        public MidAlertPage()
        {
            this.InitializeComponent();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            //Send GA View
            App.Current.GATracker.SendView("MidAlertPage");

            courseDetailButton.Visibility = Visibility.Collapsed;
            await GetMidAlert();

        }

        private async Task GetMidAlert()
        {
            courseNameTextBlock.Text = "";
            try
            {
                var midAlerts = await NPAPI.GetMidAlerts();

                courseNameTextBlock.Text = "(請選擇)";
                titleTextBlock.Text = midAlerts.Semester + " 期中預警";
                listView.ItemsSource = midAlerts.Alerts;

                //Send GA Event
                string id = ApplicationData.Current.RoamingSettings.Values.ContainsKey("id") ? ApplicationData.Current.RoamingSettings.Values["id"] as string : "N/A";
                App.Current.GATracker.SendEvent("Mid Alert", "Get Mid Alert", id, 0);
            }
            catch (NPAPI.SessionExpiredException)
            {
                //Send GA Event
                App.Current.GATracker.SendEvent("Session", "Session Expired", null, 0);

                //Try background login
                try
                {
                    await NPAPI.BackgroundLogin();
                    await GetMidAlert();
                }
                catch
                {
                    Frame.Navigate(typeof(LoginPage));
                }
            }
            catch (Exception e)
            {
                listView.Items.Clear();
                listView.Items.Add("讀取失敗，請稍後再試。");
                listView.Items.Add(e.Message);
            }
        }

        private async void listView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is MidAlerts.MidAlert)
            {
                var alertItem = e.ClickedItem as MidAlerts.MidAlert;
                var message = string.Format("預警:{3}\n{0} {1} {2}學分\n{4}",
                    alertItem.CourseNumber,
                    alertItem.Type,
                    alertItem.Credit,
                    alertItem.AlertSubmitted ? ((alertItem.Alerted ? "是" : "否") + " (" + alertItem.Ratio.Alerted + "/" + alertItem.Ratio.All  +")") : "尚未送出",
                    alertItem.Note);
                await new MessageDialog(message, alertItem.CourseName).ShowAsync();
            }
        }

        private void listView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            courseDetailButton.Visibility = listView.SelectedItem is MidAlerts.MidAlert ? Visibility.Visible : Visibility.Collapsed;
            if (!(listView.SelectedItem is MidAlerts.MidAlert))
                return;
            var alertItem = listView.SelectedItem as MidAlerts.MidAlert;
            courseNameTextBlock.Text = alertItem.CourseName;
            var message = string.Format("預警:\t{3}\n課號:\t{0}\n類型:\t{1}\n學分:\t{2}\n\n{4}",
                alertItem.CourseNumber,
                alertItem.Type,
                alertItem.Credit,
                alertItem.AlertSubmitted ? ((alertItem.Alerted ? "是" : "否") + " (" + alertItem.Ratio.Alerted + "/" + alertItem.Ratio.All + ")") : "尚未送出",
                alertItem.Note);
            detailTextBlock.Text = message;
        }

        private void courseDetailButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(listView.SelectedItem is MidAlerts.MidAlert))
                return;
            var alertItem = listView.SelectedItem as MidAlerts.MidAlert;
            Frame.Navigate(typeof(CourseDetailPage), alertItem.CourseNumber);
        }
    }
}
