using System;
using System.Collections.Generic;

namespace Project
{
    public partial class Request
    {
        public int Id { get; private set; }
        public DateTime ReleaseTime { get; set; }
        public int StartFloor { get; set; }
        public int DestinationFloor { get; set; }
        private static int nextRequestId = 1;

        public Request()
        {
            Id = nextRequestId++;
        }

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