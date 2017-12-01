using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Threading;
using System.Security.Cryptography;
using System.IO;

namespace Cexio
{
    
    /// <summary>
    /// BTC/USD Exchange [Cex.io]
    /// </summary>
    public partial class MainWindow : Window
    {
       
        public static string CEXIO_BTCUSD_URL = @"https://cex.io/api/last_price/BTC/USD";
        public static string CEXIO_BALANCE_URL = @"https://cex.io/api/balance/";

        internal static string USER_ID;
        internal static string SEC_API_KEY;
        internal static string API_KEY;


        internal static MainWindow Main;
        private static Timer TaskTimer;

        public MainWindow()
        {
            InitializeComponent();

            Main = this;


            List<string> settings = GetUserSetting();

            USER_ID = settings[0];
            API_KEY = settings[1];
            SEC_API_KEY = settings[2];

            StartTimer();    
        }


        public static void StartTimer()
        {
            var startTime = TimeSpan.Zero;
            var periodTime = TimeSpan.FromSeconds(5);

            TaskTimer = new Timer((e) => {

                    // Use other threads
                    Main.Dispatcher.Invoke(() =>
                    {
                        try
                        {

                           GetBTCUSD(CEXIO_BTCUSD_URL);

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    });
                 
            }, null, startTime, periodTime);

        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }


        public static async void GetBTCUSD(string address)
        {
            
            using (HttpClient client = new HttpClient())
            {

                try
                {
                    var resultHttp = await Task.Run(() => client.GetAsync(address).Result);

                    resultHttp.EnsureSuccessStatusCode();
                    var jsonHttp = resultHttp.Content.ReadAsStringAsync().Result;

                    dynamic JSON = JsonConvert.DeserializeObject(jsonHttp);

                    Console.WriteLine(JSON);

                    var lastPrice = JSON.lprice;

                    Main.ExchangeLabel.Content = $"{JSON.curr1}/{JSON.curr2} {lastPrice}";

                    // DO NOT SHARE
                   
                    var nonce = GetTimeStamp().ToString();

                    //var nonce =+ 10;
                    var signature = CreateCEXHash(nonce.ToString(), USER_ID, API_KEY, SEC_API_KEY);

                    Console.WriteLine(signature);

                    GetAccountBalance(CEXIO_BALANCE_URL, API_KEY, signature, nonce, lastPrice.ToString());
                }

                catch (HttpRequestException htex)
                {
                    Console.WriteLine(htex.Message);
                }
            }
        }

        private static async void GetAccountBalance(string address, string key, string signature, string nonce, string lprice)
        {
            using (HttpClient client = new HttpClient())
            {
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("key", key),
                    new KeyValuePair<string, string>("signature", signature),
                    new KeyValuePair<string, string>("nonce", nonce)
                });

                try {

                    var resultHttp = await Task.Run(() => client.PostAsync(address, content).Result);
                    resultHttp.EnsureSuccessStatusCode();

                    string response = resultHttp.Content.ReadAsStringAsync().Result;
                    
                    dynamic accJson = JsonConvert.DeserializeObject(response);

                    if (accJson.error == null)
                    {
                        string mybtc = Convert.ToString(accJson.BTC["available"]);

                        try
                        {

                            if (mybtc != null)
                            {
                                string myUSD = Convert.ToString(CalculateUSD(accJson.BTC.available.ToString(), lprice));

                                Main.BTCBalance.Content = $"BTC {accJson.BTC.available}";
                                Main.USDPure.Content = $"USD {myUSD}";
                                
                            }
                        }
                        catch (NullReferenceException nuex)
                        {
                            Console.WriteLine(nuex.Message);
                        }

                    }

                }
                catch (HttpRequestException htex)
                {
                    Console.WriteLine(htex.Message);
                }

            }
        }


        private static Decimal CalculateUSD(string BTC, string BTCPerUSD)
        {
            Decimal BTCDec = System.Convert.ToDecimal(BTC);
            Decimal BTCPerUSDDec = System.Convert.ToDecimal(BTCPerUSD);
            Decimal CexioCommisionToVisaCard = Convert.ToDecimal(3.80);

            var cleanUSD = (BTCDec * BTCPerUSDDec) - CexioCommisionToVisaCard;

            return Math.Round(cleanUSD, 3);
        }


        private static string CreateCEXHash(string nonce, string userid, string apikey, string secapikey)
        {
            var message = nonce + userid + apikey;

            var byteMessage = StringEncode(message);
            var byteSecapikey = StringEncode(secapikey);

            var hash = new HMACSHA256(byteSecapikey);

            var hexHash = hash.ComputeHash(byteMessage);

            return HashEncode(hexHash);

        } 

        private static byte[] StringEncode(string text)
        {
            var dat = new UTF8Encoding();
            return dat.GetBytes(text);
        }

        private static string HashEncode(byte[] hash)
        {
            return BitConverter.ToString(hash).Replace("-", "").ToUpper();
        }

        private static long GetTimeStamp()
        {
            return DateTimeOffset.Now.ToUnixTimeSeconds();

        }

        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {

            ContextMenu cMenu = (ContextMenu)this.Resources["CexioDeskContextMenu"];
            cMenu.IsOpen = true;

        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private static List<string> GetUserSetting()
        {
            string path = Directory.GetCurrentDirectory();
            string settingFile = System.IO.Path.Combine(path, "cx.csv");

            using (var reader = new StreamReader(settingFile))
            {
                List<string> userList = new List<string>();
                //var x = 0;

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(';');

                    userList.Add(values[0]);
                    userList.Add(values[1]);
                    userList.Add(values[2]);
                }

                return userList;
            }

        }
    }
}
