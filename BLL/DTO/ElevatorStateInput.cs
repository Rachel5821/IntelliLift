using System.ComponentModel.DataAnnotations; // נחוץ עבור [Required] אם תרצה
using Project; 

namespace Project.DTO // <<<< שינוי כאן ל-Project.DTO
{
    public class ElevatorStateInput
    {
        // [Required] // אתה יכול להוסיף את זה אם ID הוא שדה חובה בקלט מהלקוח
        public int Id { get; set; }

        public int Capacity { get; set; } = 8;
        public int CurrentFloor { get; set; } = 1;
        public Direction CurrentDirection { get; set; } = Direction.Idle; // משתמש ב-enum הקיים שלך
        public double CurrentTime { get; set; } = 0;
    }
}