using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project
{
    public enum Direction
    {
        Up = 1,
        Down = -1,
        Idle = 0
    }
    public partial class Elevator
    {
        public int Id { get; set; }
        public int Capacity { get; set; }
        public int CurrentFloor { get; set; }
        public Direction CurrentDirection { get; set; }
        // רשימת הקריאות הטעונות במעלית
        public List<Call> LoadedCalls { get; set; } = new List<Call>();
        public HashSet<Direction> FeasibleDirections { get; set; }//כיוונים אפשריים
        public HashSet<int> DropCommitments { get; set; } = new HashSet<int>();
        public float CurrentTime { get; set; }
    }
}
