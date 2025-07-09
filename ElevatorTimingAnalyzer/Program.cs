using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Project;
using Project.Algorithm;

class ElevatorTimingAnalyzer
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== מנתח זמני בקשות במערכת מעליות - 3 בקשות ===");
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

                // ניתוח זמני הבקשות - גרסה מתוקנת
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
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
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

        // יצירת 3 בקשות עם זמני רישום שונים
        var baseTime = DateTime.Now;

        // בקשה 1: קומה 2 → 7
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
            TravelCost = 1000.0
        };
        request1.AddCall(call1);

        // בקשה 2: קומה 6 → 3 (מאוחרת ב-1 שנייה)
        var request2 = new Request
        {
            ReleaseTime = baseTime.AddSeconds(1),
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

        // בקשה 3: קומה 8 → 1 (מאוחרת ב-3 שניות)
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

        // הוספת כל 3 הבקשות
        problemInstance.AddRequest(request1);
        problemInstance.AddRequest(request2);
        problemInstance.AddRequest(request3);

        Console.WriteLine($"נוצרו {problemInstance.GetUnassignedRequests().Count} בקשות");

        // הדפסת פרטי הבקשות
        var requests = problemInstance.GetUnassignedRequests();
        for (int i = 0; i < requests.Count; i++)
        {
            var req = requests[i];
            Console.WriteLine($"בקשה {req.Id}: קומה {req.StartFloor} → {req.DestinationFloor} (זמן: {req.ReleaseTime:HH:mm:ss.fff})");
        }

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
        public bool HasValidTiming { get; set; }
    }

    static List<RequestTimingInfo> AnalyzeRequestTimings(ProblemInstance problemInstance, Solution solution)
    {
        var timingResults = new List<RequestTimingInfo>();
        var selectedSchedules = solution.GetSelectedSchedules();

        Console.WriteLine();
        Console.WriteLine("=== ניתוח פרטי של הפתרון ===");
        Console.WriteLine($"מספר לוחות זמנים: {selectedSchedules.Count}");

        // זמן בסיס לחישובים יחסיים
        var baseTime = DateTime.Now;

        foreach (var schedule in selectedSchedules)
        {
            Console.WriteLine($"\nמעלית {schedule.ElevatorIndex}:");
            Console.WriteLine($"  בקשות משורתות: {schedule.ServedRequests.Count}");
            Console.WriteLine($"  עצירות: {schedule.Stops.Count}");

            // הצגת עצירות
            if (schedule.Stops.Count > 0)
            {
                Console.WriteLine("  עצירות:");
                for (int i = 0; i < schedule.Stops.Count; i++)
                {
                    var stop = schedule.Stops[i];
                    Console.WriteLine($"    {i + 1}. קומה {stop.Floor}, זמן {stop.ArrivalTime:F1}");

                    if (stop.Pickups != null && stop.Pickups.Count > 0)
                    {
                        foreach (var pickup in stop.Pickups)
                        {
                            Console.WriteLine($"        → איסוף בקשה {pickup.Id}");
                        }
                    }

                    if (stop.Drops != null && stop.Drops.Count > 0)
                    {
                        foreach (var drop in stop.Drops)
                        {
                            Console.WriteLine($"        ← הורדה לקומה {drop.DestinationFloor}");
                        }
                    }
                }
            }

            foreach (var request in schedule.ServedRequests)
            {
                var timingInfo = new RequestTimingInfo
                {
                    RequestId = request.Id,
                    StartFloor = request.StartFloor,
                    DestinationFloor = request.DestinationFloor,
                    RequestTime = request.ReleaseTime,
                    AssignedElevator = schedule.ElevatorIndex,
                    HasValidTiming = false
                };

                Console.WriteLine($"  מעבד בקשה {request.Id} (קומה {request.StartFloor} → {request.DestinationFloor}):");

                // מציאת זמן איסוף
                Stop pickupStop = null;
                foreach (var stop in schedule.Stops)
                {
                    if (stop.Pickups != null)
                    {
                        foreach (var pickup in stop.Pickups)
                        {
                            if (pickup.Id == request.Id)
                            {
                                pickupStop = stop;
                                Console.WriteLine($"    ✓ נמצאה עצירת איסוף בקומה {stop.Floor}, זמן {stop.ArrivalTime:F1}");
                                break;
                            }
                        }
                    }
                    if (pickupStop != null) break;
                }

                // מציאת זמן הורדה
                Stop dropoffStop = null;
                foreach (var stop in schedule.Stops)
                {
                    if (stop.Drops != null)
                    {
                        foreach (var drop in stop.Drops)
                        {
                            // בדיקה אם הקריאה שייכת לבקשה הנוכחית
                            bool belongsToCurrentRequest = false;
                            foreach (var call in request.Calls)
                            {
                                if (call.DestinationFloor == drop.DestinationFloor)
                                {
                                    belongsToCurrentRequest = true;
                                    Console.WriteLine($"    ✓ נמצאה עצירת הורדה בקומה {stop.Floor}, זמן {stop.ArrivalTime:F1}");
                                    break;
                                }
                            }

                            if (belongsToCurrentRequest)
                            {
                                dropoffStop = stop;
                                break;
                            }
                        }
                    }
                    if (dropoffStop != null) break;
                }

                // חישוב זמנים
                if (pickupStop != null && dropoffStop != null)
                {
                    timingInfo.PickupTime = pickupStop.ArrivalTime;
                    timingInfo.DropoffTime = dropoffStop.ArrivalTime;

                    // חישוב זמן הבקשה יחסית לזמן הבסיס
                    double requestTimeInSeconds = (request.ReleaseTime - baseTime).TotalSeconds;

                    // חישוב זמנים יחסיים
                    timingInfo.WaitingTime = Math.Max(0, timingInfo.PickupTime - requestTimeInSeconds);
                    timingInfo.TravelTime = Math.Max(0, timingInfo.DropoffTime - timingInfo.PickupTime);
                    timingInfo.TotalTime = Math.Max(0, timingInfo.DropoffTime - requestTimeInSeconds);

                    timingInfo.HasValidTiming = true;

                    Console.WriteLine($"    ✓ זמן המתנה: {timingInfo.WaitingTime:F1} שניות");
                    Console.WriteLine($"    ✓ זמן נסיעה: {timingInfo.TravelTime:F1} שניות");
                    Console.WriteLine($"    ✓ זמן כולל: {timingInfo.TotalTime:F1} שניות");
                }
                else
                {
                    Console.WriteLine($"    ✗ לא נמצאו עצירות מתאימות!");
                    if (pickupStop == null) Console.WriteLine($"      - חסרה עצירת איסוף");
                    if (dropoffStop == null) Console.WriteLine($"      - חסרה עצירת הורדה");

                    timingInfo.PickupTime = 0;
                    timingInfo.DropoffTime = 0;
                    timingInfo.WaitingTime = 0;
                    timingInfo.TravelTime = 0;
                    timingInfo.TotalTime = 0;
                    timingInfo.HasValidTiming = false;
                }

                timingResults.Add(timingInfo);
            }
        }

        return timingResults;
    }

    static void SaveTimingResults(List<RequestTimingInfo> timingResults)
    {
        string fileName = $"request_timings_3req_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

        try
        {
            using (var writer = new StreamWriter(fileName, false, System.Text.Encoding.UTF8))
            {
                writer.WriteLine("RequestId,StartFloor,DestinationFloor,RequestTime,AssignedElevator,PickupTime,DropoffTime,WaitingTime,TravelTime,TotalTime,HasValidTiming");

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
                                   $"{timing.TotalTime:F2}," +
                                   $"{timing.HasValidTiming}");
                }
            }

            string fullCsvPath = Path.GetFullPath(fileName);
            Console.WriteLine($"תוצאות נשמרו בקובץ: {fullCsvPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"שגיאה בשמירת הקובץ: {ex.Message}");
        }

        string textFileName = $"request_timings_3req_readable_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

        try
        {
            using (var writer = new StreamWriter(textFileName, false, System.Text.Encoding.UTF8))
            {
                writer.WriteLine("=== דוח זמני בקשות במערכת מעליות - 3 בקשות ===");
                writer.WriteLine($"נוצר בתאריך: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine();

                var validTimings = timingResults.Where(t => t.HasValidTiming).ToList();
                var invalidTimings = timingResults.Where(t => !t.HasValidTiming).ToList();

                writer.WriteLine("=== בקשות עם זמנים תקינים ===");
                foreach (var timing in validTimings)
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

                if (invalidTimings.Count > 0)
                {
                    writer.WriteLine();
                    writer.WriteLine("=== בקשות עם בעיות בזמנים ===");
                    foreach (var timing in invalidTimings)
                    {
                        writer.WriteLine($"בקשה מספר: {timing.RequestId} (קומה {timing.StartFloor} → {timing.DestinationFloor})");
                        writer.WriteLine($"מעלית משויכת: {timing.AssignedElevator}");
                        writer.WriteLine("לא נמצאו עצירות איסוף או הורדה מתאימות!");
                        writer.WriteLine(new string('-', 50));
                    }
                }

                if (validTimings.Count > 0)
                {
                    writer.WriteLine();
                    writer.WriteLine("=== סטטיסטיקות (בקשות תקינות בלבד) ===");
                    writer.WriteLine($"סה\"כ בקשות תקינות: {validTimings.Count}");
                    writer.WriteLine($"סה\"כ בקשות לא תקינות: {invalidTimings.Count}");
                    writer.WriteLine($"זמן המתנה ממוצע: {validTimings.Average(t => t.WaitingTime):F2} שניות");
                    writer.WriteLine($"זמן נסיעה ממוצע: {validTimings.Average(t => t.TravelTime):F2} שניות");
                    writer.WriteLine($"זמן כולל ממוצע: {validTimings.Average(t => t.TotalTime):F2} שניות");
                    writer.WriteLine($"זמן המתנה מקסימלי: {validTimings.Max(t => t.WaitingTime):F2} שניות");
                    writer.WriteLine($"זמן נסיעה מקסימלי: {validTimings.Max(t => t.TravelTime):F2} שניות");
                    writer.WriteLine($"זמן המתנה מינימלי: {validTimings.Min(t => t.WaitingTime):F2} שניות");
                    writer.WriteLine($"זמן נסיעה מינימלי: {validTimings.Min(t => t.TravelTime):F2} שניות");

                    // סטטיסטיקות נוספות עבור 3 בקשות
                    writer.WriteLine();
                    writer.WriteLine("=== סטטיסטיקות מפורטות ===");
                    var sortedByWaitTime = validTimings.OrderBy(t => t.WaitingTime).ToList();
                    var sortedByTotalTime = validTimings.OrderBy(t => t.TotalTime).ToList();

                    writer.WriteLine($"בקשה עם זמן המתנה הקצר ביותר: בקשה {sortedByWaitTime.First().RequestId} ({sortedByWaitTime.First().WaitingTime:F2}s)");
                    writer.WriteLine($"בקשה עם זמן המתנה הארוך ביותר: בקשה {sortedByWaitTime.Last().RequestId} ({sortedByWaitTime.Last().WaitingTime:F2}s)");
                    writer.WriteLine($"בקשה עם זמן כולל הקצר ביותר: בקשה {sortedByTotalTime.First().RequestId} ({sortedByTotalTime.First().TotalTime:F2}s)");
                    writer.WriteLine($"בקשה עם זמן כולל הארוך ביותר: בקשה {sortedByTotalTime.Last().RequestId} ({sortedByTotalTime.Last().TotalTime:F2}s)");

                    writer.WriteLine();
                    writer.WriteLine("=== התפלגות עומס המעליות ===");
                    var elevatorLoads = validTimings.GroupBy(t => t.AssignedElevator)
                                                  .Select(g => new { Elevator = g.Key, Count = g.Count() })
                                                  .ToList();

                    foreach (var load in elevatorLoads)
                    {
                        writer.WriteLine($"מעלית {load.Elevator}: {load.Count} בקשות ({load.Count * 100.0 / validTimings.Count:F1}%)");
                    }
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
        Console.WriteLine("=== תוצאות זמני הבקשות (3 בקשות) ===");
        Console.WriteLine();

        var validTimings = timingResults.Where(t => t.HasValidTiming).ToList();
        var invalidTimings = timingResults.Where(t => !t.HasValidTiming).ToList();

        foreach (var timing in validTimings.OrderBy(t => t.RequestId))
        {
            Console.WriteLine($"✓ בקשה {timing.RequestId} (קומה {timing.StartFloor} → {timing.DestinationFloor}):");
            Console.WriteLine($"  מעלית: {timing.AssignedElevator}");
            Console.WriteLine($"  זמן המתנה: {timing.WaitingTime:F2} שניות");
            Console.WriteLine($"  זמן נסיעה: {timing.TravelTime:F2} שניות");
            Console.WriteLine($"  זמן כולל: {timing.TotalTime:F2} שניות");
            Console.WriteLine();
        }

        if (invalidTimings.Count > 0)
        {
            Console.WriteLine("=== בקשות עם בעיות ===");
            foreach (var timing in invalidTimings)
            {
                Console.WriteLine($"✗ בקשה {timing.RequestId}: לא נמצאו זמנים תקינים");
            }
            Console.WriteLine();
        }

        if (validTimings.Count > 0)
        {
            Console.WriteLine("=== סטטיסטיקות כלליות ===");
            Console.WriteLine($"בקשות תקינות: {validTimings.Count}/{timingResults.Count}");
            Console.WriteLine($"זמן המתנה ממוצע: {validTimings.Average(t => t.WaitingTime):F2} שניות");
            Console.WriteLine($"זמן נסיעה ממוצע: {validTimings.Average(t => t.TravelTime):F2} שניות");
            Console.WriteLine($"זמן כולל ממוצע: {validTimings.Average(t => t.TotalTime):F2} שניות");

            Console.WriteLine();
            Console.WriteLine("=== התפלגות עומס המעליות ===");
            var elevatorLoads = validTimings.GroupBy(t => t.AssignedElevator)
                                          .Select(g => new { Elevator = g.Key, Count = g.Count() })
                                          .OrderBy(e => e.Elevator)
                                          .ToList();

            foreach (var load in elevatorLoads)
            {
                double percentage = load.Count * 100.0 / validTimings.Count;
                Console.WriteLine($"מעלית {load.Elevator}: {load.Count} בקשות ({percentage:F1}%)");
            }
        }
        else
        {
            Console.WriteLine("⚠️ לא נמצאו בקשות עם זמנים תקינים!");
        }
    }
}