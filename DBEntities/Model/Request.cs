using System;
using System.Collections.Generic;

namespace DBEntities.Model
{
    public partial class Request
    {
        public DateTime ReleaseTime { get; set; }
        public int StartFloor { get; set; }
        public int DestinationFloor { get; set; }

     
        public List<Call> Calls { get; set; } = new List<Call>();

 
        public void AddCall(Call call)
        {
            Calls.Add(call);
        }

        public int CallCount => Calls.Count;

  
        public List<int> GetDestinationFloors()
        {
            return Calls.ConvertAll(call => call.DestinationFloor);
        }
    }
}