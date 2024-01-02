using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TroikaReader.Classes
{
    class TroikaCard
    {
        public string id { set; get; }
        public double balance { set; get; }
        public DateTime? lastRide { set; get; }
        public DateTime? stationSwitch { set; get; }
    }
}
