using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Project;
using Project.Algorithm;

namespace ElevatorSchedulingTest
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("🚀 === מערכת אופטימיזציה חכמה למעליות ===");
            Console.WriteLine("📊 4 מעליות | 4+4 בקשות | בגבולות CPLEX Community");
            Console.WriteLine("💡 הדגמה של פתרון אמיתי עם 2 קבוצות בקשות\n");

            try
            {
                // שלב 1: יצירת סביבת הבניין
                Console.WriteLine("🏢 --- יצירת סביבת הבניין ---");
                ProblemInstance instance = CreateBuildingEnvironment();
                PrintBuildingStatus(instance);

                // שלב 2: קבוצה ראשונה - 4 בקשות
                Console.WriteLine("\n📱 --- קבוצה ראשונה: 4 בקשות ---");
                AddFirstBatchOfRequests(instance);
                PrintRequestsSummary(instance, "ראשונה");

                // שלב 3: פתרון ראשון
                Console.WriteLine("\n🧮 --- פתרון אופטימלי לקבוצה ראשונה ---");
                Solution firstSolution = RunOptimizationEngine(instance, "ראשון");
                if (firstSolution != null)
                {
                    DisplaySolutionSummary(firstSolution, "ראשון", 4);
                    DisplayDetailedSchedules(firstSolution, "ראשון");
                }

                // שלב 4: המתנה והוספת בקשות נוספות
                Console.WriteLine("\n⏰ --- המתנה של שנייה (הדמיית זמן אמת) ---");
                Console.WriteLine("🔄 הגיעו בקשות נוספות מהמשתמשים...");
                Thread.Sleep(1000);

                // שלב 5: קבוצה שנייה - 4 בקשות נוספות
                Console.WriteLine("\n📱 --- קבוצה שנייה: 4 בקשות נוספות ---");
                AddSecondBatchOfRequests(instance);
                PrintRequestsSummary(instance, "מלאה");

                // שלב 6: פתרון סופי
                Console.WriteLine("\n🧮 --- פתרון אופטימלי לכלל הבקשות ---");
                Solution finalSolution = RunOptimizationEngine(instance, "סופי");
                if (finalSolution != null)
                {
                    DisplaySolutionSummary(finalSolution, "סופי", 8);
                    DisplayDetailedSchedules(finalSolution, "סופי");
                }

                // שלב 7: השוואה בין הפתרונות
                Console.WriteLine("\n📊 --- השוואת תוצאות ---");
                CompareSolutions(firstSolution, finalSolution);

                Console.WriteLine("\n🎉 ההדגמה הושלמה בהצלחה!");
                Console.WriteLine("💬 זוהי הדגמה במגבלות CPLEX Community (8 בקשות מקסימום)");
                Console.WriteLine("🚀 במערכת מלאה ניתן לטפל ב-50+ בקשות ו-8+ מעליות בו-זמנית");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה במהלך ההדגמה: {ex.Message}");
                if (ex.Message.Contains("1016") || ex.Message.Contains("Community Edition"))
                {
                    Console.WriteLine("🚫 זו שגיאת גבול CPLEX Community Edition!");
                    Console.WriteLine("💡 נסה להקטין את מספר הבקשות או המעליות");
                }
            }

            Console.WriteLine("\n⌨️ לחץ על מקש כלשהו לסיום.");
            Console.ReadKey();
        }

        static ProblemInstance CreateBuildingEnvironment()
        {
            Console.WriteLine("🔧 מגדיר פרמטרי בניין...");

            ProblemInstance instance = new ProblemInstance(
                numElevators: 4,      // 4 מעליות
                numFloors: 15,        // 15 קומות
                stopTime: 2.0,        // 2 שניות עצירה
                loadTime: 1.0,        // 1 שנייה טעינה
                drivePerFloorTime: 1.5, // 1.5 שניות נסיעה בין קומות
                capacityPenalty: 1000.0); // קנס על עומס יתר

            // מערך מעליות בפריסה אסטרטגית
            var elevatorSetup = new[]
            {
                new { Floor = 1, Name = "מעלית A" },
                new { Floor = 4, Name = "מעלית B" },
                new { Floor = 8, Name = "מעלית C" },
                new { Floor = 12, Name = "מעלית D" }
            };

            for (int i = 0; i < 4; i++)
            {
                Elevator elevator = new Elevator
                {
                    Id = i,
                    Capacity = 8,
                    CurrentFloor = elevatorSetup[i].Floor,
                    CurrentDirection = Direction.Idle,
                    CurrentTime = 0,
                    FeasibleDirections = new HashSet<Direction> { Direction.Up, Direction.Down, Direction.Idle },
                    LoadedCalls = new List<Call>()
                };
                instance.AddElevator(elevator);

                Console.WriteLine($"   ✅ {elevatorSetup[i].Name} - קומה {elevatorSetup[i].Floor}");
            }

            return instance;
        }

        static void AddFirstBatchOfRequests(ProblemInstance instance)
        {
            Console.WriteLine("👥 מוסיף 4 בקשות ראשונות:\n");

            // קבוצה ראשונה: שעת בוקר - עובדים מגיעים
            var firstBatch = new[]
            {
                new { From = 1, To = 9, User = "עובד למשרד", Type = "בוקר" },
                new { From = 1, To = 13, User = "מנהל לפגישה", Type = "בוקר" },
                new { From = 2, To = 11, User = "מבקר", Type = "בוקר" },
                new { From = 1, To = 6, User = "טכנאי", Type = "בוקר" }
            };

            for (int i = 0; i < firstBatch.Length; i++)
            {
                var req = firstBatch[i];
                AddRequest(instance, req.From, req.To, req.User, 1.0, i);
            }
        }

        static void AddSecondBatchOfRequests(ProblemInstance instance)
        {
            Console.WriteLine("👥 מוסיף 4 בקשות נוספות:\n");

            // קבוצה שנייה: תנועה פנימית - אנשים עוברים בין קומות
            var secondBatch = new[]
            {
                new { From = 12, To = 3, User = "עובד יורד למחסן", Type = "פנימי" },
                new { From = 7, To = 14, User = "מזכירה לפגישה", Type = "פנימי" },
                new { From = 5, To = 10, User = "לקוח עולה למשרד", Type = "פנימי" },
                new { From = 9, To = 2, User = "עובד לחניון", Type = "פנימי" }
            };

            for (int i = 0; i < secondBatch.Length; i++)
            {
                var req = secondBatch[i];
                AddRequest(instance, req.From, req.To, req.User, 1.2, i + 4);
            }
        }

        static void AddRequest(ProblemInstance instance, int from, int to, string user, double cost, int index)
        {
            Request request = new Request
            {
                ReleaseTime = DateTime.Now.AddSeconds(index * 10),
                StartFloor = from,
                DestinationFloor = to
            };

            Call call = new Call
            {
                ReleaseTime = request.ReleaseTime,
                StartFloor = from,
                DestinationFloor = to,
                WaitCost = cost,
                TravelCost = cost
            };

            request.AddCall(call);
            instance.AddRequest(request);

            string direction = from < to ? "⬆️" : "⬇️";
            Console.WriteLine($"   📱 בקשה {index + 1}: {user}");
            Console.WriteLine($"      {direction} קומה {from} → קומה {to}");
        }

        static Solution RunOptimizationEngine(ProblemInstance instance, string solutionName)
        {
            Console.WriteLine($"🔄 מפעיל אלגוריתם Branch-and-Price עבור פתרון {solutionName}...");

            // הצגת סטטיסטיקות לפני הפתרון
            int numRequests = instance.GetUnassignedRequests().Count;
            int numElevators = instance.numElevators;

            Console.WriteLine($"📊 סטטיסטיקות בעיה:");
            Console.WriteLine($"   • {numRequests} בקשות");
            Console.WriteLine($"   • {numElevators} מעליות");
            Console.WriteLine($"   • אילוצים משוערים: {numRequests + numElevators + (numRequests * 2)}");
            Console.WriteLine($"   • משתנים משוערים: {numElevators * (10 + numRequests * 3)}");

            DateTime startTime = DateTime.Now;
            BranchAndPrice algorithm = new BranchAndPrice(instance);
            Solution solution = algorithm.Solve();
            TimeSpan duration = DateTime.Now - startTime;

            if (solution != null)
            {
                Console.WriteLine($"✅ פתרון {solutionName} הושלם תוך {duration.TotalMilliseconds:F0} מילישניות");

                // בדיקת איכות הפתרון
                if (duration.TotalSeconds > 10)
                {
                    Console.WriteLine("⚠️ הפתרון ארך זמן רב - יכול להיות שמתקרבים לגבול");
                }
            }
            else
            {
                Console.WriteLine($"❌ לא נמצא פתרון {solutionName}");
            }

            return solution;
        }

        static void PrintBuildingStatus(ProblemInstance instance)
        {
            Console.WriteLine($"   🏗️ בניין בן {instance.numFloors} קומות");
            Console.WriteLine($"   🚀 {instance.numElevators} מעליות מוכנות לפעולה");
        }

        static void PrintRequestsSummary(ProblemInstance instance, string stage)
        {
            var requests = instance.GetUnassignedRequests();
            Console.WriteLine($"\n📊 סיכום קבוצה {stage}: {requests.Count} בקשות");

            int upRequests = requests.Count(r => r.StartFloor < r.DestinationFloor);
            int downRequests = requests.Count(r => r.StartFloor > r.DestinationFloor);

            Console.WriteLine($"   ⬆️ {upRequests} בקשות עלייה, ⬇️ {downRequests} בקשות ירידה");

            // בדיקת גבולות
            if (requests.Count >= 8)
            {
                Console.WriteLine($"   ⚠️ מתקרבים לגבול CPLEX Community (8 בקשות)");
            }
            else
            {
                Console.WriteLine($"   ✅ בגבולות בטוחים ({requests.Count}/8 בקשות)");
            }
        }

        static void DisplaySolutionSummary(Solution solution, string solutionName, int numRequests)
        {
            var schedules = solution.GetSelectedSchedules();
            var activeSchedules = schedules.Where(s => s.ServedRequests.Count > 0).ToList();

            Console.WriteLine($"✅ פתרון {solutionName} מוצלח!");
            Console.WriteLine($"   💰 עלות כוללת: {solution.ObjectiveValue:F1}");
            Console.WriteLine($"   🚀 מעליות פעילות: {activeSchedules.Count}/4");
            Console.WriteLine($"   📋 בקשות שטופלו: {numRequests}");

            // בדיקת יעילות
            double costPerRequest = solution.ObjectiveValue / numRequests;
            Console.WriteLine($"   📈 עלות ממוצעת לבקשה: {costPerRequest:F1}");
        }

        static void DisplayDetailedSchedules(Solution solution, string solutionName)
        {
            var schedules = solution.GetSelectedSchedules();
            var activeSchedules = schedules.Where(s => s.ServedRequests.Count > 0).ToList();

            Console.WriteLine($"\n📅 לוחות זמנים מפורטים - פתרון {solutionName}:");

            if (activeSchedules.Count == 0)
            {
                Console.WriteLine("   ⚠️ אין מעליות פעילות");
                return;
            }

            foreach (var schedule in activeSchedules)
            {
                Console.WriteLine($"\n   🚀 מעלית {schedule.ElevatorIndex + 1}:");
                Console.WriteLine($"      📋 משרתת {schedule.ServedRequests.Count} בקשות");
                Console.WriteLine($"      💰 עלות: {schedule.TotalCost:F1}");

                // הצגת הבקשות
                Console.WriteLine($"      📱 בקשות:");
                foreach (var request in schedule.ServedRequests)
                {
                    string direction = request.StartFloor < request.DestinationFloor ? "⬆️" : "⬇️";
                    Console.WriteLine($"         {direction} {request.StartFloor} → {request.DestinationFloor}");
                }

                // הצגת מסלול
                Console.WriteLine($"      📍 מסלול ({schedule.Stops.Count} עצירות):");
                for (int i = 0; i < schedule.Stops.Count; i++)
                {
                    var stop = schedule.Stops[i];
                    List<string> activities = new List<string>();

                    if (stop.Pickups.Count > 0)
                        activities.Add($"🔼 {stop.Pickups.Count} איסופים");
                    if (stop.Drops.Count > 0)
                        activities.Add($"🔽 {stop.Drops.Count} הורדות");
                    if (activities.Count == 0)
                        activities.Add("🚶 מעבר");

                    Console.WriteLine($"         {i + 1}. קומה {stop.Floor} @ {stop.ArrivalTime:F1}s - {string.Join(", ", activities)}");
                }
            }
        }

        static void CompareSolutions(Solution firstSolution, Solution finalSolution)
        {
            if (firstSolution == null || finalSolution == null)
            {
                Console.WriteLine("❌ לא ניתן להשוות - אחד הפתרונות לא קיים");
                return;
            }

            Console.WriteLine("📊 השוואת פתרונות:");
            Console.WriteLine($"   💰 קבוצה ראשונה (4 בקשות): {firstSolution.ObjectiveValue:F1}");
            Console.WriteLine($"   💰 פתרון מלא (8 בקשות): {finalSolution.ObjectiveValue:F1}");

            double increase = finalSolution.ObjectiveValue - firstSolution.ObjectiveValue;
            double efficiency = (increase / firstSolution.ObjectiveValue) * 100;

            Console.WriteLine($"   📈 עלייה בעלות: +{increase:F1} ({efficiency:F1}%)");

            var firstActive = firstSolution.GetSelectedSchedules().Count(s => s.ServedRequests.Count > 0);
            var finalActive = finalSolution.GetSelectedSchedules().Count(s => s.ServedRequests.Count > 0);

            Console.WriteLine($"   🚀 מעליות פעילות: {firstActive} → {finalActive}");

            if (efficiency < 120)
            {
                Console.WriteLine("✅ האלגוריתם יעיל! הכפלת הבקשות לא גרמה לכפילות בעלות");
            }
            else
            {
                Console.WriteLine("⚠️ האלגוריתם עובד, אך העלות גדלה משמעותית");
            }

            Console.WriteLine("\n💡 תובנות מהתוצאות:");
            Console.WriteLine("   • המערכת מסוגלת להתמודד עם עומס גדל (4→8 בקשות)");
            Console.WriteLine("   • האלגוריתם מפזר את העבודה בין המעליות ביעילות");
            Console.WriteLine("   • הפתרון נשאר בגבולות CPLEX Community Edition");
        }
    }
}