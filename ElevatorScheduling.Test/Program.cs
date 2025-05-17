using System;
using System.Collections.Generic;
using System.Linq;
using Model;
using Project.Algorithm;

namespace ElevatorSchedulingTest
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== תוכנית בדיקה למערכת תזמון מעליות ===");

            try
            {
                // שלב 1: יצירת מופע בעיה פשוט
                Console.WriteLine("\n--- שלב 1: יצירת מופע בעיה ---");
                ProblemInstance instance = CreateSimpleProblemInstance();
                PrintProblemInstanceDetails(instance);

                // שלב 2: בדיקת המודל הראשי
                Console.WriteLine("\n--- שלב 2: בדיקת המודל הראשי ---");
                TestMasterModel(instance);

                // שלב 3: בדיקת בעיית התמחור
                Console.WriteLine("\n--- שלב 3: בדיקת בעיית התמחור ---");
                TestPricingProblem(instance);

                // שלב 4: בדיקת Branch-and-Price בסיסי
                Console.WriteLine("\n--- שלב 4: בדיקת אלגוריתם Branch-and-Price ---");
                TestBasicBranchAndPrice(instance);

                Console.WriteLine("\nכל הבדיקות הושלמו בהצלחה!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"שגיאה: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("\nלחץ על מקש כלשהו לסיום.");
            Console.ReadKey();
        }

        static ProblemInstance CreateSimpleProblemInstance()
        {
            // יצירת מופע בעיה עם 2 מעליות
            ProblemInstance instance = new ProblemInstance(
                numElevators: 2,
                numFloors: 20,
                stopTime: 2.0,
                loadTime: 1.0,
                drivePerFloorTime: 1.5,
                capacityPenalty: 100.0);

            Console.WriteLine("יצירת מעליות...");

            // מעלית 1 - בקומת הכניסה
            Elevator elevator1 = new Elevator
            {
                Id = 0,
                Capacity = 8,
                CurrentFloor = 1,
                CurrentDirection = Direction.Idle,
                CurrentTime = 0,
                FeasibleDirections = new HashSet<Direction> { Direction.Up, Direction.Down, Direction.Idle },
                LoadedCalls = new List<Call>()
            };
            instance.AddElevator(elevator1);
            Console.WriteLine("נוצרה מעלית 1 בקומה 1");

            // מעלית 2 - בקומה 5
            Elevator elevator2 = new Elevator
            {
                Id = 1,
                Capacity = 8,
                CurrentFloor = 5,
                CurrentDirection = Direction.Idle,
                CurrentTime = 0,
                FeasibleDirections = new HashSet<Direction> { Direction.Up, Direction.Down, Direction.Idle },
                LoadedCalls = new List<Call>()
            };
            instance.AddElevator(elevator2);
            Console.WriteLine("נוצרה מעלית 2 בקומה 5");

            Console.WriteLine("יצירת בקשות...");

            // בקשה 1: מקומה 2 לקומה 7
            Request request1 = new Request
            {
                ReleaseTime = DateTime.Now,
                StartFloor = 2,
                DestinationFloor = 7
            };
            Call call1 = new Call
            {
                ReleaseTime = DateTime.Now,
                StartFloor = 2,
                DestinationFloor = 7,
                WaitCost = 1.0,
                TravelCost = 1.0
            };
            request1.AddCall(call1);
            instance.AddRequest(request1);
            Console.WriteLine("נוצרה בקשה 1: מקומה 2 לקומה 7");

            // בקשה 2: מקומה 6 לקומה 3
            Request request2 = new Request
            {
                ReleaseTime = DateTime.Now,
                StartFloor = 6,
                DestinationFloor = 3
            };
            Call call2 = new Call
            {
                ReleaseTime = DateTime.Now,
                StartFloor = 6,
                DestinationFloor = 3,
                WaitCost = 1.0,
                TravelCost = 1.0
            };
            request2.AddCall(call2);
            instance.AddRequest(request2);
            Console.WriteLine("נוצרה בקשה 2: מקומה 6 לקומה 3");

            return instance;
        }

        static void PrintProblemInstanceDetails(ProblemInstance instance)
        {
            Console.WriteLine("\nפרטי מופע הבעיה:");
            Console.WriteLine($"מספר מעליות: {instance.numElevators}");
            Console.WriteLine($"מספר בקשות לא משויכות: {instance.GetUnassignedRequests().Count}");

            Console.WriteLine("\nמעליות:");
            foreach (var elevator in instance.GetElevators())
            {
                Console.WriteLine($"  מעלית {elevator.Id}: קומה נוכחית {elevator.CurrentFloor}, קיבולת {elevator.Capacity}");
            }

            Console.WriteLine("\nבקשות:");
            foreach (var request in instance.GetUnassignedRequests())
            {
                Console.WriteLine($"  בקשה {request.Id}: מקומה {request.StartFloor} לקומה {request.DestinationFloor}, מספר קריאות: {request.Calls.Count}");
            }
        }

        static void TestMasterModel(ProblemInstance instance)
        {
            // יצירת מודל ראשי
            MasterModel masterModel = new MasterModel(instance);
            Console.WriteLine("נוצר מודל ראשי");

            // יצירת לוחות זמנים פשוטים
            Console.WriteLine("יצירת לוחות זמנים פשוטים למעליות...");
            for (int e = 0; e < instance.numElevators; e++)
            {
                Schedule schedule = CreateSimpleSchedule(instance, e);
                masterModel.AddSchedule(schedule, e);
                Console.WriteLine($"נוסף לוח זמנים למעלית {e}");
            }

            // פתרון המודל
            Console.WriteLine("פתרון המודל הראשי...");
            Solution solution = masterModel.Solve();

            if (solution != null)
            {
                Console.WriteLine($"נמצא פתרון עם ערך: {solution.ObjectiveValue}");
                Console.WriteLine($"הפתרון אינטגרלי: {solution.isIntegral}");

                List<Schedule> selectedSchedules = solution.GetSelectedSchedules();
                Console.WriteLine($"מספר לוחות זמנים בפתרון: {selectedSchedules.Count}");

                // בדיקת הקצאת בקשות
                CheckRequestAssignment(solution, instance);
            }
            else
            {
                Console.WriteLine("לא נמצא פתרון למודל הראשי");
            }
        }

        static Schedule CreateSimpleSchedule(ProblemInstance instance, int elevatorIndex)
        {
            Schedule schedule = new Schedule(elevatorIndex);
            Elevator elevator = instance.GetElevators()[elevatorIndex];

            // עצירה ראשונה במיקום הנוכחי
            Stop initialStop = new Stop
            {
                Floor = elevator.CurrentFloor,
                Direction = Direction.Idle,
                ArrivalTime = 0
            };
            schedule.AddStop(initialStop);

            // הקצאת בקשות למעליות לפי אינדקס
            List<Request> requests = instance.GetUnassignedRequests();
            if (requests.Count > elevatorIndex)
            {
                Request request = requests[elevatorIndex];

                // עצירה לאיסוף
                Stop pickupStop = new Stop
                {
                    Floor = request.StartFloor,
                    Direction = request.StartFloor < request.DestinationFloor ? Direction.Up : Direction.Down,
                    ArrivalTime = 5.0f
                };
                pickupStop.AddPickup(request);
                schedule.AddStop(pickupStop);

                // עצירה להורדה
                Stop dropStop = new Stop
                {
                    Floor = request.DestinationFloor,
                    Direction = Direction.Idle,
                    ArrivalTime = 10.0f
                };
                foreach (Call call in request.Calls)
                {
                    dropStop.AddDrop(call);
                }
                schedule.AddStop(dropStop);

                // הוספת הבקשה לרשימת הבקשות המטופלות
                schedule.ServedRequests.Add(request);

                // חישוב עלות פשוט
                schedule.TotalCost = CalculateScheduleCost(schedule, elevator, request);
            }
            else
            {
                // לוח ריק במקרה שאין מספיק בקשות
                schedule.TotalCost = 0;
            }

            return schedule;
        }

        static float CalculateScheduleCost(Schedule schedule, Elevator elevator, Request request)
        {
            // חישוב פשוט של עלות על פי זמני המתנה ונסיעה
            float waitCost = 0;
            float travelCost = 0;

            // מציאת זמני האיסוף וההורדה
            float pickupTime = 0;
            float dropTime = 0;

            foreach (var stop in schedule.Stops)
            {
                if (stop.Floor == request.StartFloor)
                {
                    pickupTime = stop.ArrivalTime;
                }

                if (stop.Floor == request.DestinationFloor)
                {
                    dropTime = stop.ArrivalTime;
                }
            }

            // חישוב עלויות
            foreach (var call in request.Calls)
            {
                double waitTime = Math.Max(0, pickupTime - call.ReleaseTime.ToOADate());
                waitCost += (float)(call.WaitCost * waitTime);

                float travelTime = dropTime - pickupTime;
                travelCost += (float)(call.TravelCost * travelTime);
            }

            return waitCost + travelCost;
        }

        static void CheckRequestAssignment(Solution solution, ProblemInstance instance)
        {
            List<Schedule> schedules = solution.GetSelectedSchedules();
            HashSet<int> assignedRequestIds = new HashSet<int>();

            foreach (var schedule in schedules)
            {
                foreach (var request in schedule.ServedRequests)
                {
                    assignedRequestIds.Add(request.Id);
                }
            }

            int totalRequests = instance.GetUnassignedRequests().Count;
            Console.WriteLine($"בקשות שהוקצו: {assignedRequestIds.Count} מתוך {totalRequests}");

            if (assignedRequestIds.Count != totalRequests)
            {
                Console.WriteLine("אזהרה: לא כל הבקשות הוקצו");

                // הצגת בקשות שלא הוקצו
                foreach (var request in instance.GetUnassignedRequests())
                {
                    if (!assignedRequestIds.Contains(request.Id))
                    {
                        Console.WriteLine($"  בקשה {request.Id} לא הוקצתה");
                    }
                }
            }
        }

        static void TestPricingProblem(ProblemInstance instance)
        {
            Console.WriteLine("בדיקת בעיית התמחור לכל מעלית...");

            // יצירת מודל ראשי ופתרון ראשוני לקבלת ערכים דואליים
            MasterModel masterModel = new MasterModel(instance);
            for (int e = 0; e < instance.numElevators; e++)
            {
                Schedule schedule = CreateSimpleSchedule(instance, e);
                masterModel.AddSchedule(schedule, e);
            }

            Solution initialSolution = masterModel.Solve();
            if (initialSolution == null)
            {
                Console.WriteLine("לא ניתן לבדוק את בעיית התמחור - לא נמצא פתרון ראשוני");
                return;
            }

            double[] requestDuals = initialSolution.GetRequestDuals();
            double[] elevatorDuals = initialSolution.GetElevatorsDuals();

            Console.WriteLine("ערכים דואליים של בקשות:");
            for (int i = 0; i < requestDuals.Length; i++)
            {
                Console.WriteLine($"  בקשה {i}: {requestDuals[i]}");
            }

            Console.WriteLine("ערכים דואליים של מעליות:");
            for (int i = 0; i < elevatorDuals.Length; i++)
            {
                Console.WriteLine($"  מעלית {i}: {elevatorDuals[i]}");
            }

            // בדיקת בעיית התמחור לכל מעלית
            for (int e = 0; e < instance.numElevators; e++)
            {
                Console.WriteLine($"\nבדיקת בעיית התמחור למעלית {e}:");

                PricingProblem pricingProblem = new PricingProblem(
                    instance, e, requestDuals, elevatorDuals[e], 5);

                List<Schedule> schedules = pricingProblem.GenerateSchedulesWithNegativeReducedCost();

                Console.WriteLine($"  נמצאו {schedules.Count} לוחות זמנים עם עלות מופחתת שלילית");

                // הצגת פרטים על הלוחות שנמצאו
                for (int i = 0; i < schedules.Count; i++)
                {
                    Schedule schedule = schedules[i];
                    Console.WriteLine($"  לוח זמנים {i + 1}:");
                    Console.WriteLine($"    מספר עצירות: {schedule.Stops.Count}");
                    Console.WriteLine($"    מספר בקשות: {schedule.ServedRequests.Count}");
                    Console.WriteLine($"    עלות: {schedule.TotalCost}");
                }
            }
        }

        static void TestBasicBranchAndPrice(ProblemInstance instance)
        {
            Console.WriteLine("הפעלת אלגוריתם Branch-and-Price...");

            // יצירת ופתרון אלגוריתם Branch-and-Price
            BranchAndPrice algorithm = new BranchAndPrice(instance);

            DateTime startTime = DateTime.Now;
            Solution solution = algorithm.Solve();
            TimeSpan duration = DateTime.Now - startTime;

            if (solution != null)
            {
                Console.WriteLine($"נמצא פתרון עם ערך: {solution.ObjectiveValue}");
                Console.WriteLine($"זמן פתרון: {duration.TotalSeconds:F2} שניות");

                List<Schedule> schedules = solution.GetSelectedSchedules();
                Console.WriteLine($"מספר לוחות זמנים בפתרון: {schedules.Count}");

                // בדיקת הקצאת בקשות
                CheckRequestAssignment(solution, instance);

                // פרטים נוספים על לוחות הזמנים בפתרון
                Console.WriteLine("\nפרטי לוחות הזמנים בפתרון:");
                foreach (var schedule in schedules)
                {
                    Console.WriteLine($"  לוח למעלית {schedule.ElevatorIndex}:");
                    Console.WriteLine($"    מספר עצירות: {schedule.Stops.Count}");
                    Console.WriteLine($"    מספר בקשות: {schedule.ServedRequests.Count}");
                    Console.WriteLine($"    עלות: {schedule.TotalCost}");

                    Console.WriteLine("    עצירות:");
                    foreach (var stop in schedule.Stops)
                    {
                        Console.WriteLine($"      קומה {stop.Floor}, זמן: {stop.ArrivalTime}, איסופים: {stop.Pickups.Count}, הורדות: {stop.Drops.Count}");
                    }
                }
            }
            else
            {
                Console.WriteLine("לא נמצא פתרון באמצעות אלגוריתם Branch-and-Price");
            }
        }
    }
}