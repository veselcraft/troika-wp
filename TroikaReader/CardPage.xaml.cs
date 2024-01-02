using TroikaReader.Common;
using TroikaReader.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using System.Text;

namespace TroikaReader
{
    public sealed partial class CardPage : Page
    {
        private NavigationHelper navigationHelper;
        private ObservableDictionary defaultViewModel = new ObservableDictionary();

        TroikaCard CardObject;

        public CardPage()
        {
            this.InitializeComponent();

            this.navigationHelper = new NavigationHelper(this);
            this.navigationHelper.LoadState += this.NavigationHelper_LoadState;
            this.navigationHelper.SaveState += this.NavigationHelper_SaveState;
        }
        
        public NavigationHelper NavigationHelper
        {
            get { return this.navigationHelper; }
        }
        
        public ObservableDictionary DefaultViewModel
        {
            get { return this.defaultViewModel; }
        }
        
        private void NavigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
        }
        
        private void NavigationHelper_SaveState(object sender, SaveStateEventArgs e)
        {
        }
        
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedTo(e);

            CardObject = (TroikaCard)e.Parameter;
            
            StringBuilder result = new StringBuilder(CardObject.id);
            for (int i = 0; i < (10 - CardObject.id.Length); i++) {
                result.Insert(0, "0");
            }
            result.Insert(4, " ");
            result.Insert(8, " ");
            troikaIdLabel.Text = result.ToString();

            balanceLabel.Text = string.Format("на карте {0} ₽", CardObject.balance.ToString("0"));

            if (CardObject.lastRide != null)
            {
                DateTime lastRide = (DateTime)CardObject.lastRide;
                lastRideLabel.Text = string.Format("последняя поездка: {0}", lastRide.ToString("dd.MM.yyyy HH:mm"));
            } else
            {
                lastRideLabel.Visibility = Visibility.Collapsed;
            }

            if (CardObject.stationSwitch != null)
            {
                DateTime switchDate = (DateTime)CardObject.stationSwitch;
                stationSwitchLabel.Text = string.Format("действует пересадка на мцд/бкл до: {0}", switchDate.ToString("HH:mm"));
            } else
            {
                stationSwitchLabel.Visibility = Visibility.Collapsed;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedFrom(e);
        }
    }
}
