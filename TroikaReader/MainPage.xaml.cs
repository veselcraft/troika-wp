using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using MiFare;
using MiFare.Classic;
using MiFare.PcSc;
using Windows.Devices.SmartCards;
using TroikaReader.Classes;

// Документацию по шаблону элемента "Пустая страница" см. по адресу http://go.microsoft.com/fwlink/?LinkId=391641

namespace TroikaReader
{
    /// <summary>
    /// Пустая страница, которую можно использовать саму по себе или для перехода внутри фрейма.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private SmartCardReader Reader;
        private MiFareCard card;

        public MainPage()
        {
            this.InitializeComponent();

            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            Application.Current.UnhandledException += Current_UnhandledException;

            GetDevices();

            this.NavigationCacheMode = NavigationCacheMode.Required;

            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values["firstrun"] == null)
            {
                PopupMessage("Приложение поддерживает новые (выпущенные с 2018 года И с буквой V на задней части карты) и недавно использованные Тройки. Старые карты, которые давно не использовались, могут некорректно показывать информацию о балансе и последней дате поездки." + Environment.NewLine + Environment.NewLine + "О багах сообщайте в GitHub: https://github.com/veselcraft/troika-wp", "К вниманию пассажиру!");
            }
            localSettings.Values["firstrun"] = true;
        }

        /// <summary>
        /// Вызывается перед отображением этой страницы во фрейме.
        /// </summary>
        /// <param name="e">Данные события, описывающие, каким образом была достигнута эта страница.
        /// Этот параметр обычно используется для настройки страницы.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // TODO: Подготовьте здесь страницу для отображения.

            // TODO: Если приложение содержит несколько страниц, обеспечьте
            // обработку нажатия аппаратной кнопки "Назад", выполнив регистрацию на
            // событие Windows.Phone.UI.Input.HardwareButtons.BackPressed.
            // Если вы используете NavigationHelper, предоставляемый некоторыми шаблонами,
            // данное событие обрабатывается для вас.
        }

        #region Handling_UI
        /// <summary>
        /// Display message via dialogue box
        /// </summary>
        /// <returns>None</returns>
        public async void PopupMessage(string message, string title = null)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                var dlg = new MessageDialog(message, title);
                await dlg.ShowAsync();
            });
        }
        #endregion

        #region TroikaParse
        public static double CrapToRubs(byte[] ar)
        {
            double balance = (ar[0] * 256 + ar[1]) / 25.0;
            return balance;
        }

        public static long CrapToId(byte[] ar)
        {
            long number = 0;
            for (int i = 1; i < 5; i++)
            {
                number <<= 8;
                number |= ar[i];
            }
            number >>= 4;
            number |= ((uint)ar[0] & 0xf) << 28;
            return number;
        }

        public static DateTime? CrapToDate(byte[] ar)
        {
            int minutesDelta = ((ar[0] << 16) | (ar[1] << 8) | ar[2]) >> 1;
            if (minutesDelta > 0)
            {
                DateTime c = new DateTime(2018, 12, 31, 0, 0, 0, DateTimeKind.Utc).AddMinutes(minutesDelta).ToLocalTime();
                return c;
            }
            else
            {
                return null;
            }
        }

        public static DateTime? DateUntilChangeStationIsUnavailable(DateTime date)
        {
            if (date == null)
            {
                return null;
            } else
            {
                DateTime newDate = date.AddMinutes(90);
                if (newDate > DateTime.Now)
                {
                    return newDate;
                }
                else
                {
                    return null;
                }
            }
        }
        
        #endregion

        async private void GetDevices()
        {
            try
            {
                Reader = await CardReader.FindAsync();
                if (Reader == null)
                {
                    await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                    {
                        var dlg = new MessageDialog("В вашем смартфоне отсутствует поддержка NFC. Приложение не может работать без него.", "Критическая ошибка");
                        await dlg.ShowAsync();
                        Application.Current.Exit();
                    });
                }

                Reader.CardAdded += CardAdded;
                Reader.CardRemoved += CardRemoved;
            }
            catch (Exception e)
            {
                PopupMessage("Чот пошло не так: " + e.Message);
            }
        }

        public async void CardAdded(SmartCardReader sender, CardAddedEventArgs args)
        {
            try
            {
                await HandleCard(args);
            }
            catch (Exception e)
            {
                PopupMessage("CardAdded Exception: " + e.Message);
            }
        }

        void CardRemoved(SmartCardReader sender, CardRemovedEventArgs args)
        {

            card?.Dispose();
        }

        private async Task HandleCard(CardAddedEventArgs args)
        {
            bool plsDontHide = false;

            try
            {
                await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                     progressGrid.Visibility = Visibility.Visible;
                     statusRing.IsActive = true;
                     statusText.Text = "Читаю карту...";
                });

                card?.Dispose();
                card = args.SmartCard.CreateMiFareCard();
                
                var cardIdentification = await card.GetCardInfo();
                
                if (cardIdentification.PcscDeviceClass == MiFare.PcSc.DeviceClass.StorageClass
                     && (cardIdentification.PcscCardName == CardName.MifareStandard1K || cardIdentification.PcscCardName == CardName.MifareStandard4K))
                {
                    try
                    {
                        await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            statusText.Text = "Читаю данные...";
                        });

                        card.AddOrUpdateSectorKeySet(new SectorKeySet
                        {
                            Key = new byte[] { 0xA7, 0x3F, 0x5D, 0xC1, 0xD3, 0x33 },
                            KeyType = KeyType.KeyA,
                            Sector = 8
                        });

                        var data = await card.GetData(8, 0, 48);

                        // Читаем собсна тройку

                        byte[] rawID = new byte[] { data[2], data[3], data[4], data[5], data[6] };
                        byte[] rawBalance = new byte[] { data[16 + 5], data[16 + 6]};
                        byte[] rawDate = new byte[] { data[16], data[16 + 1], data[16 + 2] };
                        
                        DateTime? lastRide = CrapToDate(rawDate);
                        DateTime? switchStation = null;
                        if (lastRide != null)
                        {
                            switchStation = DateUntilChangeStationIsUnavailable((DateTime)lastRide);
                        }

                        TroikaCard troika = new TroikaCard
                        {
                            id = CrapToId(rawID).ToString(),
                            balance = CrapToRubs(rawBalance),
                            lastRide = lastRide,
                            stationSwitch = switchStation
                        };

                        await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            this.Frame.Navigate(typeof(CardPage), troika);
                        });
                    }
                    catch (Exception)
                    {
                        plsDontHide = true;
                        await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            statusText.Text = "Карта недочитана до конца либо неисправна";
                        });
                    }
                    
                    await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        statusRing.IsActive = false;
                        if (!plsDontHide) progressGrid.Visibility = Visibility.Collapsed;
                    });
                }
            }
            catch (Exception e)
            {
                PopupMessage("HandleCard Exception: " + e.Message);
                await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    statusRing.IsActive = false;
                    if (!plsDontHide) progressGrid.Visibility = Visibility.Collapsed;
                });
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            string message = e.Exception.Message;
            if (e.Exception.InnerException != null)
            {
                message += Environment.NewLine + e.Exception.InnerException.Message;
            }

            PopupMessage(message);
        }

        private void Current_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            string message = e.Exception.Message;
            if (e.Exception.InnerException != null)
            {
                message += Environment.NewLine + e.Exception.InnerException.Message;
            }

            PopupMessage(message);
        }

        private void Image_Tapped(object sender, TappedRoutedEventArgs e)
        {
            PopupMessage("Тройка 1.0.0. Автор: @veselcraft. Под этим же ником на GitHub найдёте исходники этого приложения. " + Environment.NewLine + Environment.NewLine + "Приложение не официальное, не одобрено ГУП \"Московский метрополитен\", имеете в ввиду.", "О программе");
        }
    }
}
