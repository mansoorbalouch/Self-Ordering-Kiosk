﻿using Newtonsoft.Json;
using SmartKioskApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using static SmartKioskApp.Global;

namespace SmartKioskApp.Views
{
    /// <summary>
    /// Interaction logic for CheckOut.xaml
    /// </summary>
    public partial class CheckOut : Window
    {
        public bool PaymentConfirmed = false;

        bool TransactionCompleted = false;
        public static string PaymentMethod;
        bool InAction = false;
        string _method = "";
        public bool CloseThisScreen = false;
        int cashAmount = 0;
        public bool IsBack = false;
        string dateTime = "";
        public  bool QRPayConfirmed = false;

        SqlCommand cmd = null;
        private string sql = null;
        private string ConnectionString = "Integrated Security=SSPI;" + "Initial Catalog=LocDBKiosk;" + "Data Source=localhost;";
        private SqlConnection conn = null;
        SqlDataReader reader;

        
        DispatcherTimer dispatcherTimer = new DispatcherTimer();
        DispatcherTimer dispatcherTimerQR = new DispatcherTimer();
        DispatcherTimer screenTimer = new DispatcherTimer();

        private readonly DispatcherTimer timer = new DispatcherTimer();
        private int lastDay;
        public CheckOut()
        {
            InitializeComponent();

            MenuViewModel menuViewModel = new MenuViewModel();
            menuViewModel.LoadCategory();
            menuViewModel.LoadIcons();
            backIcon.Source = ItemMenu.BitmaSourceFromByteArray(menuViewModel.myIcons[6].Icon);

            Back.Visibility = Visibility.Visible;
            DueAmountBar.Visibility = Visibility.Hidden;
            CreditAmountBar.Visibility = Visibility.Hidden;
            imgCashPay.Source = ItemMenu.BitmaSourceFromByteArray(menuViewModel.myIcons[5].Icon);
            imgQRPay.Source = ItemMenu.BitmaSourceFromByteArray(menuViewModel.myIcons[9].Icon);

            screenTimer.Tick += new EventHandler(ScreenTimeOut_Tick);
            screenTimer.Interval = new TimeSpan(0, 0, 30);
            screenTimer.Start();

            this.timer.Interval = TimeSpan.FromSeconds(1);
            this.timer.Tick += this.timer_Tick;
            this.lastDay = DateTime.Now.Date.Day;
            this.timer.Start();

        }
        public void btnPayWithCash(object sender, EventArgs e)
        {

            //disable Note Validator
            ReadWrite.Write("Neutral", Global.Actions.Enabled.ToString());
            PaymentBar.Visibility = Visibility.Hidden;
            Back.Visibility = Visibility.Hidden;
            txtPayInstruct.Visibility = Visibility.Hidden;
            PaymentQR.Visibility = Visibility.Hidden;
            txtPayInstruct1.Visibility = Visibility.Visible;
            DueAmountBar.Visibility = Visibility.Visible;
            CreditAmountBar.Visibility = Visibility.Visible;
            PaymentMethod = "Cash";
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();

        }
        private async void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            string _method = "cash";
            if (!InAction)
            {
                InAction = true;
                try
                {
                    string msg = ReadWrite.Read(Global.Actions.Rejection.ToString());
                    if (!string.IsNullOrWhiteSpace(msg))
                    {
                        ReadWrite.Write("", Global.Actions.Rejection.ToString());
                    }
                    if (Global.NextAction == Global.ActionList.None)
                    {
                        //lblWait.Text = "Insert Cash...";
                        Global.NextAction = Global.ActionList.CollectingMoney;

                    }
                    if (Global.NextAction == Global.ActionList.CollectingMoney)
                    {
                        Global.General.CreditAmount = Convert.ToInt32(ReadWrite.Read(Global.Actions.AddToAmount.ToString()));
                        if (_method == "cash")
                        {
                            if (cashAmount != Global.General.CreditAmount)
                            {
                                screenTimer.Stop();
                                screenTimer.Start();
                                //ScreenTimeOut.Stop();
                                //ScreenTimeOut.Start();
                            }
                            cashAmount = Global.General.CreditAmount;
                            ItemMenu.orders[0].InsertedAmount = Global.General.CreditAmount;

                            if (CashHandler.GetCashInAmount() >= Global.cartTotalAmount)
                            {
                                Global.General.CreditAmount = Convert.ToInt32(ReadWrite.Read(Global.Actions.AddToAmount.ToString()));
                                ItemMenu.orders[0].InsertedAmount = Global.General.CreditAmount;
                                Console.WriteLine("Triggering dispensing process");
                                Global.NextAction = Global.ActionList.StartDispensing;
                            }
                            else
                            {
                                Global.General.CreditAmount = Convert.ToInt32(ReadWrite.Read(Global.Actions.AddToAmount.ToString()));
                                ItemMenu.orders[0].InsertedAmount = Global.General.CreditAmount;
                                txtCreditAmount.DataContext = ItemMenu.orders[0];

                                await txtCreditAmount.Dispatcher.BeginInvoke(new Action(() =>
                                {

                                    txtCreditAmount.Content = Convert.ToInt32(ReadWrite.Read(Global.Actions.AddToAmount.ToString()));
                                }));
                            }
                        }
                    }
                    if (Global.NextAction == Global.ActionList.StartDispensing)
                    {
                        ReadWrite.Write("Stop", Global.Actions.Enabled.ToString());
                        Global.General.CashInserted = Global.General.CreditAmount;
                        //start dispensing function
                        //tActions.Stop();
                        dispatcherTimer.Stop();
                        PaymentConfirmed = true;
                    }
                    if (CloseThisScreen)
                    {
                        screenTimer.Stop();
                        dispatcherTimer.Stop();
                        Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    Global.General.CreditAmount = 0;
                    Global.NextAction = Global.ActionList.None;
                    InAction = false;
                    //if (TransactionCompleted)
                    //Dispose();
                    

                }
            }
            if (PaymentConfirmed)
            {

                Close();
                this.Close();

            }
        }
        public async void ScreenTimeOut_Tick(object sender, EventArgs e)
        {
            try
            {
                screenTimer.Stop();
                ReadWrite.Write("Stop", Global.Actions.Enabled.ToString());
                this.Close();
                CloseThisScreen = true;


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }
        
        public static int GenNewTicketNo()
        {
            int TicketNo = Convert.ToInt32(ReadWrite.Read(Global.Actions.SaleNo.ToString()));
            TicketNo++;
            ReadWrite.Write(TicketNo.ToString(), Global.Actions.SaleNo.ToString());
            return TicketNo;
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            if (DateTime.Now.Date.Day != this.lastDay)
            {
                this.lastDay = DateTime.Now.Date.Day;
                ReadWrite.Write("0", Global.Actions.SaleNo.ToString());
            }
        }

        //in the class constructor

        public static BitmapSource BitmaSourceFromByteArray(byte[] buffer)
        {
            var bitmap = new BitmapImage();

            using (var stream = new MemoryStream(buffer))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }

            bitmap.Freeze(); // optionally make it cross-thread accessible
            return bitmap;
        }

        private void btnBack(object sender, RoutedEventArgs e)
        {
            IsBack = true;
            this.Close();
        }

        private void btnPayWithQR(object sender, RoutedEventArgs e)
        {
            _ = FunGenerateQR();
            _method = "QR";
            dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            PaymentBar.Visibility = Visibility.Hidden;
            Back.Visibility = Visibility.Hidden;
            txtPayInstruct.Visibility = Visibility.Hidden;
            PaymentQR.Visibility = Visibility.Hidden;
            //QRcodeBar.Visibility = Visibility.Visible;
            txtPayInstruct2.Visibility = Visibility.Visible;
            DueAmountBar.Visibility = Visibility.Visible;
            CreditAmountBar.Visibility = Visibility.Visible;
            QRcode.Visibility = Visibility.Visible;
            PaymentMethod = "QR";
            dispatcherTimerQR.Tick += new EventHandler(dispatcherTimerQR_Tick);
            dispatcherTimerQR.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimerQR.Start();
        }
        private async Task<bool> FunGenerateQR()
        {
            string @UserId = "DigitalTrans_QR";
            string @Password = "Alf@D!g!t@l*1";
            string @TransactionId = "DT123";
            string @CompanyId = "DigiTrans";
            string @ProductId = "DT456";
            string @OrderId = "DT789";
            int @Amount = ItemMenu.orders[0].DueAmount;
            string @MerchantPAN = ConfigurationManager.AppSettings["PAN"].ToString();
            //string @MerchantPAN = "5333387519489734";
            string @DataHash = "";
            // Hash calculation  ///
            string ConcatenateStringofRequest = @UserId + "+" + @Password + "+" + @TransactionId + "+" + @CompanyId + "+" + @ProductId + "+" + @OrderId + "+" + @Amount + "+" + @MerchantPAN;
            byte[] key = Encoding.ASCII.GetBytes("88moQHxKJbtvFzk4yqF7CAwgeGyMYVakbSNPjPOKWIQYB6xgikjqf5caaQiVqgQ");
            HMACSHA256 myhmacsha1 = new HMACSHA256(key);
            byte[] byteArray = Encoding.ASCII.GetBytes(ConcatenateStringofRequest);
            MemoryStream stream = new MemoryStream(byteArray);
            @DataHash = myhmacsha1.ComputeHash(stream).Aggregate("", (s, e) => s + String.Format("{0:x2}", e), s => s);
            Console.WriteLine("Hitting Api .......!");
            try
            {
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri("https://payments.bankalfalah.com/mVisaAcq/AlfaPay.svc/GenerateQRString");
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var requestBody = new
                    {
                        UserId = @UserId,
                        Password = @Password,
                        TransactionId = @TransactionId,
                        CompanyId = @CompanyId,
                        ProductId = @ProductId,
                        OrderId = @OrderId,
                        Amount = @Amount,
                        MerchantPAN = @MerchantPAN,
                        DataHash = @DataHash
                    };
                    HttpResponseMessage apiResponse = await client.PostAsJsonAsync(client.BaseAddress, requestBody);
                    _ResponseData _responseData = new _ResponseData();

                    Console.WriteLine("Response API .......!\r\n");
                    Console.WriteLine(apiResponse + "\r\n");
                    if (apiResponse.IsSuccessStatusCode)
                    {
                        var documentResponse = await apiResponse.Content.ReadAsStringAsync();
                        _responseData = JsonConvert.DeserializeObject<_ResponseData>(documentResponse);
                        Console.WriteLine(_responseData);
                        Console.WriteLine("URL: {0}", _responseData.URL);
                        Console.WriteLine("ResponseCode: {0}", _responseData.ResponseCode);
                        Console.WriteLine("Response Description: {0}", _responseData.ResponseDesc);
                        Console.WriteLine("QR-Code Image string: {0}", _responseData.QRCode);


                        //byte[] arr = Encoding.ASCII.GetBytes(_responseData.QRCode);
                        byte[] img = Convert.FromBase64String(_responseData.QRCode);
                        QRcode.Source = ItemMenu.BitmaSourceFromByteArray(img);

                    }
                    //  creditamount = @Amount;

                    return true;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public async void dispatcherTimerQR_Tick(object sender, EventArgs e)
        {
            if (!InAction )
            {
                InAction = true;
                try
                {
                    //lblWait.Text = "Reading...";
                    if (Global.NextAction == Global.ActionList.None)
                    {
                        //lblWait.Text = "Scan Now...";
                        Global.NextAction = Global.ActionList.CollectingMoney;
                    }
                    if (Global.NextAction == Global.ActionList.CollectingMoney)
                    {
                        _ = ReadWrite.ReadQRAmountAPIAsync(dateTime, ConfigurationManager.AppSettings["PAN"].ToString());
                        if (ReadWrite.PaidAmount > 0)
                        {
                            ReadWrite.Write(cashAmount.ToString(), Global.Actions.AddToAmount.ToString());
                        }
                        Global.General.CreditAmount = Convert.ToInt32(ReadWrite.Read(Global.Actions.AddToAmount.ToString()));
                        if (_method == "QR")
                        {
                            if (ReadWrite.PaidAmount != Global.General.CreditAmount)
                            {
                                screenTimer.Stop();
                                screenTimer.Start();
                            }
                            cashAmount = Global.General.CreditAmount;
                            if (CashHandler.GetCashInAmount() >= Global.cartTotalAmount)
                            {
                                Global.General.CreditAmount = Convert.ToInt32(ReadWrite.Read(Global.Actions.AddToAmount.ToString()));
                                txtCreditAmount.DataContext = Global.General.CreditAmount.ToString();
                                Console.WriteLine("Transaction in process!");
                                Global.NextAction = Global.ActionList.StartDispensing;
                            }

                        }
                        //lblWait.Text = "Sacn QR with above apps ...";
                    }

                    if (Global.NextAction == Global.ActionList.StartDispensing)
                    {
                        dispatcherTimerQR.Stop();
                        QRPayConfirmed = true;
                    }
                    if (CloseThisScreen)
                    {
                        screenTimer.Stop();
                        dispatcherTimer.Stop();
                        Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    Global.General.CashInserted = 0;
                    Global.NextAction = Global.ActionList.None;
                    InAction = false;
                }
            }
            if (PaymentConfirmed)
            {

                Close();
                this.Close();

            }
        }
    }
}