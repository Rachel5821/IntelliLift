using System;
using System.IO;
using System.Linq;
using Project;
using Project.Algorithm;

class ElevatorTimingAnalyzer
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== מנתח זמני בקשות במערכת מעליות ===");
        Console.WriteLine();

        try
        {
            // יצירת מופע בעיה
            var problemInstance = CreateProblemInstance();

            // פתרון הבעיה
            Console.WriteLine("פותר את הבעיה...");
            var exactReplan = new ExactReplan(problemInstance);
            var solution = exactReplan.solve();

            if (solution != null)
            {
                Console.WriteLine($"נמצא פתרון עם עלות כוללת: {solution.ObjectiveValue}");

                // ניתוח זמני הבקשות
                var timingResults = AnalyzeRequestTimings(problemInstance, solution);

                // שמירה לקובץ
                SaveTimingResults(timingResults);

                // הצגה במסך
                DisplayTimingResults(timingResults);
            }
            else
            {
                Console.WriteLine("לא נמצא פתרון אפשרי!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"שגיאה: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("לחץ על מקש כלשהו לסיום.");
        Console.ReadKey();
    }

    static ProblemInstance CreateProblemInstance()
    {
        var problemInstance = new ProblemInstance(
            numElevators: 2,
            numFloors: 10,
            stopTime: 2.0,
            loadTime: 1.0,
            drivePerFloorTime: 1.5,
            capacityPenalty: 100.0
        );

        // יצירת מעליות
        var elevator1 = new Elevator
        {
            Id = 0,
            Capacity = 8,
            CurrentFloor = 1,
            CurrentDirection = Direction.Idle,
            CurrentTime = 0.0f,
            LoadedCalls = new List<Call>()
        };

        var elevator2 = new Elevator
        {
            Id = 1,
            Capacity = 8,
            CurrentFloor = 5,
            CurrentDirection = Direction.Idle,
            CurrentTime = 0.0f,
            LoadedCalls = new List<Call>()
        };

        problemInstance.AddElevator(elevator1);
        problemInstance.AddElevator(elevator2);

        // יצירת בקשות עם זמני רישום שונים
        var baseTime = DateTime.Now;

        var request1 = new Request
        {
            ReleaseTime = baseTime,
            StartFloor = 2,
            DestinationFloor = 7
        };

        var call1 = new Call
        {
            ReleaseTime = baseTime,
            StartFloor = 2,
            DestinationFloor = 7,
            WaitCost = 1.0,
            TravelCost = 1.0
        };
        request1.AddCall(call1);

        var request2 = new Request
        {
            ReleaseTime = baseTime.AddSeconds(1), // בקשה שנייה מאוחרת ב-1 שנייה
            StartFloor = 6,
            DestinationFloor = 3
        };

        var call2 = new Call
        {
            ReleaseTime = baseTime.AddSeconds(1),
            StartFloor = 6,
            DestinationFloor = 3,
            WaitCost = 1.0,
            TravelCost = 1.0
        };
        request2.AddCall(call2);

        // בקשה נוספת לבדיקה
        var request3 = new Request
        {
            ReleaseTime = baseTime.AddSeconds(3),
            StartFloor = 8,
            DestinationFloor = 1
        };

        var call3 = new Call
        {
            ReleaseTime = baseTime.AddSeconds(3),
            StartFloor = 8,
            DestinationFloor = 1,
            WaitCost = 1.0,
            TravelCost = 1.0
        };
        request3.AddCall(call3);

        problemInstance.AddRequest(request1);
        problemInstance.AddRequest(request2);
        problemInstance.AddRequest(request3);

        Console.WriteLine($"נוצרו {problemInstance.GetUnassignedRequests().Count} בקשות");

        return problemInstance;
    }

    public class RequestTimingInfo
    {
        public int RequestId { get; set; }
        public int StartFloor { get; set; }
        public int DestinationFloor { get; set; }
        public DateTime RequestTime { get; set; }
        public int AssignedElevator { get; set; }
        public double PickupTime { get; set; }
        public double DropoffTime { get; set; }
        public double WaitingTime { get; set; }
        public double TravelTime { get; set; }
        public double TotalTime { get; set; }
    }

    static List<RequestTimingInfo> AnalyzeRequestTimings(ProblemInstance problemInstance, Solution solution)
    {
        var timingResults = new List<RequestTimingInfo>();
        var selectedSchedules = solution.GetSelectedSchedules();

        foreach (var schedule in selectedSchedules)
        {
            foreach (var request in schedule.ServedRequests)
            {
                var timingInfo = new RequestTimingInfo
                {
                    RequestId = request.Id,
                    StartFloor = request.StartFloor,
                    DestinationFloor = request.DestinationFloor,
                    RequestTime = request.ReleaseTime,
                    AssignedElevator = schedule.ElevatorIndex
                };

                // מציאת זמן איסוף
                var pickupStop = schedule.Stops.FirstOrDefault(s =>
                    s.Pickups != null && s.Pickups.Any(p => p.Id == request.Id));

                if (pickupStop != null)
                {
                    timingInfo.PickupTime = pickupStop.ArrivalTime;
                }

                // מציאת זמן הורדה
                var dropoffStop = schedule.Stops.FirstOrDefault(s =>
                    s.Drops != null && s.Drops.Any(d =>
                        request.Calls.Any(c => c.DestinationFloor == d.DestinationFloor)));

                if (dropoffStop != null)
                {
                    timingInfo.DropoffTime = dropoffStop.ArrivalTime;
                }

                // חישוב זמנים
                double requestTimeSeconds = request.ReleaseTime.ToOADate() * 24 * 60 * 60;

                timingInfo.WaitingTime = timingInfo.PickupTime - requestTimeSeconds;
                timingInfo.TravelTime = timingInfo.DropoffTime - timingInfo.PickupTime;
                timingInfo.TotalTime = timingInfo.DropoffTime - requestTimeSeconds;

                // ווידוא שהזמנים לא שליליים
                timingInfo.WaitingTime = Math.Max(0, timingInfo.WaitingTime);
                timingInfo.TravelTime = Math.Max(0, timingInfo.TravelTime);
                timingInfo.TotalTime = Math.Max(0, timingInfo.TotalTime);

                timingResults.Add(timingInfo);
            }
        }

        return timingResults;
    }

    static void SaveTimingResults(List<RequestTimingInfo> timingResults)
    {
        string fileName = $"request_timings_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

        try
        {
            using (var writer = new StreamWriter(fileName, false, System.Text.Encoding.UTF8))
            {
                writer.WriteLine("RequestId,StartFloor,DestinationFloor,RequestTime,AssignedElevator,PickupTime,DropoffTime,WaitingTime,TravelTime,TotalTime");

                foreach (var timing in timingResults)
                {
                    writer.WriteLine($"{timing.RequestId}," +
                                   $"{timing.StartFloor}," +
                                   $"{timing.DestinationFloor}," +
                                   $"{timing.RequestTime:yyyy-MM-dd HH:mm:ss.fff}," +
                                   $"{timing.AssignedElevator}," +
                                   $"{timing.PickupTime:F2}," +
                                   $"{timing.DropoffTime:F2}," +
                                   $"{timing.WaitingTime:F2}," +
                                   $"{timing.TravelTime:F2}," +
                                   $"{timing.TotalTime:F2}");
                }
            }

            string fullCsvPath = Path.GetFullPath(fileName);
            Console.WriteLine($"תוצאות נשמרו בקובץ: {fullCsvPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"שגיאה בשמירת הקובץ: {ex.Message}");
        }

        string textFileName = $"request_timings_readable_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

        try
        {
            using (var writer = new StreamWriter(textFileName, false, System.Text.Encoding.UTF8))
            {
                writer.WriteLine("=== דוח זמני בקשות במערכת מעליות ===");
                writer.WriteLine($"נוצר בתאריך: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine();

                foreach (var timing in timingResults)
                {
                    writer.WriteLine($"בקשה מספר: {timing.RequestId}");
                    writer.WriteLine($"מסלול: קומה {timing.StartFloor} → קומה {timing.DestinationFloor}");
                    writer.WriteLine($"זמן רישום: {timing.RequestTime:yyyy-MM-dd HH:mm:ss.fff}");
                    writer.WriteLine($"מעלית משויכת: {timing.AssignedElevator}");
                    writer.WriteLine($"זמן איסוף: {timing.PickupTime:F2} שניות");
                    writer.WriteLine($"זמן הורדה: {timing.DropoffTime:F2} שניות");
                    writer.WriteLine($"זמן המתנה: {timing.WaitingTime:F2} שניות");
                    writer.WriteLine($"זמן נסיעה: {timing.TravelTime:F2} שניות");
                    writer.WriteLine($"זמן כולל: {timing.TotalTime:F2} שניות");
                    writer.WriteLine(new string('-', 50));
                }

                if (timingResults.Count > 0)
                {
                    writer.WriteLine();
                    writer.WriteLine("=== סטטיסטיקות ===");
                    writer.WriteLine($"סה\"כ בקשות: {timingResults.Count}");
                    writer.WriteLine($"זמן המתנה ממוצע: {timingResults.Average(t => t.WaitingTime):F2} שניות");
                    writer.WriteLine($"זמן נסיעה ממוצע: {timingResults.Average(t => t.TravelTime):F2} שניות");
                    writer.WriteLine($"זמן כולל ממוצע: {timingResults.Average(t => t.TotalTime):F2} שניות");
                    writer.WriteLine($"זמן המתנה מקסימלי: {timingResults.Max(t => t.WaitingTime):F2} שניות");
                    writer.WriteLine($"זמן נסיעה מקסימלי: {timingResults.Max(t => t.TravelTime):F2} שניות");
                }
            }

            string fullTextPath = Path.GetFullPath(textFileName);
            Console.WriteLine($"דוח טקסט נשמר בקובץ: {fullTextPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"שגיאה בשמירת דוח הטקסט: {ex.Message}");
        }
    }

    static void DisplayTimingResults(List<RequestTimingInfo> timingResults)
    {
        Console.WriteLine();
        Console.WriteLine("=== תוצאות זמני הבקשות ===");
        Console.WriteLine();

        foreach (var timing in timingResults)
        {
            Console.WriteLine($"בקשה {timing.RequestId} (קומה {timing.StartFloor} → {timing.DestinationFloor}):");
            Console.WriteLine($"  מעלית: {timing.AssignedElevator}");
            Console.WriteLine($"  זמן המתנה: {timing.WaitingTime:F2} שניות");
            Console.WriteLine($"  זמן נסיעה: {timing.TravelTime:F2} שניות");
            Console.WriteLine($"  זמן כולל: {timing.TotalTime:F2} שניות");
            Console.WriteLine();
        }

        if (timingResults.Count > 0)
        {
            Console.WriteLine("=== סטטיסטיקות ===");
            Console.WriteLine($"זמן המתנה ממוצע: {timingResults.Average(t => t.WaitingTime):F2} שניות");
            Console.WriteLine($"זמן נסיעה ממוצע: {timingResults.Average(t => t.TravelTime):F2} שניות");
            Console.WriteLine($"זמן כולל ממוצע: {timingResults.Average(t => t.TotalTime):F2} שניות");
        }
    }
}