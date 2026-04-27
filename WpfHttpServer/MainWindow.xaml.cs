using System;
using System.Net;
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

namespace WpfHttpServer
{

    public partial class MainWindow : Window
    {

        private HttpListener _listener;
        private int _getCount = 0;
        private int _postCount = 0;
        private List<string> _message = new List<string>();

        public MainWindow()
        {
            InitializeComponent();
        }
        
        private async Task HandleRequestsAsync()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();

                    var request = context.Request;
                    var response = context.Response;

                    AddLog($"Получен {request.HttpMethod} запрос.");

                    string responseString = "";

                    if (request.HttpMethod == "GET")
                    {
                        var stats = new
                        {
                            getCount = _getCount,
                            postCount = _postCount,
                            totalRequest = _getCount + _postCount,
                        };
                        _getCount++;
                        responseString = System.Text.Json.JsonSerializer.Serialize(stats);
                        response.ContentType = "application/json";
                        response.StatusCode = 200;
                        

                        AddLog($"Ответ на GET: {responseString}");
                    } else if (request.HttpMethod == "POST")
                    {
                        

                        _postCount++;
                        responseString = "POST получен, но обработка пока не реализована";
                        response.ContentType = "text/plain";
                        response.StatusCode = 200;
                    }

                    byte[] buffer = Encoding.UTF8.GetBytes(responseString);

                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.OutputStream.Close();

                    AddLog($"Отправлен ответ {responseString}.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка ${ex.ToString()}");
                }
            }
        }

        private async void StartServerButton_Click( object sender, RoutedEventArgs e)
        {
            if (_listener != null && _listener.IsListening)
            {
                _listener.Stop();
                _listener.Close();
                StartServerButton.Content = "Запустить сервер";
                AddLog("Сервер остановлен");
                return;
            }

            int port = int.Parse(PortTextBox.Text);

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Start();

            StartServerButton.Content = "Остановить сервер";

            AddLog($"Сервер запущен на порту {port}");

            _ = Task.Run(HandleRequestsAsync);
        }

        private void AddLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText($"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}");
                LogTextBox.ScrollToEnd();
            });
        }
    }
}