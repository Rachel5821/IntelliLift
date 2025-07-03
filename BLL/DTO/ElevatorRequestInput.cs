using System; // נחוץ עבור DateTime

namespace Project.DTO // <<<< שינוי כאן ל-Project.DTO
{
    public class ElevatorRequestInput
    {
        public DateTime ReleaseTime { get; set; }
        public int StartFloor { get; set; }
        public int DestinationFloor { get; set; }
    }
}