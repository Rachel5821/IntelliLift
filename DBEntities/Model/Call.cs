using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBEntities.Model
{
    public partial class Call
    {
        public DateTime ReleaseTime { get; set; }
        public int StartFloor { get; set; }
        public int DestinationFloor { get; set; }



    }
}
