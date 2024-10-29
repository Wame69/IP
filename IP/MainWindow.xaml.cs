using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MultiUserChatApp
{
    public partial class MainWindow : Window
    {
        private Server server;
        private Client client;

        public MainWindow()
        {
            InitializeComponent();
            server = new Server(12345);
            _ = server.Start();
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            string ip = IPTextBox.Text;
            client = new Client();

            try
            {
                await client.Connect(ip, 12345);
                MessageBox.Show("Подключение успешно!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}");
            }
        }

        private async void SendMessageButton_Click(object sender, RoutedEventArgs e)
        {
            if (client == null || !client.IsConnected)
            {
                MessageBox.Show("Подключитесь к серверу перед отправкой сообщения.");
                return;
            }

            string message = ChatInput.Text;

            if (!string.IsNullOrWhiteSpace(message))
            {
                await client.SendMessage(message);
                ChatInput.Clear();
                AddMessageToChat($"Вы: {message}");
            }
            else
            {
                MessageBox.Show("Введите сообщение перед отправкой.");
            }
        }

        private void ChatInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendMessageButton_Click(this, new RoutedEventArgs());
            }
        }

        public void AddMessageToChat(string message)
        {
            ChatMessages.Items.Add(message);
        }

        private class Server
        {
            private TcpListener listener;
            private List<TcpClient> clients = new List<TcpClient>();

            public Server(int port)
            {
                listener = new TcpListener(IPAddress.Any, port);
            }

            public async Task Start()
            {
                listener.Start();
                while (true)
                {
                    var client = await listener.AcceptTcpClientAsync();
                    clients.Add(client);
                    _ = HandleClient(client);
                }
            }

            private async Task HandleClient(TcpClient client)
            {
                var stream = client.GetStream();
                byte[] buffer = new byte[1024];
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    BroadcastMessage(message);
                }
            }

            private void BroadcastMessage(string message)
            {
                foreach (var client in clients)
                {
                    var stream = client.GetStream();
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    stream.WriteAsync(data, 0, data.Length);
                }
            }
        }

        private class Client
        {
            private TcpClient client;
            private NetworkStream stream;

            public bool IsConnected => client?.Connected ?? false;

            public async Task Connect(string ip, int port)
            {
                client = new TcpClient();

                try
                {
                    await client.ConnectAsync(IPAddress.Parse(ip), port);
                    stream = client.GetStream();
                    _ = ListenForMessages();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Ошибка при подключении к серверу.", ex);
                }
            }

            public async Task SendMessage(string message)
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("Не удалось отправить сообщение: поток не инициализирован.");
                }

                byte[] data = Encoding.UTF8.GetBytes(message);
                await stream.WriteAsync(data, 0, data.Length);
            }

            private async Task ListenForMessages()
            {
                byte[] buffer = new byte[1024];
                int bytesRead;

                try
                {
                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ((MainWindow)Application.Current.MainWindow).AddMessageToChat($"Друг: {message}");
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при получении сообщения: {ex.Message}");
                }
            }
        }
    }
}
