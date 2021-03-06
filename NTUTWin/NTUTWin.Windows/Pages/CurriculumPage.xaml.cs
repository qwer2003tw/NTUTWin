﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace NTUTWin
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class CurriculumPage : Page
    {
        private string name;

        //private StatusBarProgressIndicator progressbar = StatusBar.GetForCurrentView().ProgressIndicator;

        private ApplicationDataContainer roamingSettings = ApplicationData.Current.RoamingSettings;
        private ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        public CurriculumPage()
        {
            this.InitializeComponent();

            //For debugging

            //var sessionId = roamingSettings.Values["JSESSIONID"].ToString();
            //roamingSettings.Values.Clear();
            //roamingSettings.Values.Add("JSESSIONID", sessionId);
            //roamingSettings.Values.Remove("JSESSIONID");
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            //Send GA View
            App.Current.GATracker.SendView("CurriculumPage");
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter != null)
            {
                //Search by navigation parameter
                var searchId = e.Parameter as string;
                searchForIdTextBox.Text = searchId;
                await SearchForId(searchId);
            }
            else
            {
                //Restore cached data
                //Cache all variables needed, restore them if no exception occured
                try
                {
                    string searchId = null, name = null, semesterName = null;
                    List<Semester> semesters = null;
                    List<Course> courses = null;

                    if (localSettings.Values.ContainsKey("searchId"))
                        searchId = localSettings.Values["searchId"].ToString();
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
                catch (Exception exception)
                {
                    //Clear settings if parsing failed
                    localSettings.Values.Remove("searchId");
                    localSettings.Values.Remove("name");
                    localSettings.Values.Remove("semester");
                    localSettings.Values.Remove("semesters");
                    localSettings.Values.Remove("courses");

                    //Send GA Exception
                    App.Current.GATracker.SendException(exception.Message, false);
                }
            }
        }

        private async Task GetSchedule(Semester semester)
        {
            try
            {
                var courses = await NPAPI.GetCourses(searchForIdTextBox.Text, semester.Year, semester.SemesterNumber);
                //Fill scheduleGrid
                FillCoursesIntoGrid(courses);

                //Update result label
                searchResultLabelTextBlock.Text = name + " " + semester;

                //Show searchAppBarToggleButton
                //searchAppBarToggleButton.Visibility = Visibility.Visible;

                //Save to roaming settings
                var coursesJson = JsonConvert.SerializeObject(courses);
                var semesterJson = JsonConvert.SerializeObject(semester);
                SaveToSettings(localSettings, "courses", coursesJson.ToString());
                SaveToSettings(localSettings, "semester", semesterJson.ToString());

                //Send GA Event
                bool searchSelf = roamingSettings.Values.ContainsKey("id") && roamingSettings.Values["id"] as string == searchForIdTextBox.Text;
                App.Current.GATracker.SendEvent("Get Curriculum", semester.ToString(), searchForIdTextBox.Text, searchSelf ? 0 : 1);
            }
            catch (NPAPI.SessionExpiredException)
            {
                //Send GA Event
                App.Current.GATracker.SendEvent("Session", "Session Expired", null, 0);

                //Try background login
                try
                {
                    await NPAPI.BackgroundLogin();
                    await GetSchedule(semester);
                }
                catch
                {
                    Frame.Navigate(typeof(LoginPage));
                }
            }
            catch (Exception e)
            {
                await new MessageDialog(e.Message, "錯誤").ShowAsync();
            }
        }

        private void FillCoursesIntoGrid(List<Course> courses)
        {
            //Clear previous result
            scheduleGrid.Children.Clear();
            scheduleGrid.RowDefinitions.Clear();
            unscheduledCoursesGrid.Items.Clear();

            //Summary
            float hours = 0;
            float credits = 0;
            foreach(var course in courses)
            {
                hours += course.Hours;
                credits += course.Credit;
            }
            summaryTextBlock.Text = string.Format("學分: {0} 時數: {1}", credits, hours);

            //Prepare scheduleGrid header columns
            var dayChars = "一二三四五六日";
            for (int d = 0; d < 7; d++)
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
            //for (int i = 0, count = course.ClassRooms.Count; i < count; i++)
                //textBlock.Text += (i == 0 ? "\n" : "") + course.ClassRooms[i] + (i < count - 1 ? "、" : "");

            textBlock.TextAlignment = TextAlignment.Center;
            textBlock.TextWrapping = TextWrapping.Wrap;
            textBlock.FontSize = 16;
            textBlock.VerticalAlignment = VerticalAlignment.Center;

            Border border = new Border();
            border.MinHeight = 50;
            border.Child = textBlock;
            border.Tapped += async (object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e) =>
            {

                string content = "";

                if (course.ClassRooms.Count > 0)
                {
                    foreach (string classRoom in course.ClassRooms)
                        content += classRoom + " ";
                    content += "\n";
                }

                string timeString = Course.GetTimeString(time);

                if (timeString != null)
                    content += timeString + "\n";

                content += string.Format("學分: {0} 時數: {1}\n", course.Credit, course.Hours);

                if (course.Teachers.Count > 0)
                {
                    foreach (string teacher in course.Teachers)
                        content += teacher + " ";
                    content += "\n";
                }

                if (!string.IsNullOrWhiteSpace(course.Note))
                    content += course.Note;

                var dialog = new MessageDialog(content, course.Name);
                if(!(string.IsNullOrWhiteSpace(course.IdForSelect) || course.IdForSelect == "0"))
                    dialog.Commands.Add(new UICommand("詳細資料", (command) =>
                    {
                        Frame.Navigate(typeof(CourseDetailPage), course.IdForSelect);
                    }));
                dialog.Commands.Add(new UICommand("關閉"));

                //Send GA Event
                App.Current.GATracker.SendEvent("Other", "Tap on Course", null, 0);

                await dialog.ShowAsync();
            };

            Brush backColor, hoverColor;
            if (DateTime.Today.DayOfWeek == (DayOfWeek)(day + 1 % 7))
            {
                backColor = new SolidColorBrush(Color.FromArgb(255, 209, 52, 56));
                hoverColor = new SolidColorBrush(Color.FromArgb(255, 180, 52, 56));
            }
            else
            {
                backColor = new SolidColorBrush(Color.FromArgb(128, 128, 128, 128));
                hoverColor = new SolidColorBrush(Color.FromArgb(255, 128, 128, 128));
            }

            border.Background = backColor;
            border.Padding = new Thickness(5);
            border.Margin = new Thickness(1);

            border.PointerEntered += (sender, e) =>
            {
                border.Background = hoverColor;
            };

            border.PointerExited += (sender, e) =>
            {
                border.Background = backColor;
            };

            return border;
        }

        private async Task SearchForId(string id)
        {
            //Disable user input
            searchForIdTextBox.IsEnabled = searchSelfButton.IsEnabled = semesterComboBox.IsEnabled = getSemestersButton.IsEnabled = false;

            try
            {
                var semestersRequest = await NPAPI.GetSemesters(id);

                //Update label
                name = semestersRequest.Name;
                searchResultLabelTextBlock.Text = name;

                //Update comoboBox
                semesterComboBox.ItemsSource = semestersRequest.Semesters;
                if (semesterComboBox.Items.Count > 0)
                    semesterComboBox.SelectedIndex = 0;

                //Save request to local settings
                SaveToSettings(localSettings, "name", name);
                var semestersJson = JsonConvert.SerializeObject(semestersRequest.Semesters);
                SaveToSettings(localSettings, "semesters", semestersJson.ToString());

                //Send GA Event
                bool searchSelf = roamingSettings.Values.ContainsKey("id") && roamingSettings.Values["id"] as string == id;
                App.Current.GATracker.SendEvent("Get Semesters", null, id, searchSelf ? 0 : 1);

                //Save search id
                SaveToSettings(localSettings, "searchId", id);
            }
            catch (NPAPI.SessionExpiredException)
            {
                //Send GA Event
                App.Current.GATracker.SendEvent("Session", "Session Expired", null, 0);

                //Try background login
                try
                {
                    await NPAPI.BackgroundLogin();
                    await SearchForId(id);
                }
                catch
                {
                    Frame.Navigate(typeof(LoginPage));
                }
            }
            catch (Exception e)
            {
                await new MessageDialog(e.Message, "錯誤").ShowAsync();
            }

            //Enableuser input
            searchForIdTextBox.IsEnabled = searchSelfButton.IsEnabled = semesterComboBox.IsEnabled = getSemestersButton.IsEnabled = true;

            semesterComboBox.Focus(FocusState.Programmatic);
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
                //Disable user input
                searchForIdTextBox.IsEnabled = searchSelfButton.IsEnabled = semesterComboBox.IsEnabled = getSemestersButton.IsEnabled = false;
                await GetSchedule(semesterComboBox.SelectedItem as Semester);
                //Enable user input
                searchForIdTextBox.IsEnabled = searchSelfButton.IsEnabled = semesterComboBox.IsEnabled = getSemestersButton.IsEnabled = true;
            }
        }

        private async void searchForIdTextBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                await SearchForId(searchForIdTextBox.Text);
            }
        }

        private async void getSemestersButton_Click(object sender, RoutedEventArgs e)
        {
            await SearchForId(searchForIdTextBox.Text);
        }

        private void schoolEventScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SchedulePage));
        }

        private async void searchSelfButton_Click(object sender, RoutedEventArgs e)
        {
            if (!roamingSettings.Values.ContainsKey("id"))
                return;

            searchForIdTextBox.Text = roamingSettings.Values["id"].ToString();
            await SearchForId(roamingSettings.Values["id"].ToString());
        }
    }
}
