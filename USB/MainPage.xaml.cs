using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace serialPortUWPv2
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private SerialDevice serialPort = null;
        private SolarCalc solarCalc = new SolarCalc();

        DataWriter dataWriterObject = null;
        DataReader dataReaderObject = null;

        private ObservableCollection<DeviceInformation> listOfDevices;

        private CancellationTokenSource ReadCancellationTokenSource;

        string recived = "";

        public MainPage()
        {
            this.InitializeComponent();

            listOfDevices = new ObservableCollection<DeviceInformation>();
            ListAvailablePorts();

        }

        private async void ListAvailablePorts()
        {
            try
            {
                string aqs = SerialDevice.GetDeviceSelector();
                var dis = await DeviceInformation.FindAllAsync(aqs);

                for (int i = 0; i < dis.Count; i++)
                {
                    listOfDevices.Add(dis[i]);
                }

                lstSerialDevices.ItemsSource = listOfDevices;

                lstSerialDevices.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                txtMessage.Text = ex.Message;
            }
        }

        private void ButtonConnectToDevice_Click(object sender, RoutedEventArgs e)
        {
            SerialPortConfiguration();
        }

        private async void SerialPortConfiguration()
        {
            var selection = lstSerialDevices.SelectedItems;

            if (selection.Count <= 0)
            {
                txtMessage.Text = "Select an object for serial connection!";
                return;
            }

            DeviceInformation entry = (DeviceInformation)selection[0];
            try
            {
                serialPort = await SerialDevice.FromIdAsync(entry.Id);
                serialPort.WriteTimeout = TimeSpan.FromMilliseconds(1000);
                serialPort.ReadTimeout = TimeSpan.FromMilliseconds(1000);
                serialPort.BaudRate = 115200;
                serialPort.Parity = SerialParity.None;
                serialPort.StopBits = SerialStopBitCount.One;
                serialPort.DataBits = 8;
                serialPort.Handshake = SerialHandshake.None;
                txtMessage.Text = "Serial Port Correctly Configured!";

                ReadCancellationTokenSource = new CancellationTokenSource();

                Listen();
            }

            catch (Exception ex)
            {
                txtMessage.Text = ex.Message;
            }
        }

        private async void Listen()
        {
            try
            {
                if (serialPort != null)
                {
                    dataReaderObject = new DataReader(serialPort.InputStream);

                    while (true)
                    {
                        await ReadData(ReadCancellationTokenSource.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                txtMessage.Text = ex.Message;

                // if(ex.GetType.Name == "TaskCancelledException")
            }
            finally
            {

            }
        }

        private async Task ReadData(CancellationToken cancellationToken)
        {
            Task<UInt32> loadAsyncTask;

            int calChkSum = 0;

            uint ReadBufferLength = 1;

            cancellationToken.ThrowIfCancellationRequested();

            dataReaderObject.InputStreamOptions = InputStreamOptions.Partial;

            loadAsyncTask = dataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken);

            UInt32 bytesRead = await loadAsyncTask;

            if (bytesRead > 0)
            {
                recived += dataReaderObject.ReadString(bytesRead);
                // txtRecieved.Text = recived;
                if (recived[0] == '#')
                {
                    if (recived.Length > 3)
                    {
                        if (recived[2] == '#')
                        {
                            //txtRecieved.Text = recived;
                            if (recived.Length > 42)
                            {
                                txtRecieved.Text = recived + txtRecieved.Text;
                                // add parse code 
                                txtPacketNum.Text = recived.Substring(3, 3);
                                txtAN0.Text = recived.Substring(6, 4);
                                txtAN1.Text = recived.Substring(10, 4);
                                txtAN2.Text = recived.Substring(14, 4);
                                txtAN3.Text = recived.Substring(18, 4);
                                txtAN4.Text = recived.Substring(22, 4);
                                txtAN5.Text = recived.Substring(26, 4);
                                txtBinOut.Text = recived.Substring(30, 8);
                                txtCalChkSum.Text = recived.Substring(38, 3);

                                for (int i = 3; i < 38; i++)
                                {
                                    calChkSum += (byte)recived[i];
                                }
                                txtCalChkSum.Text = Convert.ToString(calChkSum);
                                int an0 = Convert.ToInt32(recived.Substring(6, 4));
                                int an1 = Convert.ToInt32(recived.Substring(10, 4));
                                int an2 = Convert.ToInt32(recived.Substring(14, 4));
                                int an3 = Convert.ToInt32(recived.Substring(18, 4));
                                int an4 = Convert.ToInt32(recived.Substring(22, 4));
                                int an5 = Convert.ToInt32(recived.Substring(26, 4));

                                int recChkSum = Convert.ToInt32(recived.Substring(38, 3));
                                calChkSum %= 1000;
                                if (recChkSum == calChkSum)
                                {
                                    txtSolarVolt.Text = solarCalc.GetSolarVoltage(an0);
                                    txtBatteryVolt.Text = solarCalc.GetBatteryVoltage(an2);
                                    txtBatteryCurrent.Text = solarCalc.GetBatteryCurrent(an1, an2);
                                    txtLED1Current.Text = solarCalc.GetLEDcurrent(an4, an1);
                                    txtLED2Current.Text = solarCalc.GetLEDcurrent(an3, an1);

                                }
                                recived = "";
                            }
                        }
                        else
                        {
                            recived = "";
                        }

                    }

                }
                else
                {
                    recived = "";
                }
            }
        }

        private async void ButtonWrite_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort != null)
            {
                var dataPacket = txtSend.Text.ToString();
                dataWriterObject = new DataWriter(serialPort.OutputStream);
                await SendPacket(dataPacket);

                if (dataWriterObject != null)
                {
                    dataWriterObject.DetachStream();
                    dataWriterObject = null;
                }

            }
        }

        private async Task SendPacket(string value)
        {
            var dataPacket = value;
            Task<UInt32> storeAsyncTask;
            if (dataPacket.Length != 0)
            {
                dataWriterObject.WriteString(dataPacket);

                storeAsyncTask = dataWriterObject.StoreAsync().AsTask();

                UInt32 bytesWritten = await storeAsyncTask;
                if (bytesWritten > 0)
                {
                    txtMessage.Text = "Values Set Correctly";
                }
            }
            else
            {
                txtMessage.Text = "No Value Sent";
            }
        }

    }
}