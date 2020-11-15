using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SeriMongoDesktopClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        HubConnection connection;

        public MainWindow()
        {
            InitializeComponent();

        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            connection = new HubConnectionBuilder()
                .WithUrl(new Uri(seriMongoUrl.Text))
                // .WithAutomaticReconnect()
                .Build();

            connection.Closed += async (error) =>
            {
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await connection.StartAsync();
            };

            connection.Reconnecting += error =>
            {
                System.Diagnostics.Debug.Assert(connection.State == HubConnectionState.Reconnecting);

                // Notify users the connection was lost and the client is reconnecting.
                // Start queuing or dropping messages.

                return Task.CompletedTask;
            };

            connection.Reconnected += connectionId =>
            {
                System.Diagnostics.Debug.Assert(connection.State == HubConnectionState.Connected);

                // Notify users the connection was reestablished.
                // Start dequeuing messages queued while reconnecting if any.

                return Task.CompletedTask;
            };

            // Bind log entries to grid using a view-source to allow styling based on lon entry values
            ObservableCollection<LogEntry> logEntries = new ObservableCollection<LogEntry>();
            CollectionViewSource logEntriesViewSource;
            logEntriesViewSource = (CollectionViewSource)(FindResource("logEntriesViewSource"));
            logEntriesViewSource.Source = logEntries;

            connection.On<LogEntry>("OnReceiveLogEntry", (logEntry) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    logEntries.Add(logEntry);
                });
            });

            try
            {
                await connection.StartAsync();
                btnConnect.IsEnabled = false;                
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }

        public class LogEntry
        {
            public string Id { get; set; }
            public DateTime Timestamp { get; set; }
            public string Level { get; set; }
            public string RenderedMessage { get; set; }
            public Dictionary<string, object> Properties { get; set; }
        }
    }
}
