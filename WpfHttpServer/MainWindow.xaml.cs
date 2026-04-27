using ScottPlot.WPF;
using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net.Http;

namespace WpfHttpServer
{

    public partial class MainWindow : Window
    {

        private HttpListener _listener;

        private int _getCount = 0;
        private int _postCount = 0;

        private List<string> _messages = new List<string>();
        private List<long> _processingTimesMs = new List<long>();
        private List<Logs> _logs = new List<Logs>();

        private readonly HttpClient _httpClient = new HttpClient();

        public MainWindow()
        {
            InitializeComponent();
        }

        public class Logs
        {
            public DateTime Timestamp { get; set; }
            public string Method { get; set; } = "";
            public string Url { get; set; } = "";
            public int StatusCode { get; set; }
            public string Message { get; set; } = "";

        }
        
        private async Task HandleRequestsAsync()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var startTime = DateTime.Now;
                    var context = await _listener.GetContextAsync();

                    var request = context.Request;
                    var response = context.Response;

                    var options = new JsonSerializerOptions
                    {
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };

                    //AddLog($"Получен {request.HttpMethod} запрос.");

                    string responseString = "";

                    if (request.HttpMethod == "GET")
                    {
                        _getCount++;

                        var stats = new
                        {
                            getCount = _getCount,
                            postCount = _postCount,
                            totalRequest = _getCount + _postCount,
                            messagesCount = _messages.Count,
                            avgProcessingTimeMs = AvgProcessingTimeMs,
                            messages = _messages
                        };

                        responseString = System.Text.Json.JsonSerializer.Serialize(stats, options);
                        response.ContentType = "application/json";
                        response.StatusCode = 200;


                        //AddLog($"Ответ на GET: {responseString}");
                    } else if (request.HttpMethod == "POST")
                    {
                        _postCount++;

                        string body = "";
                        using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                        {
                            body = await reader.ReadToEndAsync();
                        }

                        //AddLog($"Тело запроса: {body}");

                        try
                        {
                            var jsonData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(body);

                            if (jsonData != null && jsonData.TryGetValue("message", out string? messageText) && !string.IsNullOrEmpty(messageText))
                            {
                                string id = Guid.NewGuid().ToString();
                                _messages.Add($"{id}: {messageText}");

                                var responseData = new
                                {
                                    id = id,
                                    message = "Сообщение сохранено",
                                    text = messageText
                                };

                                

                                responseString = System.Text.Json.JsonSerializer.Serialize(responseData, options);
                                response.ContentType = "application/json";
                                response.StatusCode = 201;

                                //AddLog($"Сообщение сохранено ");
                            } else
                            {
                                var errorData = new { error = "Поле 'message' обязательно" };
                                responseString = System.Text.Json.JsonSerializer.Serialize(errorData);
                                response.ContentType = "application/json";
                                response.StatusCode = 400;

                                //AddLog($"Ошибка: нео поля 'message'");
                            } 

                        } catch (System.Text.Json.JsonException) 
                        {
                            var errorData = new { error = "Поле 'message' обязательно" };
                            responseString = System.Text.Json.JsonSerializer.Serialize(errorData);
                            response.ContentType = "application/json";
                            response.StatusCode = 400;

                            //AddLog($"Ошибка: неверный Json");
                        }
                    }

                    byte[] buffer = Encoding.UTF8.GetBytes(responseString);

                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.OutputStream.Close();

                    var processingTime = (long)(DateTime.Now - startTime).TotalMilliseconds;
                    _processingTimesMs.Add(processingTime);

                    AddLog(request.HttpMethod, request.RawUrl, response.StatusCode, responseString);
                    //AddLog($"Отправлен ответ {responseString}.");
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
                AddLog("Сервер", "", 0, "Сервер остановлен");

                return;
            }

            int port = int.Parse(PortTextBox.Text);

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Start();

            StartServerButton.Content = "Остановить сервер";

            AddLog("Сервер", "", 0, $"Сервер запущен на порту {port}");

            _ = Task.Run(HandleRequestsAsync);
        }

        private void AddLog(string method, string url, int statusCode, string message)
        {
            var entry = new Logs
            {
                Timestamp = DateTime.Now,
                Method = method,
                Url = url,
                StatusCode = statusCode,
                Message = message
            };
            
            _logs.Add(entry);

            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText($"[{entry.Timestamp:HH:mm:ss}] {method} {url} - {statusCode} - {message}{Environment.NewLine}"); 
                LogTextBox.ScrollToEnd();
            });
            UpdateChart();
        }


        private void DownloadLogsButton_Click(object sender, RoutedEventArgs e)
        {
            var logToSave = new
            {
                exportedAt = DateTime.Now,
                totalRequests = _getCount + _postCount,
                logs = _logs
            };

            string json = System.Text.Json.JsonSerializer.Serialize(logToSave);

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "stats",
                DefaultExt = ".json",
                Filter = "JSON document (.json)|*.json"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {

                    File.WriteAllText(saveFileDialog.FileName, json);
                    AddLog("Сервер", "", 0, $"Логи сохранены в: {saveFileDialog.FileName}");
                } catch (Exception ex)
                {
                    AddLog("Ошибка", "", 0, $"Сохранение логов: {ex.Message}");

                }

            }
        }

        private double AvgProcessingTimeMs
        {
            get
            {
                if (_processingTimesMs.Count == 0) return 0;
                long sum = 0;
                foreach (var time in _processingTimesMs)
                    sum += time;
                return (double)sum / _processingTimesMs.Count;
            }
        }

        private void ApplyFilterLogsButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = (FiterLogs.SelectedItem as ComboBoxItem)?.Content.ToString();

            Dispatcher.Invoke(() =>
            {
                LogTextBox.Clear();

                IEnumerable<Logs> filtered = _logs;

                switch (selected)
                {
                    case "GET":
                        filtered = _logs.Where(l => l.Method == "GET");
                        break;
                    case "POST":
                        filtered = _logs.Where(l => l.Method == "POST");
                        break;
                    case "Ошибки":
                        filtered = _logs.Where(l => l.StatusCode > 400);
                        break;
                }

                foreach(var entry in filtered)
                {
                    LogTextBox.AppendText($"[{entry.Timestamp:HH:mm:ss}] {entry.Method} {entry.Url} - {entry.StatusCode} - {entry.Message}{Environment.NewLine}");
                }
            });
        }

        private void UpdateChart()
        {
            Dispatcher.Invoke(() =>
            {
                var model = new PlotModel { Title = "Нагрузка по секундам" };

                var dateAxis = new DateTimeAxis
                {
                    Position = AxisPosition.Bottom,
                    StringFormat = "mm:ss",
                    Title = "Время",
                    IntervalType = DateTimeIntervalType.Seconds,
                    MinorIntervalType = DateTimeIntervalType.Seconds
                };
                model.Axes.Add(dateAxis);
                model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Кол-во запросов" });

                var grouped = _logs
                    .GroupBy(l => new DateTime(l.Timestamp.Year, l.Timestamp.Month, l.Timestamp.Day,
                                                l.Timestamp.Hour, l.Timestamp.Minute, l.Timestamp.Second))
                    .ToDictionary(g => g.Key, g => g.Count());

                if (grouped.Count == 0) return;

                var minSecond = grouped.Keys.Min();
                var maxSecond = grouped.Keys.Max();

                var series = new LineSeries();
                var current = minSecond;

                while (current <= maxSecond)
                {
                    int count = grouped.ContainsKey(current) ? grouped[current] : 0;
                    series.Points.Add(new DataPoint(current.ToOADate(), count));
                    current = current.AddSeconds(1);
                }

                model.Series.Add(series);
                LoadChart.Model = model;
            });
        }



        // Клиент
        private async void SendRequestButton_Click(object sender, EventArgs e)
        {
            string url = UrlTextBox.Text;
            string method = (MethodComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "GET";

            ResponseTextBox.Text = "Отправка...";

            try
            {
                if (method == "GET")
                {
                    string response = await _httpClient.GetStringAsync(url);
                    ResponseTextBox.Text = response;
                    AddLog("Клиент", url, 200, $"GET успешно");

                }
                else if (method == "POST")
                {
                    string body = BodyTextBox.Text;
                    if (string.IsNullOrWhiteSpace(body))
                    {
                        ResponseTextBox.Text = "Тело пустое";
                        return;
                    }

                    var content = new StringContent(body, Encoding.UTF8, "application/json");
                    HttpResponseMessage httpResponse = await _httpClient.PostAsync(url, content);
                    string response = await httpResponse.Content.ReadAsStringAsync();

                    ResponseTextBox.Text = $"Статус: {(int)httpResponse.StatusCode}\n\n{response}";
                    AddLog("Клиент", url, (int)httpResponse.StatusCode, $"POST завершен");
                }
            }
            catch (Exception ex)
            {
                ResponseTextBox.Text = $"Ошибка: {ex.Message}";
                AddLog("Клиент", url, 0, $"Ошибка: {ex.Message}");
            }

        }
    }
}