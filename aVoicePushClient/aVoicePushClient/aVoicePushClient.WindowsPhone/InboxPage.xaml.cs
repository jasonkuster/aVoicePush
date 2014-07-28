using aVoicePushClient.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using HtmlAgilityPack;

// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkID=390556

namespace aVoicePushClient
{

    public class MessageThread
    {
        public string Participant { get; set; }
        public List<Message> Messages { get; set; }
        public string FirstMessage
        {
            get
            {
                return Messages.First().Content;
            }
        }

        public MessageThread()
        {
            Messages = new List<Message>();
        }
    }

    public class Message
    {
        public string Content { get; set; }
        public string Sender { get; set; }
        public string Time { get; set; }
    }

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class InboxPage : Page
    {
        /// <summary>
        /// https://www.google.com/voice/m
        /// </summary>
        private static readonly string HOME_URL = "https://www.google.com/voice/m";

        /// <summary>
        /// http://cryclops.com/apps/avoice/#push
        /// </summary>
        private static readonly string TUTORIAL_URL = "http://cryclops.com/apps/avoice/#push";

        private NavigationHelper navigationHelper;

        /// <summary>
        /// Gets the <see cref="NavigationHelper"/> associated with this <see cref="Page"/>.
        /// </summary>
        public NavigationHelper NavigationHelper
        {
            get { return this.navigationHelper; }
        }

        public InboxPage()
        {
            this.InitializeComponent();

            this.navigationHelper = new NavigationHelper(this);
            this.navigationHelper.LoadState += this.NavigationHelper_LoadState;
            this.navigationHelper.SaveState += this.NavigationHelper_SaveState;
            this.navigationHelper.BackPressed += this.NavigationHelper_BackPressed;

            CoreWindow.GetForCurrentThread().VisibilityChanged += CoreWindow_VisibilityChanged;
        }

        /// <summary>
        /// Populates the page with content passed during navigation.  Any saved state is also
        /// provided when recreating a page from a prior session.
        /// </summary>
        /// <param name="sender">
        /// The source of the event; typically <see cref="NavigationHelper"/>
        /// </param>
        /// <param name="e">Event data that provides both the navigation parameter passed to
        /// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested and
        /// a dictionary of state preserved by this page during an earlier
        /// session.  The state will be null the first time a page is visited.</param>
        private void NavigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
            // Pop the loading page off. Back quits (or goes back in the browser).
            Frame.BackStack.Clear();

            string navigationParameter = e.NavigationParameter as string;

            if (navigationParameter != null && navigationParameter == "tutorial")
            {
                // Show the tutorial
                GvWebView.Navigate(new Uri(TUTORIAL_URL));
            }
            else
            {
                // Just show the inbox
                string url = HOME_URL;

                if (e.PageState != null && e.PageState.ContainsKey("Url"))
                {
                    string savedUrl = e.PageState["Url"] as string;

                    if (savedUrl != null)
                        url = savedUrl;
                }

                GvWebView.Navigate(new Uri(url));
            }
        }

        /// <summary>
        /// Preserves state associated with this page in case the application is suspended or the
        /// page is discarded from the navigation cache.  Values must conform to the serialization
        /// requirements of <see cref="SuspensionManager.SessionState"/>.
        /// </summary>
        /// <param name="sender">The source of the event; typically <see cref="NavigationHelper"/></param>
        /// <param name="e">Event data that provides an empty dictionary to be populated with
        /// serializable state.</param>
        private void NavigationHelper_SaveState(object sender, SaveStateEventArgs e)
        {
            e.PageState["Url"] = GvWebView.Source.ToString();
        }

        private bool NavigationHelper_BackPressed(object sender, Windows.Phone.UI.Input.BackPressedEventArgs e)
        {
            if (GvWebView.CanGoBack)
            {
                GvWebView.GoBack();
                return false;
            }

            return true;
        }

        private void CoreWindow_VisibilityChanged(CoreWindow sender, VisibilityChangedEventArgs args)
        {
            if (args.Visible)
            {
                ClearTileToastAndBadgeNotifications();
            }
        }

        private void ClearTileToastAndBadgeNotifications()
        {
            TileUpdateManager.CreateTileUpdaterForApplication().Clear();

#if WINDOWS_PHONE_APP
            ToastNotificationManager.History.Clear();
#endif

            BadgeUpdateManager.CreateBadgeUpdaterForApplication().Clear();
        }

        #region NavigationHelper registration

        /// <summary>
        /// The methods provided in this section are simply used to allow
        /// NavigationHelper to respond to the page's navigation methods.
        /// <para>
        /// Page specific logic should be placed in event handlers for the  
        /// <see cref="NavigationHelper.LoadState"/>
        /// and <see cref="NavigationHelper.SaveState"/>.
        /// The navigation parameter is available in the LoadState method 
        /// in addition to page state preserved during an earlier session.
        /// </para>
        /// </summary>
        /// <param name="e">Provides data for navigation methods and event
        /// handlers that cannot cancel the navigation request.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedFrom(e);
        }

        #endregion

        private async void AppBarSignOutButton_Click(object sender, RoutedEventArgs e)
        {
            PageProgress.Visibility = Visibility.Visible;

            await App.DeleteNotificationAsync();

            MessageDialog dialog = new MessageDialog("If this is really goodbye, don't forget to disable your filters in Gmail.");
            await dialog.ShowAsync();

            Frame.Navigate(typeof(SignInPage), "signout");
        }

        private async void AppBarRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (await VerifyCancelMessageAsync())
            {
                GvWebView.Refresh();
                ClearTileToastAndBadgeNotifications();
            }
        }

        private async void AppBarHomeButton_Click(object sender, RoutedEventArgs e)
        {
            if (await VerifyCancelMessageAsync())
            {
                GvWebView.Navigate(new Uri(HOME_URL));
            }
        }

        private void AppBarTutorial_Click(object sender, RoutedEventArgs e)
        {
            GvWebView.Navigate(new Uri(TUTORIAL_URL));
        }

        private void GvWebView_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            PageProgress.Visibility = Visibility.Visible;
        }

        private async void GvWebView_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            PageProgress.Visibility = Visibility.Collapsed;

            List<MessageThread> threads = new List<MessageThread>();

            if (!args.IsSuccess)
            {
                MessageDialog dialog = new MessageDialog("There's a problem with the webpage. Try refreshing. " + args.WebErrorStatus.ToString());
                await dialog.ShowAsync();
            }
            else if (sender.Source.ToString().Contains(HOME_URL))
            {
                string html = await sender.InvokeScriptAsync("eval", new string[] { "document.documentElement.outerHTML;" });
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);
                var divs = doc.DocumentNode.Descendants("div")
                    .Where(d => d.Attributes.Contains("class") && d.Attributes["class"].Value.Contains("mr"));
                foreach(var message in divs) {
                    var recip_node = message.Descendants("a")
                        .Where(a => a.Attributes.Contains("href") && a.Attributes["href"].Value.Contains("/voice/m/contact/"))
                        .FirstOrDefault();
                    if (recip_node != null)
                    {
                        string recip = recip_node.InnerText;
                        var msg_nodes = message.Descendants("div")
                            .Where(d => d.Attributes.Contains("class") && d.Attributes["class"].Value.Equals("ms3"))
                            .FirstOrDefault()
                            .Descendants("div")
                            .Where(d => !d.Attributes.Contains("class") || !d.Attributes["class"].Value.Equals("ms2"));
                        List<Message> convo_messages = new List<Message>();
                        foreach (var message_contents in msg_nodes)
                        {
                            string message_sender = message_contents.Descendants("span")
                                .Where(s => s.GetAttributeValue("class", "").Equals("sf"))
                                .FirstOrDefault()
                                .Descendants("b")
                                .FirstOrDefault()
                                .InnerText
                                .Trim()
                                .Replace(":", "");
                            string message_content = message_contents.Descendants("span")
                                .Where(s => !s.Attributes.Contains("class"))
                                .FirstOrDefault()
                                .InnerText
                                .Trim();
                            string message_time = message_contents.Descendants("span")
                                .Where(s => s.GetAttributeValue("class", "").Equals("ms"))
                                .FirstOrDefault()
                                .InnerText
                                .Trim();
                            convo_messages.Add(new Message { Sender = message_sender, Content = message_content, Time = message_time });
                        }
                        threads.Add(new MessageThread() { Messages = convo_messages, Participant = recip });
                    }
                }
                MessagesListBox.ItemsSource = threads;
            }
        }

        /// <summary>
        /// Check with the user to ensure they meant to leave the SMS writing page.
        /// </summary>
        /// <returns>True if they wish to leave</returns>
        private async Task<bool> VerifyCancelMessageAsync()
        {
            if (GvWebView.Source.ToString().Contains(HOME_URL + "/sms"))
            {
                // Alert the user that they'll lose their work
                MessageDialog d = new MessageDialog("Leaving this page will cause anything you typed to be lost.", "Discard message?");
                d.Commands.Add(new UICommand("cancel"));
                d.Commands.Add(new UICommand("discard"));

                var cmd = await d.ShowAsync();

                return cmd.Label == "discard";
            }

            return true;
        }
    }
}
