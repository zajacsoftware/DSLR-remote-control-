using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EOSDigital.API;
using EOSDigital.SDK;
using bd.tools.net;
using System.Windows.Threading;
using System.Net;
using System.Text;
namespace DSLR-remote-control
{

    public partial class MainWindow : Window
    {
        #region Variables

        CanonAPI APIHandler;
        Camera MainCamera;
        CameraValue[] AvList;
        CameraValue[] TvList;
        CameraValue[] ISOList;
        List<Camera> CamList;
        bool IsInit = false;
        int BulbTime = 30;
        ImageBrush bgbrush = new ImageBrush();
        Action<BitmapImage> SetImageAction;
        System.Windows.Forms.FolderBrowserDialog SaveFolderBrowser = new System.Windows.Forms.FolderBrowserDialog();

        int ErrCount;
        object ErrLock = new object();

        #endregion

        private AsynchronousSocketListener socetListener;
        public string newFileName;
        private string localPath;

    

        public MainWindow()
        {

            Console.WriteLine(DateTime.Now.ToString("MM-dd-yyyy-HH.mm.ss"));
         
            try
            {
                InitializeComponent();

                socetListener = new AsynchronousSocketListener();
                socetListener.SocketEvent += SocetListener_SocketEvent;
                socetListener.Start();

                APIHandler = new CanonAPI();
                APIHandler.CameraAdded += APIHandler_CameraAdded;
                ErrorHandler.SevereErrorHappened += ErrorHandler_SevereErrorHappened;
                ErrorHandler.NonSevereErrorHappened += ErrorHandler_NonSevereErrorHappened;
                localPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "RemotePhoto");
                SavePathTextBox.Text = localPath;

                SetImageAction = (BitmapImage img) => { bgbrush.ImageSource = img; };
                SaveFolderBrowser.Description = "Save Images To...";
                RefreshCamera();
                IsInit = true;


            }
            catch (DllNotFoundException) { ReportError("Canon DLLs not found!", true); }
            catch (Exception ex) { ReportError(ex.Message, true); }
        }

        System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();



        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
        
        }

        private string nextQRString = "";
        private void SocetListener_SocketEvent(object sender, SocketListenerEventArgs e)
        {
            switch (e.type)
            {
                case SocketListenerEventArgs.MESSAGE_RECEIVED:
                    Dispatcher.Invoke(new Action(() => {
                        string[] ar = e.data.Split(':');

                        hostInfo.Text = " data received " + e.data;
                        if (ar!=null && ar.Length == 3 && ar[0]=="click" && ar[1].Length > 0)
                        {
                            nextQRString = ar[1];
                            takeAhoto();
                        }

                    }), DispatcherPriority.ContextIdle);
                    break;
                case SocketListenerEventArgs.SERVER_STERTED:
                    Dispatcher.Invoke(new Action(() => {
                        hostInfo.Text = "Server started. listening on:" + e.data;
                    }), DispatcherPriority.ContextIdle);
                    break;
                default:
                    //do nothing 
                    break;

            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                IsInit = false;
                MainCamera?.Dispose();
                APIHandler?.Dispose();
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        #region API Events

        private void APIHandler_CameraAdded(CanonAPI sender)
        {
            try { Dispatcher.Invoke((Action)delegate { RefreshCamera(); }); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void MainCamera_StateChanged(Camera sender, StateEventID eventID, int parameter)
        {

            try
            {
                if (eventID == StateEventID.Shutdown && IsInit)
                {
                    Dispatcher.Invoke((Action)delegate { CloseSession(); });
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void MainCamera_ProgressChanged(object sender, int progress)
        {
            try { MainProgressBar.Dispatcher.Invoke((Action)delegate { MainProgressBar.Value = progress; }); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void MainCamera_LiveViewUpdated(Camera sender, Stream img)
        {
            try
            {
                using (WrapStream s = new WrapStream(img))
                {
                    img.Position = 0;
                    BitmapImage EvfImage = new BitmapImage();
                    EvfImage.BeginInit();
                    EvfImage.StreamSource = s;
                    EvfImage.CacheOption = BitmapCacheOption.OnLoad;
                    EvfImage.EndInit();
                    EvfImage.Freeze();
                    Application.Current.Dispatcher.BeginInvoke(SetImageAction, EvfImage);
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }


        private bool busy = false;
        private void MainCamera_DownloadReady(Camera sender, DownloadInfo Info)
        {
            try
            {
                string dir = null;
                SavePathTextBox.Dispatcher.Invoke((Action)delegate { dir = SavePathTextBox.Text; });
                //Info.FileName = "local.jpg";
                string filename;
                if (nextQRString == "")
                {
                    filename = "local_" + DateTime.Now.ToString("MM-dd-yyyy-HH.mm.ss") + ".jpg"; 
                }
                else
                {
                    filename = nextQRString + ".jpg";
                }

                Info.FileName = filename;

                sender.DownloadFile(Info, dir);
                busy = false;
                nextQRString = "";

                MainProgressBar.Dispatcher.Invoke((Action)delegate { MainProgressBar.Value = 0; });

               
            }
            catch (Exception ex)
            {
                busy = false;
            }
        }

        private void ErrorHandler_NonSevereErrorHappened(object sender, ErrorCode ex)
        {
            ReportError($"SDK Error code: {ex} ({((int)ex).ToString("X")})", false);
        }

        private void ErrorHandler_SevereErrorHappened(object sender, Exception ex)
        {
            ReportError(ex.Message, true);
        }

        #endregion

        #region Session

        private void OpenSessionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MainCamera?.SessionOpen == true) CloseSession();
                else OpenSession();
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try { RefreshCamera(); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        #endregion

        #region Settings

        private void AvCoBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (AvCoBox.SelectedIndex < 0) return;
                MainCamera.SetSetting(PropertyID.Av, AvValues.GetValue((string)AvCoBox.SelectedItem).IntValue);
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void TvCoBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (TvCoBox.SelectedIndex < 0) return;

                MainCamera.SetSetting(PropertyID.Tv, TvValues.GetValue((string)TvCoBox.SelectedItem).IntValue);
                if ((string)TvCoBox.SelectedItem == "Bulb")
                {
                    BulbBox.IsEnabled = true;
                    BulbSlider.IsEnabled = true;
                }
                else
                {
                    BulbBox.IsEnabled = false;
                    BulbSlider.IsEnabled = false;
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void ISOCoBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ISOCoBox.SelectedIndex < 0) return;
                MainCamera.SetSetting(PropertyID.ISO, ISOValues.GetValue((string)ISOCoBox.SelectedItem).IntValue);
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void TakePhotoButton_Click(object sender, RoutedEventArgs e)
        {
            takeAhoto();
        }


        private void takeAhoto()
        {
            Console.WriteLine(" takeAhoto ");
            if (busy)
            {
                return;
            }

            busy = true;
            try
            {
                if ((bool)STComputerRdButton.IsChecked) MainCamera.SetSetting(PropertyID.SaveTo, (int)SaveTo.Host);
                else if ((bool)STBothRdButton.IsChecked) MainCamera.SetSetting(PropertyID.SaveTo, (int)SaveTo.Both);
                MainCamera.SetCapacity(4096, int.MaxValue);

                BrowseButton.IsEnabled = true;
                SavePathTextBox.IsEnabled = true;

                MainCamera.TakePhoto();
            }
            catch (Exception ex)
            {
                busy = false;
              
            }
        }

        private void BulbSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try { if (IsInit) BulbBox.Text = BulbSlider.Value.ToString(); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void BulbBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (IsInit)
                {
                    int b;
                    if (int.TryParse(BulbBox.Text, out b) && b != BulbTime)
                    {
                        BulbTime = b;
                        BulbSlider.Value = BulbTime;
                    }
                    else BulbBox.Text = BulbTime.ToString();
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void SaveToRdButton_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (IsInit)
                {
                    if ((bool)STCameraRdButton.IsChecked)
                    {
                        MainCamera.SetSetting(PropertyID.SaveTo, (int)SaveTo.Camera);
                        BrowseButton.IsEnabled = false;
                        SavePathTextBox.IsEnabled = false;
                    }
                    else
                    {
                        if ((bool)STComputerRdButton.IsChecked) MainCamera.SetSetting(PropertyID.SaveTo, (int)SaveTo.Host);
                        else if ((bool)STBothRdButton.IsChecked) MainCamera.SetSetting(PropertyID.SaveTo, (int)SaveTo.Both);

                        MainCamera.SetCapacity(4096, int.MaxValue);
                        BrowseButton.IsEnabled = true;
                        SavePathTextBox.IsEnabled = true;
                    }
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }



        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(SavePathTextBox.Text)) SaveFolderBrowser.SelectedPath = SavePathTextBox.Text;
                if (SaveFolderBrowser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    SavePathTextBox.Text = SaveFolderBrowser.SelectedPath;
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        #endregion

    

        #region Subroutines

        private void CloseSession()
        {
            MainCamera.CloseSession();
            AvCoBox.Items.Clear();
            TvCoBox.Items.Clear();
            ISOCoBox.Items.Clear();
            SettingsGroupBox.IsEnabled = false;
            LiveViewGroupBox.IsEnabled = false;
            SessionButton.Content = "Open Session";
            SessionLabel.Content = "No open session";
          
        }

        private void RefreshCamera()
        {
            CameraListBox.Items.Clear();
            CamList = APIHandler.GetCameraList();
            foreach (Camera cam in CamList) CameraListBox.Items.Add(cam.DeviceName);
            if (MainCamera?.SessionOpen == true) CameraListBox.SelectedIndex = CamList.FindIndex(t => t.ID == MainCamera.ID);
            else if (CamList.Count > 0) CameraListBox.SelectedIndex = 0;
            OpenSession();
        }

        private void OpenSession()
        {
            if (CameraListBox.SelectedIndex >= 0)
            {
                MainCamera = CamList[CameraListBox.SelectedIndex];
                MainCamera.OpenSession();
                MainCamera.LiveViewUpdated += MainCamera_LiveViewUpdated;
                MainCamera.ProgressChanged += MainCamera_ProgressChanged;
                MainCamera.StateChanged += MainCamera_StateChanged;
                MainCamera.DownloadReady += MainCamera_DownloadReady;
   

            SessionButton.Content = "Close Session";
                SessionLabel.Content = MainCamera.DeviceName;
                AvList = MainCamera.GetSettingsList(PropertyID.Av);
                TvList = MainCamera.GetSettingsList(PropertyID.Tv);
                ISOList = MainCamera.GetSettingsList(PropertyID.ISO);
                foreach (var Av in AvList) AvCoBox.Items.Add(Av.StringValue);
                foreach (var Tv in TvList) TvCoBox.Items.Add(Tv.StringValue);
                foreach (var ISO in ISOList) ISOCoBox.Items.Add(ISO.StringValue);
                AvCoBox.SelectedIndex = AvCoBox.Items.IndexOf(AvValues.GetValue(MainCamera.GetInt32Setting(PropertyID.Av)).StringValue);
                TvCoBox.SelectedIndex = TvCoBox.Items.IndexOf(TvValues.GetValue(MainCamera.GetInt32Setting(PropertyID.Tv)).StringValue);
                ISOCoBox.SelectedIndex = ISOCoBox.Items.IndexOf(ISOValues.GetValue(MainCamera.GetInt32Setting(PropertyID.ISO)).StringValue);
                SettingsGroupBox.IsEnabled = true;
                LiveViewGroupBox.IsEnabled = true;

                if ((bool)STComputerRdButton.IsChecked) MainCamera.SetSetting(PropertyID.SaveTo, (int)SaveTo.Host);
                else if ((bool)STBothRdButton.IsChecked) MainCamera.SetSetting(PropertyID.SaveTo, (int)SaveTo.Both);
                MainCamera.SetCapacity(4096, int.MaxValue);
                // MessageBox.Show("dupa", "Error", MessageBoxButton.OK, MessageBoxImage.None);

            }
        }

        private void ReportError(string message, bool lockdown)
        {
            int errc;
            lock (ErrLock) { errc = ++ErrCount; }

            if (lockdown) EnableUI(false);

            if (errc < 4) MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            else if (errc == 4) MessageBox.Show("Many errors happened!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            lock (ErrLock) { ErrCount--; }
        }

        private void EnableUI(bool enable)
        {
            if (!Dispatcher.CheckAccess()) Dispatcher.Invoke((Action)delegate { EnableUI(enable); });
            else
            {
                SettingsGroupBox.IsEnabled = enable;
                InitGroupBox.IsEnabled = enable;
                LiveViewGroupBox.IsEnabled = enable;
            }
        }

        #endregion
    }
}
