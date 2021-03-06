﻿using System.Collections.Generic;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

using System;
using Windows.Storage;
using Newtonsoft.Json;
using Windows.UI;
using Windows.UI.Xaml.Media;
using System.Threading.Tasks;
using Windows.UI.ViewManagement;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace NTUTWin
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        private string name;

        private StatusBarProgressIndicator progressbar = StatusBar.GetForCurrentView().ProgressIndicator;

        private ApplicationDataContainer roamingSettings = ApplicationData.Current.RoamingSettings;
        private ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        public MainPage()
        {
            this.InitializeComponent();

            //For debugging

            //var sessionId = roamingSettings.Values["JSESSIONID"].ToString();
            //roamingSettings.Values.Clear();
            //roamingSettings.Values.Add("JSESSIONID", sessionId);
            //roamingSettings.Values.Remove("JSESSIONID");
        }

        private async Task GetSchedule(Semester semester)
        {
            var coursesRequest = await NPAPI.GetCourses(searchForIdTextBox.Text, semester.Year, semester.SemesterNumber);

            if (coursesRequest.Success)
            {
                //Fill scheduleGrid
                FillCoursesIntoGrid(coursesRequest.Data);

                //Update result label
                searchResultLabelTextBlock.Text = name + " " + semester;

                //Save to roaming settings
                var coursesJson = JsonConvert.SerializeObject(coursesRequest.Data);
                var semesterJson = JsonConvert.SerializeObject(semester);
                SaveToSettings(localSettings, "courses", coursesJson.ToString());
                SaveToSettings(localSettings, "semester", semesterJson.ToString());

                //Send GA Event
                bool searchSelf = roamingSettings.Values.ContainsKey("id") && roamingSettings.Values["id"] as string == searchForIdTextBox.Text;
                App.Current.GATracker.SendEvent("Get Curriculum", semester.ToString(), searchForIdTextBox.Text, searchSelf ? 0 : 1);
            }
            else
            {
                if (coursesRequest.Error == NPAPI.RequestResult.ErrorType.Unauthorized)
                {
                    //Send GA Event
                    App.Current.GATracker.SendEvent("Session", "Session Expired", null, 0);

                    //Try background login
                    var result = await NPAPI.BackgroundLogin();
                    if (result.Success)
                        await GetSchedule(semester);
                    else
                        Frame.Navigate(typeof(LoginPage));
                    
                }
                else
                    await new MessageDialog(coursesRequest.Message).ShowAsync();
            }
        }

        private void FillCoursesIntoGrid(List<Course> courses)
        {
            //Clear previous result
            scheduleGrid.Children.Clear();
            scheduleGrid.RowDefinitions.Clear();
            unscheduledCoursesGrid.Items.Clear();

            //Prepare scheduleGrid header columns
            var dayChars = "一二三四五六日";
            for(int d = 0; d < 7; d++)
            {
                TextBlock textBlock = new TextBlock();
                textBlock.Text = dayChars[d].ToString();
                textBlock.TextAlignment = TextAlignment.Center;
                Grid.SetRow(textBlock, 0);
                Grid.SetColumn(textBlock, d + 1);
                scheduleGrid.Children.Add(textBlock);
            }
            var timeChars = "123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            for (int t = 0; t < timeChars.Length; t++)
            {
                TextBlock textBlock = new TextBlock();
                textBlock.Text = timeChars[t].ToString();
                textBlock.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetRow(textBlock, t + 1);
                Grid.SetColumn(textBlock, 0);

                RowDefinition rowDefinition = new RowDefinition();
                rowDefinition.Height = GridLength.Auto;
                scheduleGrid.RowDefinitions.Add(rowDefinition);

                scheduleGrid.Children.Add(textBlock);
            }

            int maxDay = 0;
            int maxTime = 0;

            foreach (Course course in courses)
            {
                if (course.Schedule.Count != 0)
                {
                    foreach (int day in course.Schedule.Keys)
                    {
                        maxDay = Math.Max(maxDay, day);

                        var times = course.Schedule[day];
                        foreach (int time in times)
                        {
                            maxTime = Math.Max(maxTime, time);

                            var border = GetCourseElement(course, time, day);
                            
                            Grid.SetColumn(border, day + 1);
                            Grid.SetRow(border, time);
                            scheduleGrid.Children.Add(border);
                        }
                    }
                }
                else
                {
                    //show courses that dosne't have any schedule data
                    var border = GetCourseElement(course, -1);
                    unscheduledCoursesGrid.Items.Add(border);
                }
            }

            unscheduledCoursesTextBlock.Visibility = unscheduledCoursesGrid.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            //Determine should Saturday and Sunday should be shown
            saturdayColumnDefinition.Width = maxDay >= 5 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            sundayColumnDifinition.Width = maxDay == 6 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

            //Hide unnecessary rows
            for (int r = maxTime + 1; r < scheduleGrid.RowDefinitions.Count; r++)
                scheduleGrid.RowDefinitions[r].Height = new GridLength(0);
        }

        private FrameworkElement GetCourseElement(Course course, int time, int day = -1)
        {
            var textBlock = new TextBlock();

            textBlock.Text = course.Name;
            textBlock.TextAlignment = TextAlignment.Center;
            textBlock.TextWrapping = TextWrapping.Wrap;
            textBlock.FontSize = 16;
            textBlock.VerticalAlignment = VerticalAlignment.Center;

            Border border = new Border();
            border.Child = textBlock;
            border.Tapped += async (object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e) =>
            {
                //Send GA Event
                App.Current.GATracker.SendEvent("Other", "Tap on Course", null, 0);

                string content = "";

                if (course.ClassRooms.Count > 0)
                {
                    foreach (string classRoom in course.ClassRooms)
                        content += classRoom + " ";
                    content += "\n";
                }

                if (course.Teachers.Count > 0)
                {
                    foreach (string teacher in course.Teachers)
                        content += teacher + " ";
                    content += "\n";
                }

                string timeString = Course.GetTimeString(time);

                if (timeString != null)
                    content += timeString + "\n";

                if (!string.IsNullOrWhiteSpace(course.Note))
                    content += course.Note;

                await new MessageDialog(content, course.Name).ShowAsync();
            };

            Brush backColor;
            if(DateTime.Today.DayOfWeek == (DayOfWeek)(day + 1 % 7))
                backColor = Application.Current.Resources["PhoneAccentBrush"] as Brush;
            else
                backColor = new SolidColorBrush(Color.FromArgb(128, 128, 128, 128));
            
            border.Background = backColor;
            border.Padding = new Thickness(5);
            border.Margin = new Thickness(1);

            return border;
        }

        private async Task SearchForId(string id)
        {
            var semestersRequest = await NPAPI.GetSemesters(id);
            bool saveSearchId = true;
            if (semestersRequest.Success)
            {
                //Update label
                name = semestersRequest.Name;
                searchResultLabelTextBlock.Text = name;

                //Update comoboBox
                semesterComboBox.ItemsSource = semestersRequest.Semesters;
                if (semesterComboBox.Items.Count > 0)
                    semesterComboBox.SelectedIndex = 0;
                semesterComboBox.Visibility = semesterComboBox.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

                //Save request to roaming settings
                SaveToSettings(localSettings, "name", name);
                var semestersJson = JsonConvert.SerializeObject(semestersRequest.Semesters);
                SaveToSettings(localSettings, "semesters", semestersJson.ToString());

                //Send GA Event
                bool searchSelf = roamingSettings.Values.ContainsKey("id") && roamingSettings.Values["id"] as string == id;
                App.Current.GATracker.SendEvent("Get Semesters", null, id, searchSelf ? 0 : 1);
            }
            else
            {
                if (semestersRequest.Error == NPAPI.RequestResult.ErrorType.Unauthorized)
                {
                    //Send GA Event
                    App.Current.GATracker.SendEvent("Session", "Session Expired", null, 0);

                    //Try background login
                    var result = await NPAPI.BackgroundLogin();
                    if (result.Success)
                        await SearchForId(id);
                    else
                        Frame.Navigate(typeof(LoginPage));
                }
                else
                {
                    await new MessageDialog(semestersRequest.Message).ShowAsync();
                    saveSearchId = false;
                }
                    
            }
            
            if (saveSearchId)
                SaveToSettings(localSettings, "searchId", id);
        }

        private void RestoreCachedData()
        {
            //Cache all variables needed, restore them if no exception occured
            List<Course> courses = null;
            try
            {
                string searchId = null, name = null, semesterName = null;
                List<Semester> semesters = null;

                if (roamingSettings.Values.ContainsKey("searchId"))
                    searchId = roamingSettings.Values["searchId"].ToString();
                else if (roamingSettings.Values.ContainsKey("id"))
                    searchId = roamingSettings.Values["id"].ToString();

                if (localSettings.Values.ContainsKey("name"))
                    name = localSettings.Values["name"].ToString();

                if (localSettings.Values.ContainsKey("semester"))
                    semesterName = JsonConvert.DeserializeObject<Semester>(localSettings.Values["semester"] as string).ToString();

                if (localSettings.Values.ContainsKey("semesters"))
                    semesters = JsonConvert.DeserializeObject<List<Semester>>(localSettings.Values["semesters"].ToString());

                if (localSettings.Values.ContainsKey("courses"))
                    courses = JsonConvert.DeserializeObject<List<Course>>(localSettings.Values["courses"].ToString());
                
                //Restore data
                if (searchId != null)
                    searchForIdTextBox.Text = searchId;

                if (name != null)
                {
                    this.name = name;
                    searchResultLabelTextBlock.Text = name + (semesterName != null ? " " + semesterName : "");
                }
                else
                    throw new Exception("Name not cached");

                if (semesters != null)
                    semesterComboBox.ItemsSource = semesters;

                if (courses != null)
                    FillCoursesIntoGrid(courses);
            }
            catch (Exception e)
            {
                //Clear settings if parsing failed
                localSettings.Values.Remove("searchId");
                localSettings.Values.Remove("name");
                localSettings.Values.Remove("semester");
                localSettings.Values.Remove("semesters");
                localSettings.Values.Remove("courses");

                //Send GA Exception
                App.Current.GATracker.SendException(e.Message, false);
            }

            searchAppBarToggleButton.IsChecked = courses == null || courses.Count == 0;
        }

        private void SaveToSettings(ApplicationDataContainer settings, string key, object value)
        {
            if (!settings.Values.ContainsKey(key))
                settings.Values.Add(key, value);
            else
                settings.Values[key] = value;
        }

        private async void semesterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            scheduleGrid.Children.Clear();
            searchResultLabelTextBlock.Text = name;
            if (semesterComboBox.SelectedItem is Semester)
            {
                await progressbar.ShowAsync();
                await GetSchedule(semesterComboBox.SelectedItem as Semester);
                await progressbar.HideAsync();
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            //Send GA View
            App.Current.GATracker.SendView("CurriculumPage");

            RestoreCachedData();

            
        }

        private void searchAppBarToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            searchGrid.Visibility = Visibility.Visible;
        }

        private void searchAppBarToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            searchGrid.Visibility = Visibility.Collapsed;
        }

        private async void searchForIdTextBox_KeyUp(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                await progressbar.ShowAsync();
                searchForIdTextBox.IsReadOnly = true;
                await SearchForId(searchForIdTextBox.Text);
                await progressbar.HideAsync();
                searchForIdTextBox.IsReadOnly = false;
                semesterComboBox.Focus(FocusState.Programmatic);
            }
        }

        private void aboutAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(AboutPage));
        }

        private async void rateAndReviewAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store:reviewapp?appid=5c805945-21cb-4160-9a45-1de3ec408a9d"));

            //Send GA Event
            App.Current.GATracker.SendEvent("Other", "Go Rating Page", null, 0);
        }

        private async void logoutAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            var result = await NPAPI.LogoutNPortal();
            if(result.Success)
                Frame.Navigate(typeof(LoginPage));
            else
                await new MessageDialog(result.Message).ShowAsync();
        }
    }
}