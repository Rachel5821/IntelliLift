using Microsoft.VisualBasic;
using Project;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Algorithm
{
    /// <summary>
    /// מייצג צומת בעץ החיפוש של בעיית ה-Pricing כפי שמתואר במאמר בסעיף 3.1
    /// </summary>
    public class PricingNode
    {
        // שדות המתאימים למודל במאמר
        public int CurrentFloor { get; private set; }
        public double CurrentTime { get; private set; }
        public int CurrentLoad { get; private set; }

        // Av במאמר - בקשות משויכות שטרם נאספו
        public List<Request> UnservedAssignedRequests { get; private set; }

        // Ov במאמר - בקשות אופציונליות (לא משויכות) שטרם נאספו
        public List<Request> UnservedOptionalRequests { get; private set; }

        // בקשות שכבר נאספו (R(e) \ Av) - משויכות
        public HashSet<Request> ServedAssignedRequests { get; private set; }

        // בקשות אופציונליות שכבר נאספו (תת-קבוצה של Ru)
        public HashSet<Request> ServedOptionalRequests { get; private set; }

        // Sv במאמר - לוח זמנים עד כה
        public Schedule CurrentSchedule { get; private set; }

        // sv במאמר - העצירה הנוכחית
        public Stop CurrentStop { get; private set; }

        // מידע על המעלית - מתווסף לנוחות הגישה
        private readonly int elevatorCapacity;
        private readonly int maxFloors;

        /// <summary>
        /// מייצר צומת חדש בעץ החיפוש
        /// </summary>
        public PricingNode(
            int currentFloor,
            double currentTime,
            int currentLoad,
            HashSet<Request> servedAssignedRequests,
            List<Request> unservedAssignedRequests,
            HashSet<Request> servedOptionalRequests,
            List<Request> unservedOptionalRequests,
            Schedule currentSchedule,
            int elevatorCapacity,
            int maxFloors)
        {
            CurrentFloor = currentFloor;
            CurrentTime = currentTime;
            CurrentLoad = currentLoad;

            // קבוצות הבקשות - מחולקות לפי המאמר
            ServedAssignedRequests = servedAssignedRequests ?? new HashSet<Request>();
            UnservedAssignedRequests = unservedAssignedRequests ?? new List<Request>();
            ServedOptionalRequests = servedOptionalRequests ?? new HashSet<Request>();
            UnservedOptionalRequests = unservedOptionalRequests ?? new List<Request>();

            CurrentSchedule = currentSchedule ?? throw new ArgumentNullException(nameof(currentSchedule));

            this.elevatorCapacity = elevatorCapacity;
            this.maxFloors = maxFloors;

            // העצירה הנוכחית היא האחרונה בלוח הזמנים
            if (CurrentSchedule.Stops.Count > 0)
            {
                CurrentStop = CurrentSchedule.Stops[CurrentSchedule.Stops.Count - 1];
            }
            else
            {
                CurrentStop = null;
            }
        }
        public bool IsLast()
        {
            Console.WriteLine($"[IsLast DEBUG]:");
            Console.WriteLine($"  - UnservedAssignedRequests: {UnservedAssignedRequests.Count}");
            Console.WriteLine($"  - CurrentLoad: {CurrentLoad}");
            Console.WriteLine($"  - HasPendingDropCommitments: {HasPendingDropCommitments()}");
            Console.WriteLine($"  - UnservedOptionalRequests: {UnservedOptionalRequests.Count}");

            // בדיקה אם יש עבודה חובה
            bool hasMandatoryWork = UnservedAssignedRequests.Count > 0 ||
                                   CurrentLoad > 0 ||
                                   HasPendingDropCommitments();

            if (hasMandatoryWork)
            {
                Console.WriteLine($"  - יש עבודה חובה - לא סופי");
                return false;
            }

            // ✅ הוסף תנאי: אם יש בקשות אופציונליות ולא אספנו כלום, תמשיך!
            if (UnservedOptionalRequests.Count > 0 && ServedOptionalRequests.Count == 0)
            {
                Console.WriteLine($"  - יש בקשות אופציונליות ולא אספנו כלום - לא סופי");
                return false;
            }

            // ✅ או אלטרנטיבה: הגבל ל-3 בקשות אופציונליות מקסימום
            if (UnservedOptionalRequests.Count > 3)
            {
                Console.WriteLine($"  - יותר מ-3 בקשות אופציונליות ({UnservedOptionalRequests.Count}) - לא סופי");
                return false;
            }

            Console.WriteLine($"  - גמרנו! סופי");
            return true;
        }
        //public bool IsLast()
        //{
        //    Console.WriteLine($"[IsLast DEBUG]:");
        //    Console.WriteLine($"  - UnservedAssignedRequests: {UnservedAssignedRequests.Count}");
        //    Console.WriteLine($"  - CurrentLoad: {CurrentLoad}");
        //    Console.WriteLine($"  - HasPendingDropCommitments: {HasPendingDropCommitments()}");
        //    Console.WriteLine($"  - UnservedOptionalRequests: {UnservedOptionalRequests.Count}");

        //    // בדיקה אם יש עבודה חובה
        //    bool hasMandatoryWork = UnservedAssignedRequests.Count > 0 ||
        //                           CurrentLoad > 0 ||
        //                           HasPendingDropCommitments();

        //    if (hasMandatoryWork)
        //    {
        //        Console.WriteLine($"  - יש עבודה חובה - לא סופי");
        //        return false;
        //    }

        //    // ✅ הפתרון הפשוט: אם אין עבודה חובה, זה סופי!
        //    // לא משנה כמה בקשות אופציונליות יש
        //    Console.WriteLine($"  - אין עבודה חובה - סופי");
        //    return true;
        //}
        //public bool IsLast()
        //{
        //    // עבודה חובה
        //    bool hasMandatoryWork = UnservedAssignedRequests.Count > 0 ||
        //                           CurrentLoad > 0 ||
        //                           HasPendingDropCommitments();

        //    if (hasMandatoryWork) return false;

        //    // ✅ הוסף תנאי עצירה לבקשות אופציונליות:
        //    if (UnservedOptionalRequests.Count > 0)
        //    {
        //        // בדוק אם המעלית מלאה או שאין dual טוב
        //        if (CurrentLoad >= elevatorCapacity) return true;

        //        // בדוק אם כל הבקשות האופציונליות לא כדאיות
        //        bool anyWorthwhile = UnservedOptionalRequests.Any(request => {
        //            int requestIndex = unassignedRequests.IndexOf(request);
        //            double dual = (requestIndex >= 0) ? requestDuals[requestIndex] : 0;
        //            return dual > 30; // סף סביר
        //        });

        //        if (!anyWorthwhile) return true;
        //    }

        //    return true; // אין יותר עבודה
        //}
        /// <summary>
        /// בודק אם הצומת הוא צומת סופי (feasible node)
        /// לפי המאמר: צומת היא אפשרית אם Av ריק ו-sv הוא העצירה האחרונה של Sv
        /// </summary>
        //public bool IsLast()
        //{
        //    Console.WriteLine($"[IsLast DEBUG]:");
        //    Console.WriteLine($"  - UnservedAssignedRequests: {UnservedAssignedRequests.Count}");
        //    Console.WriteLine($"  - CurrentLoad: {CurrentLoad}");
        //    Console.WriteLine($"  - HasPendingDropCommitments: {HasPendingDropCommitments()}");
        //    Console.WriteLine($"  - UnservedOptionalRequests: {UnservedOptionalRequests.Count}");

        //    // אם יש עבודה חובה
        //    bool hasMandatoryWork = UnservedAssignedRequests.Count > 0 ||
        //                           CurrentLoad > 0 ||
        //                           HasPendingDropCommitments();

        //    if (hasMandatoryWork)
        //    {
        //        // ✅ בדיקה מיוחדת: אם המעלית מלאה ואין להוריד - תקוע!
        //        if (CurrentLoad >= elevatorCapacity && !HasDropsAtCurrentFloor() && !HasPendingDropCommitments())
        //        {
        //            Console.WriteLine($"  - ⚠️ מעלית תקועה (מלאה ואין הורדות) - סופי");
        //            return true;
        //        }

        //        Console.WriteLine($"  - יש עבודה חובה - לא סופי");
        //        return false;
        //    }

        //    // אם יש בקשות אופציונליות
        //    if (UnservedOptionalRequests.Count > 0)
        //    {
        //        // ✅ בדיקה: אם המעלית מלאה, לא יכולה לקחת יותר
        //        if (CurrentLoad >= elevatorCapacity)
        //        {
        //            Console.WriteLine($"  - מעלית מלאה, לא יכולה לקחת אופציונליות - סופי");
        //            return true;
        //        }

        //        Console.WriteLine($"  - יש בקשות אופציונליות - לא סופי");
        //        return false;
        //    }

        //    Console.WriteLine($"  - אין יותר עבודה - סופי");
        //    return true;
        //}

        //public bool IsLast()
        //{
        //    Console.WriteLine($"[IsLast DEBUG]:");
        //    Console.WriteLine($"  - UnservedAssignedRequests (Av): {UnservedAssignedRequests.Count}");
        //    Console.WriteLine($"  - CurrentLoad: {CurrentLoad}");
        //    Console.WriteLine($"  - HasPendingDropCommitments: {HasPendingDropCommitments()}");
        //    Console.WriteLine($"  - UnservedOptionalRequests (Ov): {UnservedOptionalRequests.Count}");

        //    // בדיקה אם יש עבודה חובה
        //    bool hasMandatoryWork = UnservedAssignedRequests.Count > 0 ||
        //                           CurrentLoad > 0 ||
        //                           HasPendingDropCommitments();

        //    if (hasMandatoryWork)
        //    {
        //        Console.WriteLine($"  - יש עבודה חובה - לא סופי");
        //        return false;
        //    }

        //    // ✅ השינוי החשוב: אם יש בקשות אופציונליות עם dual טוב, המשך לחפש
        //    if (UnservedOptionalRequests.Count > 0)
        //    {
        //        // בדוק אם כל הבקשות האופציונליות כבר נבדקו או שיש עוד מה לבדוק
        //        Console.WriteLine($"  - יש {UnservedOptionalRequests.Count} בקשות אופציונליות - לא סופי");
        //        return false;
        //    }

        //    Console.WriteLine($"  - אין יותר עבודה - סופי");
        //    return true;
        //}
        //public bool IsLast()
        //{
        //    Console.WriteLine($"[IsLast DEBUG - לפי המאמר]:");
        //    Console.WriteLine($"  - UnservedAssignedRequests (Av): {UnservedAssignedRequests.Count}");
        //    Console.WriteLine($"  - CurrentLoad: {CurrentLoad}");
        //    Console.WriteLine($"  - HasPendingDropCommitments: {HasPendingDropCommitments()}");
        //    Console.WriteLine($"  - UnservedOptionalRequests (Ov): {UnservedOptionalRequests.Count}");

        //    // לפי המאמר: צומת אפשרי אם Av ריק ואין drop floors
        //    // בקשות אופציונליות (Ov) לא משפיעות על "feasible"!
        //    bool noAssignedWork = UnservedAssignedRequests.Count == 0;  // Av = ∅
        //    bool noCurrentLoad = CurrentLoad == 0;                     // אין נוסעים
        //    bool noDropCommitments = !HasPendingDropCommitments();     // sv הוא עצירה אחרונה

        //    bool isFeasible = noAssignedWork && noCurrentLoad && noDropCommitments;

        //    Console.WriteLine($"  - תוצאה לפי המאמר: {isFeasible}");
        //    Console.WriteLine($"  - (בקשות אופציונליות לא משפיעות על feasible!)");

        //    return isFeasible;
        //}
        //public bool IsLast()
        //{
        //    Console.WriteLine($"[IsLast DEBUG]:");
        //    Console.WriteLine($"  - UnservedAssignedRequests: {UnservedAssignedRequests.Count}");
        //    Console.WriteLine($"  - CurrentLoad: {CurrentLoad}");
        //    Console.WriteLine($"  - HasPendingDropCommitments: {HasPendingDropCommitments()}");
        //    Console.WriteLine($"  - UnservedOptionalRequests: {UnservedOptionalRequests.Count}");

        //    // בדיקה אם יש עבודה חובה
        //    bool hasMandatoryWork = UnservedAssignedRequests.Count > 0 ||
        //                           CurrentLoad > 0 ||
        //                           HasPendingDropCommitments();

        //    if (hasMandatoryWork)
        //    {
        //        Console.WriteLine($"  - יש עבודה חובה - לא סופי");
        //        return false;
        //    }

        //    // אם אין עבודה חובה, בדוק אם יש בקשות אופציונליות שכדאי לקחת
        //    if (UnservedOptionalRequests.Count > 0)
        //    {
        //        // בדוק אם יש בקשות אופציונליות עם dual טוב
        //        foreach (var request in UnservedOptionalRequests)
        //        {
        //            int requestIndex = UnservedAssignedRequests.IndexOf(request);
        //            double dual = (requestIndex >= 0 && requestIndex < requestDuals.Length) ?
        //                         requestDuals[requestIndex] : 0;

        //            // אם יש dual גבוה, עדיין כדאי להמשיך
        //            if (dual > 20) // סף סביר
        //            {
        //                Console.WriteLine($"  - בקשה אופציונלית עם dual טוב ({dual}) - לא סופי");
        //                return false;
        //            }
        //        }
        //    }

        //    Console.WriteLine($"  - אין יותר עבודה משתלמת - סופי");
        //    return true;
        //}
        //public bool IsLast()
        //{
        //    bool noAssigned = UnservedAssignedRequests.Count == 0;
        //    bool noLoad = CurrentLoad == 0;
        //    bool noPendingDrops = !HasPendingDropCommitments();

        //    Console.WriteLine($"[IsLast DEBUG]:");
        //    Console.WriteLine($"  - UnservedAssignedRequests: {UnservedAssignedRequests.Count} (צריך 0)");
        //    Console.WriteLine($"  - CurrentLoad: {CurrentLoad} (צריך 0)");
        //    Console.WriteLine($"  - HasPendingDropCommitments: {HasPendingDropCommitments()} (צריך false)");
        //    Console.WriteLine($"  - UnservedOptionalRequests: {UnservedOptionalRequests.Count}");

        //    bool result = noAssigned && noLoad && noPendingDrops;
        //    Console.WriteLine($"  - תוצאה: {result}");

        //    return result;
        //}
        //public bool IsLast()
        //{
        //    // צומת אפשרי אם:
        //    // 1. כל הבקשות המשויכות נאספו (Av ריק)
        //    // 2. אין עוד הורדות (כל הנוסעים הורדו)
        //    // 3. כל drop commitments טופלו

        //    return UnservedAssignedRequests.Count == 0 &&
        //           CurrentLoad == 0 &&
        //           !HasPendingDropCommitments();
        //}

        ///// <summary>
        /// חישוב העלות המופחתת כפי שמתואר במאמר בסעיף 3.1:
        /// c̃(S) = c(S) - ∑ρ∈Ru∩S πρ - πe
        /// </summary>
        public double GetReducedCost(double[] requestDuals, double elevatorDual, List<Request> unassignedRequests)
        {
            double cost = CurrentSchedule.TotalCost;
            double dualSum = 0;

            // חישוב סכום הערכים הדואליים
            // שים לב: רק בקשות מ-Ru (בקשות לא משויכות/אופציונליות) נכנסות לחישוב!
            // המאמר מדגיש את זה בנוסחה: ∑ρ∈Ru∩S πρ
            foreach (var request in ServedOptionalRequests)
            {
                int requestIndex = unassignedRequests.IndexOf(request);
                if (requestIndex >= 0 && requestIndex < requestDuals.Length)
                {
                    dualSum += requestDuals[requestIndex];
                }
            }

            // החזרת העלות המופחתת
            return cost - dualSum - elevatorDual;
        }

        /// <summary>
        /// מחזיר את לוח הזמנים הנוכחי
        /// </summary>
        public Schedule GetSchedule()
        {
            // ודא שהלוח מכיל את כל הבקשות
            foreach (var request in ServedOptionalRequests)
            {
                if (!CurrentSchedule.ServedRequests.Contains(request))
                {
                    CurrentSchedule.ServedRequests.Add(request);
                }
            }

            return CurrentSchedule;
        }
        /// <summary>
        /// מייצר את כל הצמתים הבנים האפשריים מהצומת הנוכחי
        /// כל פעולה אפשרית (pickup, drop או move) מובילה לצומת בן
        /// לפי המאמר סעיף 3.1: "A child node v' of node v arises by two actions:
        /// Either a request is picked up or the elevator moves to the next floor for dropping a loaded call."
        public List<PricingNode> Branch()
        {
            Console.WriteLine($"[Branch DEBUG] מתחיל branching:");
            Console.WriteLine($"  - CurrentFloor: {CurrentFloor}");
            Console.WriteLine($"  - CurrentLoad: {CurrentLoad}");

            List<PricingNode> children = new List<PricingNode>();

            // עדיפות 1: הורדות חובה
            bool hasDrops = HasDropsAtCurrentFloor();
            if (hasDrops)
            {
                PricingNode dropNode = CreateDropNode();
                children.Add(dropNode);
                Console.WriteLine($"  - ✅ נוצר צומת הורדה");
                return children;
            }

            // עדיפות 2: איסופים משויכים
            List<Request> assignedPickups = GetPickableAssignedRequests();
            foreach (var request in assignedPickups)
            {
                PricingNode childNode = CreatePickupAssignedNode(request);
                children.Add(childNode);
                Console.WriteLine($"  - ✅ נוצר צומת איסוף משויך");
            }

            if (children.Count > 0) return children;

            // עדיפות 3: איסופים אופציונליים - רק אם יש מקום!
            if (CurrentLoad < elevatorCapacity)
            {
                List<Request> optionalPickups = GetPickableOptionalRequests();
                foreach (var request in optionalPickups.Take(1)) // רק הראשונה
                {
                    if (CurrentLoad + request.Calls.Count <= elevatorCapacity)
                    {
                        PricingNode childNode = CreatePickupOptionalNode(request);
                        children.Add(childNode);
                        Console.WriteLine($"  - ✅ נוצר צומת איסוף אופציונלי");
                        break; // רק אחת!
                    }
                }
            }

            // עדיפות 4: ✅ דילוג על בקשות אופציונליות (אם המעלית מלאה)
            if (UnservedOptionalRequests.Count > 0 && CurrentLoad >= elevatorCapacity)
            {
                Console.WriteLine($"  - מעלית מלאה - יוצר צומת דילוג על אופציונליות");

                PricingNode skipNode = new PricingNode(
                    currentFloor: CurrentFloor,
                    currentTime: CurrentTime,
                    currentLoad: CurrentLoad,
                    servedAssignedRequests: new HashSet<Request>(ServedAssignedRequests),
                    unservedAssignedRequests: new List<Request>(UnservedAssignedRequests),
                    servedOptionalRequests: new HashSet<Request>(ServedOptionalRequests),
                    unservedOptionalRequests: new List<Request>(), // ← דלג על הכל
                    currentSchedule: new Schedule(CurrentSchedule),
                    elevatorCapacity: elevatorCapacity,
                    maxFloors: maxFloors
                );

                children.Add(skipNode);
                return children;
            }
            // ✅ עדיפות 5: תנועות - רק אם באמת נדרש
            if (children.Count == 0 && HasPendingDropCommitments())
            {
                Console.WriteLine($"  - יוצר צמתי תנועה (יש התחייבויות)...");
                List<PricingNode> moveNodes = CreateMoveNodes();
                children.AddRange(moveNodes.Take(2)); // מקסימום 2 תנועות
            }

            // 🆕 אם אין פעולות אבל יש בקשות אופציונליות - תמשיך לחפש!
            if (children.Count == 0 && UnservedOptionalRequests.Count > 0)
            {
                Console.WriteLine($"  - אין פעולות בקומה הנוכחית, יוצר צמתי תנועה לחיפוש בקשות");
                List<PricingNode> moveNodes = CreateMoveNodes();
                children.AddRange(moveNodes);
            }

            // ✅ אם אין כלום לעשות - צור צומת "סיום"
            if (children.Count == 0)
            {
                Console.WriteLine($"  - אין יותר מה לעשות - יוצר צומת סיום");

                PricingNode endNode = new PricingNode(
                    currentFloor: CurrentFloor,
                    currentTime: CurrentTime,
                    currentLoad: 0, // ← נניח שהורדנו את כולם
                    servedAssignedRequests: new HashSet<Request>(ServedAssignedRequests),
                    unservedAssignedRequests: new List<Request>(),
                    servedOptionalRequests: new HashSet<Request>(ServedOptionalRequests),
                    unservedOptionalRequests: new List<Request>(),
                    currentSchedule: new Schedule(CurrentSchedule),
                    elevatorCapacity: elevatorCapacity,
                    maxFloors: maxFloors
                );

                children.Add(endNode);
            }
            // עדיפות 5: תנועות - רק אם באמת נדרש
            //if (children.Count == 0 && HasPendingDropCommitments())
            //{
            //    Console.WriteLine($"  - יוצר צמתי תנועה (יש התחייבויות)...");
            //    List<PricingNode> moveNodes = CreateMoveNodes();
            //    children.AddRange(moveNodes.Take(2)); // מקסימום 2 תנועות
            //}

            //// ✅ אם אין כלום לעשות - צור צומת "סיום"

            //if (children.Count == 0)
            //{
            //    Console.WriteLine($"  - אין יותר מה לעשות - יוצר צומת סיום");

            //    PricingNode endNode = new PricingNode(
            //        currentFloor: CurrentFloor,
            //        currentTime: CurrentTime,
            //        currentLoad: 0, // ← נניח שהורדנו את כולם
            //        servedAssignedRequests: new HashSet<Request>(ServedAssignedRequests),
            //        unservedAssignedRequests: new List<Request>(),
            //        servedOptionalRequests: new HashSet<Request>(ServedOptionalRequests),
            //        unservedOptionalRequests: new List<Request>(),
            //        currentSchedule: new Schedule(CurrentSchedule),
            //        elevatorCapacity: elevatorCapacity,
            //        maxFloors: maxFloors
            //    );

            //    children.Add(endNode);
            //}

            Console.WriteLine($"[Branch DEBUG] סה\"כ בנים: {children.Count}");
            return children;
        }
        //public List<PricingNode> Branch()
        //{
        //    Console.WriteLine($"[Branch DEBUG] מתחיל branching:");
        //    Console.WriteLine($"  - CurrentFloor: {CurrentFloor}");
        //    Console.WriteLine($"  - CurrentLoad: {CurrentLoad}");

        //    List<PricingNode> children = new List<PricingNode>();

        //    // בדיקת הורדות
        //    bool hasDrops = HasDropsAtCurrentFloor();
        //    Console.WriteLine($"  - HasDropsAtCurrentFloor: {hasDrops}");

        //    if (hasDrops)
        //    {
        //        Console.WriteLine($"  - יוצר צומת הורדה...");
        //        PricingNode dropNode = CreateDropNode();
        //        children.Add(dropNode);
        //        Console.WriteLine($"  - ✅ נוצר צומת הורדה");
        //        return children;
        //    }

        //    // בדיקת איסופים משויכים
        //    List<Request> assignedPickups = GetPickableAssignedRequests();
        //    Console.WriteLine($"  - מספר איסופים משויכים: {assignedPickups.Count}");

        //    foreach (var request in assignedPickups)
        //    {
        //        Console.WriteLine($"  - יוצר צומת איסוף משויך לבקשה {request.Id}...");
        //        PricingNode childNode = CreatePickupAssignedNode(request);
        //        children.Add(childNode);
        //        Console.WriteLine($"  - ✅ נוצר צומת איסוף משויך");
        //    }

        //    if (children.Count > 0)
        //    {
        //        Console.WriteLine($"  - מחזיר {children.Count} צמתים (איסופים משויכים)");
        //        return children;
        //    }

        //    // בדיקת איסופים אופציונליים
        //    List<Request> optionalPickups = GetPickableOptionalRequests();
        //    Console.WriteLine($"  - מספר איסופים אופציונליים זמינים: {optionalPickups.Count}");

        //    foreach (var request in optionalPickups)
        //    {
        //        if (CurrentLoad + request.Calls.Count <= elevatorCapacity)
        //        {
        //            Console.WriteLine($"  - יוצר צומת איסוף אופציונלי לבקשה {request.Id}...");
        //            PricingNode childNode = CreatePickupOptionalNode(request);
        //            children.Add(childNode);
        //            Console.WriteLine($"  - ✅ נוצר צומת איסוף אופציונלי");
        //        }
        //        else
        //        {
        //            Console.WriteLine($"  - ❌ איסוף אופציונלי נדחה (קיבולת: {CurrentLoad} + {request.Calls.Count} > {elevatorCapacity})");
        //        }
        //    }

        //    // בדיקת תנועות
        //    if (children.Count == 0 || HasPendingDropCommitments())
        //    {
        //        Console.WriteLine($"  - יוצר צמתי תנועה...");
        //        List<PricingNode> moveNodes = CreateMoveNodes();
        //        children.AddRange(moveNodes);
        //        Console.WriteLine($"  - ✅ נוצרו {moveNodes.Count} צמתי תנועה");
        //    }

        //    Console.WriteLine($"[Branch DEBUG] סה\"כ בנים: {children.Count}");
        //    return children;
        //}
        /// </summary>
        //public List<PricingNode> Branch()
        //{
        //    List<PricingNode> children = new List<PricingNode>();
        //    Direction currentDirection = CurrentStop?.Direction ?? Direction.Idle;

        //    // ✅ עדיפות 1: הורדות חובה בקומה הנוכחית
        //    if (HasDropsAtCurrentFloor())
        //    {
        //        PricingNode dropNode = CreateDropNode();
        //        children.Add(dropNode);
        //        return children; // רק הורדה - לא ממשיכים לאפשרויות אחרות
        //    }

        //    // ✅ עדיפות 2: איסוף בקשות משויכות חובה בקומה הנוכחית
        //    List<Request> assignedPickups = GetPickableAssignedRequests();
        //    if (assignedPickups.Count > 0)
        //    {
        //        foreach (var request in assignedPickups)
        //        {
        //            PricingNode childNode = CreatePickupAssignedNode(request);
        //            children.Add(childNode);
        //        }
        //        return children; // רק איסופים חובה
        //    }

        //    // ✅ עדיפות 3: איסוף בקשות אופציונליות (רק אם יש מקום)
        //    List<Request> optionalPickups = GetPickableOptionalRequests();
        //    foreach (var request in optionalPickups)
        //    {
        //        if (CurrentLoad + request.Calls.Count <= elevatorCapacity)
        //        {
        //            PricingNode childNode = CreatePickupOptionalNode(request);
        //            children.Add(childNode);
        //        }
        //    }

        //    // ✅ עדיפות 4: תנועה (רק אם אין פעולות אחרות)
        //    if (children.Count == 0 || HasPendingDropCommitments())
        //    {
        //        List<PricingNode> moveNodes = CreateMoveNodes();
        //        children.AddRange(moveNodes);
        //    }

        //    return children;
        //}

        //public List<PricingNode> Branch()
        //{
        //    List<PricingNode> children = new List<PricingNode>();
        //    Direction currentDirection = CurrentStop?.Direction ?? Direction.Idle;

        //    // חלק 1: אם יש בקשות משויכות שטרם נאספו בקומה הנוכחית ובכיוון הנוכחי
        //    // לפי ה-first-stop pickup requirement מהמאמר, חייבים לאסוף אותן
        //    foreach (var request in GetPickableAssignedRequests())
        //    {
        //        PricingNode childNode = CreatePickupAssignedNode(request);
        //        children.Add(childNode);
        //    }

        //    // חלק 2: אם יש בקשות אופציונליות שטרם נאספו בקומה הנוכחית ובכיוון הנוכחי
        //    // המאמר מציין גם שניתן לעשות dual fixing אם πρ ≤ c̄(ρ)
        //    foreach (var request in GetPickableOptionalRequests())
        //    {
        //        // בדיקת קיבולת - בקשות אופציונליות נאספות רק אם יש מקום
        //        if (CurrentLoad + request.Calls.Count <= elevatorCapacity)
        //        {
        //            PricingNode childNode = CreatePickupOptionalNode(request);
        //            children.Add(childNode);
        //        }
        //    }

        //    // חלק 3: אם יש הורדות בקומה הנוכחית
        //    if (HasDropsAtCurrentFloor())
        //    {
        //        PricingNode childNode = CreateDropNode();
        //        children.Add(childNode);
        //    }

        //    // חלק 4: מעבר לקומה הבאה - לפי האילוצים של drop commitments
        //    List<PricingNode> moveNodes = CreateMoveNodes();
        //    children.AddRange(moveNodes);

        //    return children;
        //}

        /// <summary>
        /// מחזיר את כל הבקשות המשויכות שניתן לאסוף בקומה הנוכחית ובכיוון הנוכחי
        /// </summary>
        private List<Request> GetPickableAssignedRequests()
        {
            Direction currentDirection = CurrentStop?.Direction ?? Direction.Idle;

            return UnservedAssignedRequests
                .Where(r => r.StartFloor == CurrentFloor &&
                           (currentDirection == Direction.Idle ||
                            currentDirection == DetermineDirection(r.StartFloor, r.DestinationFloor)))
                .ToList();
        }

        /// <summary>
        /// מחזיר את כל הבקשות האופציונליות שניתן לאסוף בקומה הנוכחית ובכיוון הנוכחי
        /// </summary>
        private List<Request> GetPickableOptionalRequests()
        {
            Direction currentDirection = CurrentStop?.Direction ?? Direction.Idle;
            return UnservedOptionalRequests.Take(2).ToList();

            //return UnservedOptionalRequests
            //    .Where(r => r.StartFloor == CurrentFloor &&
            //               (currentDirection == Direction.Idle ||
            //                currentDirection == DetermineDirection(r.StartFloor, r.DestinationFloor)))
            //    .ToList();
        }

        /// <summary>
        /// בודק אם יש הורדות בקומה הנוכחית
        /// </summary>
        private bool HasDropsAtCurrentFloor()
        {
            // בדיקה אם יש קריאות שיש להוריד בקומה הנוכחית
            foreach (var stop in CurrentSchedule.Stops)
            {
                foreach (var pickup in stop.Pickups)
                {
                    foreach (var call in pickup.Calls)
                    {
                        if (call.DestinationFloor == CurrentFloor)
                        {
                            return true;
                        }
                    }
                }
            }

            // בדיקה ב-drop commitments
            if (CurrentStop != null && CurrentStop.DropFloors.Contains(CurrentFloor))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// בודק אם יש drop commitments שטרם טופלו
        /// </summary>
        private bool HasPendingDropCommitments()
        {
            if (CurrentStop == null)
                return false;

            return CurrentStop.DropFloors.Count > 0;
        }

        /// <summary>
        /// מייצר צמתים למעבר לקומות הבאות, בהתאם לאילוצי drop commitments
        /// לפי המאמר, drop commitments משפיעים על הקומה הבאה והכיוון
        /// </summary>
        //private List<PricingNode> CreateMoveNodes()
        //{
        //    List<PricingNode> moveNodes = new List<PricingNode>();
        //    Direction currentDirection = CurrentStop?.Direction ?? Direction.Idle;

        //    // מקרה 1: אם יש drop commitments, חייבים לנוע לכיוון הקומה הבאה בדרך
        //    if (HasPendingDropCommitments())
        //    {
        //        int nextDropFloor = GetNextDropFloor();
        //        Direction requiredDirection = DetermineDirection(CurrentFloor, nextDropFloor);

        //        // אם הקומה הבאה היא כבר drop floor, לא צריך לנוע
        //        if (nextDropFloor != CurrentFloor)
        //        {
        //            // צעד בכיוון הנדרש
        //            int nextFloor = CurrentFloor + (requiredDirection == Direction.Up ? 1 : -1);
        //            if (IsValidFloor(nextFloor))
        //            {
        //                moveNodes.Add(CreateMoveNode(nextFloor, requiredDirection));
        //            }
        //        }
        //    }
        //    // מקרה 2: אם אין drop commitments, אפשר לנוע לכל כיוון (אם יש כיוון נוכחי)
        //    else if (currentDirection != Direction.Idle)
        //    {
        //        int nextFloor = CurrentFloor + (currentDirection == Direction.Up ? 1 : -1);
        //        if (IsValidFloor(nextFloor))
        //        {
        //            moveNodes.Add(CreateMoveNode(nextFloor, currentDirection));
        //        }

        //        // לפי המאמר, אם אין drop commitments והגענו לקומה האחרונה בכיוון,
        //        // אפשר גם לשנות כיוון (מקרה מיוחד למערכת עם כמות גדולה של בקשות)
        //        int oppositeFloor = CurrentFloor + (currentDirection == Direction.Up ? -1 : 1);
        //        if (IsLastFloorInDirection(currentDirection) && IsValidFloor(oppositeFloor))
        //        {
        //            Direction oppositeDirection = currentDirection == Direction.Up ? Direction.Down : Direction.Up;
        //            moveNodes.Add(CreateMoveNode(oppositeFloor, oppositeDirection));
        //        }
        //    }

        //    return moveNodes;
        //}
        private List<PricingNode> CreateMoveNodes()
        {
            List<PricingNode> moveNodes = new List<PricingNode>();
            HashSet<(int floor, Direction dir)> potentialNextSteps = new HashSet<(int, Direction)>();

            Direction currentElevatorDirection = CurrentStop?.Direction ?? Direction.Idle;

            // 1. אפשרות: המשך תנועה בכיוון הנוכחי (אם המעלית אינה במצב סרק)
            // זוהי אפשרות ברירת המחדל להמשיך ישר.
            if (currentElevatorDirection != Direction.Idle)
            {
                int nextFloorInCurrentDir = CurrentFloor + (currentElevatorDirection == Direction.Up ? 1 : -1);
                if (IsValidFloor(nextFloorInCurrentDir))
                {
                    potentialNextSteps.Add((nextFloorInCurrentDir, currentElevatorDirection));
                }
            }

            // 2. אפשרות: שקול תנועה בכיוון ההפוך
            // זה קריטי כדי לאפשר שינויי כיוון ולחקור נתיבים שבהם שינוי כיוון מועיל.
            Direction oppositeDirection = Direction.Idle;
            if (currentElevatorDirection == Direction.Up)
            {
                oppositeDirection = Direction.Down;
            }
            else if (currentElevatorDirection == Direction.Down)
            {
                oppositeDirection = Direction.Up;
            }
            else // אם currentElevatorDirection == Direction.Idle, נטפל בזה בהמשך
            {
                // במצב Idle, נתחיל בכיוון Up כברירת מחדל לאפשרות זו,
                // ונבחן גם Down בנפרד.
                oppositeDirection = Direction.Down; // נחשב את ה"הפוך" כ-Down אם ה"נוכחי" היה פוטנציאלית Up
            }


            int nextFloorOpposite = CurrentFloor + (oppositeDirection == Direction.Up ? 1 : -1);

            bool shouldConsiderOpposite = false;

            // א. תמיד שקול אם הגיעה לקצה פיזי (כפי שהיה במקור)
            if (currentElevatorDirection != Direction.Idle && IsLastFloorInDirection(currentElevatorDirection))
            {
                shouldConsiderOpposite = true;
            }
            // ב. אם אין נוסעים להורדה **מחוייבת** ובקשות שלא נאספו (משויכות או אופציונליות) נמצאות בכיוון ההפוך
            else if (!HasPendingDropCommitments() &&
                     (UnservedAssignedRequests.Any(r => DetermineDirection(CurrentFloor, r.StartFloor) == oppositeDirection) ||
                      UnservedOptionalRequests.Any(r => DetermineDirection(CurrentFloor, r.StartFloor) == oppositeDirection)))
            {
                shouldConsiderOpposite = true;
            }
            // ג. אם המעלית במצב סרק ויש בקשות שלא נאספו כלשהן (כדי לאלץ תנועה התחלתית)
            else if (currentElevatorDirection == Direction.Idle &&
                     (UnservedAssignedRequests.Any() || UnservedOptionalRequests.Any()))
            {
                shouldConsiderOpposite = true; // נטפל ביצירת שני הכיוונים (Up ו-Down) בסעיף הבא
            }


            if (shouldConsiderOpposite && IsValidFloor(nextFloorOpposite) && nextFloorOpposite != CurrentFloor)
            {
                potentialNextSteps.Add((nextFloorOpposite, oppositeDirection));
            }


            // 3. טיפול מיוחד במצב Idle: אם המעלית במצב סרק מוחלט, היא צריכה לבחון תנועה גם למעלה וגם למטה
            // כדי למצוא איסופים פוטנציאליים. זה מבטיח תנועה התחלתית.
            // רק אם עדיין לא נוספו צעדים קונקרטיים מסיבות אחרות
            if (currentElevatorDirection == Direction.Idle && !potentialNextSteps.Any())
            {
                if (IsValidFloor(CurrentFloor + 1))
                {
                    potentialNextSteps.Add((CurrentFloor + 1, Direction.Up));
                }
                if (IsValidFloor(CurrentFloor - 1))
                {
                    potentialNextSteps.Add((CurrentFloor - 1, Direction.Down));
                }
            }

            // צור צמתים עבור כל הצעדים הפוטנציאליים שנקבעו
            foreach (var step in potentialNextSteps)
            {
                moveNodes.Add(CreateMoveNode(step.floor, step.dir));
            }

            // ודא שאין צמתים כפולים (אם אותה קומה באותו כיוון נוספה ביותר מדרך אחת)
            return moveNodes.Distinct().ToList();
        }

        /// <summary>
        /// בודק אם הקומה הנוכחית היא האחרונה בכיוון הנוכחי
        /// </summary>
        private bool IsLastFloorInDirection(Direction direction)
        {
            return (direction == Direction.Up && CurrentFloor == maxFloors) ||
                   (direction == Direction.Down && CurrentFloor == 1);
        }

        /// <summary>
        /// מחזיר את הקומה הבאה שיש בה drop commitment
        /// </summary>
        private int GetNextDropFloor()
        {
            if (CurrentStop == null || CurrentStop.DropFloors.Count == 0)
                return CurrentFloor;

            // לפי כיוון הנסיעה, מוצאים את הקומה הבאה
            Direction currentDirection = CurrentStop.Direction;

            if (currentDirection == Direction.Up)
            {
                // הקומה הנמוכה ביותר שגבוהה מהקומה הנוכחית
                return CurrentStop.DropFloors
                    .Where(f => f > CurrentFloor)
                    .DefaultIfEmpty(CurrentFloor)
                    .Min();
            }
            else // כיוון למטה או idle
            {
                // הקומה הגבוהה ביותר שנמוכה מהקומה הנוכחית
                return CurrentStop.DropFloors
                    .Where(f => f < CurrentFloor)
                    .DefaultIfEmpty(CurrentFloor)
                    .Max();
            }
        }

        /// <summary>
        /// מייצר צומת חדש לאיסוף בקשה משויכת
        /// </summary>
        private PricingNode CreatePickupAssignedNode(Request request)
        {
            // יצירת העתקים של קבוצות הבקשות
            HashSet<Request> newServedAssignedRequests = new HashSet<Request>(ServedAssignedRequests);
            newServedAssignedRequests.Add(request);

            List<Request> newUnservedAssignedRequests = new List<Request>(UnservedAssignedRequests);
            newUnservedAssignedRequests.Remove(request);

            // יצירת העתק של לוח הזמנים
            Schedule newSchedule = new Schedule(CurrentSchedule);

            // יצירת עצירה חדשה לאיסוף
            Direction pickupDirection = DetermineDirection(request.StartFloor, request.DestinationFloor);
            Stop pickupStop = new Stop
            {
                Floor = CurrentFloor,
                ArrivalTime = (float)CurrentTime,
                Direction = pickupDirection
            };

            // הוספת הבקשה לעצירת האיסוף
            pickupStop.AddPickup(request);

            // הוספת העצירה ללוח הזמנים
            newSchedule.AddStop(pickupStop);

            // חישוב עלויות עבור הבקשה שנאספה
            double waitCost = 0;
            foreach (var call in request.Calls)
            {
                // זמן ההמתנה מזמן הרישום עד זמן האיסוף
                double waitTime = Math.Max(0, CurrentTime - call.ReleaseTime.ToOADate());
                waitCost += call.WaitCost * waitTime;
            }

            // יתכן שיש גם עלות חריגת קיבולת
            double capacityCost = 0;
            if (CurrentLoad + request.Calls.Count > elevatorCapacity)
            {
                capacityCost = Constant.CapacityPenalty * (CurrentLoad + request.Calls.Count - elevatorCapacity);
            }

            // הוספת העלות ללוח הזמנים
            newSchedule.TotalCost += (float)(waitCost + capacityCost);

            // זמן חדש אחרי האיסוף
            double newTime = CurrentTime + Constant.StopTime;

            // עומס חדש אחרי האיסוף
            int newLoad = CurrentLoad + request.Calls.Count;

            // עדכון drop commitments
            foreach (var call in request.Calls)
            {
                pickupStop.DropFloors.Add(call.DestinationFloor);
            }

            return new PricingNode(
                CurrentFloor,
                newTime,
                newLoad,
                newServedAssignedRequests,
                newUnservedAssignedRequests,
                ServedOptionalRequests,
                UnservedOptionalRequests,
                newSchedule,
                elevatorCapacity,
                maxFloors
            );
        }

        /// <summary>
        /// מייצר צומת חדש לאיסוף בקשה אופציונלית
        /// </summary>
        private PricingNode CreatePickupOptionalNode(Request request)
        {
            // יצירת העתקים של קבוצות הבקשות
            HashSet<Request> newServedOptionalRequests = new HashSet<Request>(ServedOptionalRequests);
            newServedOptionalRequests.Add(request);
            List<Request> newUnservedOptionalRequests = new List<Request>(UnservedOptionalRequests);
            newUnservedOptionalRequests.Remove(request);

            // יצירת העתק של לוח הזמנים
            Schedule newSchedule = new Schedule(CurrentSchedule);

            // ✅ חישוב זמן נסיעה לקומת הבקשה
            double travelTime = CalculateTravelTime(CurrentFloor, request.StartFloor);
            double arrivalTime = CurrentTime + travelTime;

            // יצירת עצירה חדשה לאיסוף
            Direction pickupDirection = DetermineDirection(request.StartFloor, request.DestinationFloor);
            Stop pickupStop = new Stop
            {
                Floor = request.StartFloor,  // ✅ שונה מ-CurrentFloor
                ArrivalTime = (float)arrivalTime,  // ✅ שונה מ-CurrentTime
                Direction = pickupDirection
            };

            // הוספת הבקשה לעצירת האיסוף
            pickupStop.AddPickup(request);

            // הוספת העצירה ללוח הזמנים
            newSchedule.AddStop(pickupStop);

            // חישוב עלויות עבור הבקשה שנאספה
            double waitCost = 0;
            foreach (var call in request.Calls)
            {
                // זמן ההמתנה מזמן הרישום עד זמן האיסוף
                double waitTime = Math.Max(0, arrivalTime - call.ReleaseTime.ToOADate());  // ✅ שונה מ-CurrentTime
                waitCost += call.WaitCost * waitTime;
            }

            // יתכן שיש גם עלות חריגת קיבולת
            double capacityCost = 0;
            if (CurrentLoad + request.Calls.Count > elevatorCapacity)
            {
                capacityCost = Constant.CapacityPenalty * (CurrentLoad + request.Calls.Count - elevatorCapacity);
            }

            // ✅ הוסף עלות נסיעה
            double travelCost = travelTime * 1.0; // או כל פקטור עלות נסיעה שרלוונטי

            // הוספת העלות ללוח הזמנים
            newSchedule.TotalCost += (float)(waitCost + capacityCost + travelCost);  // ✅ הוסף travelCost

            // זמן חדש אחרי האיסוף
            double newTime = arrivalTime + Constant.StopTime;  // ✅ שונה מ-CurrentTime

            // עומס חדש אחרי האיסוף
            int newLoad = CurrentLoad + request.Calls.Count;

            // עדכון drop commitments
            foreach (var call in request.Calls)
            {
                pickupStop.DropFloors.Add(call.DestinationFloor);
            }

            return new PricingNode(
                request.StartFloor,  // ✅ שונה מ-CurrentFloor - עכשיו במיקום הבקשה
                newTime,
                newLoad,
                ServedAssignedRequests,
                UnservedAssignedRequests,
                newServedOptionalRequests,
                newUnservedOptionalRequests,
                newSchedule,
                elevatorCapacity,
                maxFloors
            );
        }
        //private PricingNode CreatePickupOptionalNode(Request request)
        //{
        //    // יצירת העתקים של קבוצות הבקשות
        //    HashSet<Request> newServedOptionalRequests = new HashSet<Request>(ServedOptionalRequests);
        //    newServedOptionalRequests.Add(request);

        //    List<Request> newUnservedOptionalRequests = new List<Request>(UnservedOptionalRequests);
        //    newUnservedOptionalRequests.Remove(request);

        //    // יצירת העתק של לוח הזמנים
        //    Schedule newSchedule = new Schedule(CurrentSchedule);

        //    // יצירת עצירה חדשה לאיסוף
        //    Direction pickupDirection = DetermineDirection(request.StartFloor, request.DestinationFloor);
        //    Stop pickupStop = new Stop
        //    {
        //        Floor = CurrentFloor,
        //        ArrivalTime = (float)CurrentTime,
        //        Direction = pickupDirection
        //    };

        //    // הוספת הבקשה לעצירת האיסוף
        //    pickupStop.AddPickup(request);

        //    // הוספת העצירה ללוח הזמנים
        //    newSchedule.AddStop(pickupStop);

        //    // חישוב עלויות עבור הבקשה שנאספה
        //    double waitCost = 0;
        //    foreach (var call in request.Calls)
        //    {
        //        // זמן ההמתנה מזמן הרישום עד זמן האיסוף
        //        double waitTime = Math.Max(0, CurrentTime - call.ReleaseTime.ToOADate());
        //        waitCost += call.WaitCost * waitTime;
        //    }

        //    // יתכן שיש גם עלות חריגת קיבולת
        //    double capacityCost = 0;
        //    if (CurrentLoad + request.Calls.Count > elevatorCapacity)
        //    {
        //        capacityCost = Constant.CapacityPenalty * (CurrentLoad + request.Calls.Count - elevatorCapacity);
        //    }

        //    // הוספת העלות ללוח הזמנים
        //    newSchedule.TotalCost += (float)(waitCost + capacityCost);

        //    // זמן חדש אחרי האיסוף
        //    double newTime = CurrentTime + Constant.StopTime;

        //    // עומס חדש אחרי האיסוף
        //    int newLoad = CurrentLoad + request.Calls.Count;

        //    // עדכון drop commitments
        //    foreach (var call in request.Calls)
        //    {
        //        pickupStop.DropFloors.Add(call.DestinationFloor);
        //    }

        //    return new PricingNode(
        //        CurrentFloor,
        //        newTime,
        //        newLoad,
        //        ServedAssignedRequests,
        //        UnservedAssignedRequests,
        //        newServedOptionalRequests,
        //        newUnservedOptionalRequests,
        //        newSchedule,
        //        elevatorCapacity,
        //        maxFloors
        //    );
        //}

        /// <summary>
        /// מייצר צומת חדש להורדת נוסעים בקומה הנוכחית
        /// </summary>
        private PricingNode CreateDropNode()
        {
            // יצירת העתק של לוח הזמנים
            Schedule newSchedule = new Schedule(CurrentSchedule);

            // יצירת עצירה חדשה להורדה
            Stop dropStop = new Stop
            {
                Floor = CurrentFloor,
                ArrivalTime = (float)CurrentTime,
                Direction = CurrentStop?.Direction ?? Direction.Idle
            };

            // מציאת קריאות להורדה בקומה הנוכחית
            List<Call> dropsHere = FindDropsAtCurrentFloor();

            // הוספת ההורדות לעצירה
            foreach (var call in dropsHere)
            {
                dropStop.AddDrop(call);
            }

            // הוספת העצירה ללוח הזמנים
            newSchedule.AddStop(dropStop);

            // חישוב עלויות עבור ההורדות
            double travelCost = 0;
            foreach (var call in dropsHere)
            {
                // מציאת זמן האיסוף של הקריאה
                double pickupTime = FindPickupTimeForCall(call);

                // זמן הנסיעה מזמן האיסוף עד זמן ההורדה
                double travelTime = CurrentTime - pickupTime;
                travelCost += call.TravelCost * travelTime;
            }

            // הוספת העלות ללוח הזמנים
            newSchedule.TotalCost += (float)travelCost;

            // זמן חדש אחרי ההורדה
            double newTime = CurrentTime + Constant.StopTime;

            // עומס חדש אחרי ההורדה
            int newLoad = CurrentLoad - dropsHere.Count;

            // עדכון drop commitments - הסרת הקומה הנוכחית
            HashSet<int> newDropFloors = new HashSet<int>(dropStop.DropFloors);
            newDropFloors.Remove(CurrentFloor);
            dropStop.DropFloors = newDropFloors;

            return new PricingNode(
                CurrentFloor,
                newTime,
                newLoad,
                ServedAssignedRequests,
                UnservedAssignedRequests,
                ServedOptionalRequests,
                UnservedOptionalRequests,
                newSchedule,
                elevatorCapacity,
                maxFloors
            );
        }

        /// <summary>
        /// מייצר צומת חדש למעבר לקומה אחרת
        /// </summary>
        private PricingNode CreateMoveNode(int nextFloor, Direction direction)
        {
            // יצירת העתק של לוח הזמנים
            Schedule newSchedule = new Schedule(CurrentSchedule);

            // חישוב זמן הנסיעה
            double travelTime = CalculateTravelTime(CurrentFloor, nextFloor);
            double newTime = CurrentTime + travelTime;

            // יצירת עצירה חדשה
            Stop moveStop = new Stop
            {
                Floor = nextFloor,
                ArrivalTime = (float)newTime,
                Direction = direction
            };

            // העתקת drop commitments
            if (CurrentStop != null)
            {
                foreach (int floor in CurrentStop.DropFloors)
                {
                    moveStop.DropFloors.Add(floor);
                }
            }

            // הוספת העצירה ללוח הזמנים
            newSchedule.AddStop(moveStop);

            return new PricingNode(
                nextFloor,
                newTime,
                CurrentLoad,
                ServedAssignedRequests,
                UnservedAssignedRequests,
                ServedOptionalRequests,
                UnservedOptionalRequests,
                newSchedule,
                elevatorCapacity,
                maxFloors
            );
        }

        /// <summary>
        /// מוצא את כל הקריאות שיש להוריד בקומה הנוכחית
        /// </summary>
        private List<Call> FindDropsAtCurrentFloor()
        {
            List<Call> drops = new List<Call>();

            // מעבר על כל העצירות בלוח הזמנים
            foreach (var stop in CurrentSchedule.Stops)
            {
                // מעבר על כל האיסופים בעצירה
                foreach (var pickup in stop.Pickups)
                {
                    // מעבר על כל הקריאות באיסוף
                    foreach (var call in pickup.Calls)
                    {
                        // אם היעד של הקריאה הוא הקומה הנוכחית
                        if (call.DestinationFloor == CurrentFloor)
                        {
                            drops.Add(call);
                        }
                    }
                }
            }

            return drops;
        }

        /// <summary>
        /// מוצא את זמן האיסוף של קריאה
        /// </summary>
        private double FindPickupTimeForCall(Call call)
        {
            foreach (var stop in CurrentSchedule.Stops)
            {
                foreach (var pickup in stop.Pickups)
                {
                    if (pickup.Calls.Contains(call))
                    {
                        return stop.ArrivalTime;
                    }
                }
            }
            return 0;
        }

        /// <summary>
        /// מחשב את זמן הנסיעה בין שתי קומות
        /// </summary>
        private double CalculateTravelTime(int fromFloor, int toFloor)
        {
            int distance = Math.Abs(toFloor - fromFloor);
            if (distance == 0) return 0;

            return Constant.ElevatorStartupTime + distance * Constant.DrivePerFloorTime;
        }
        private Direction DetermineDirection(int fromFloor, int toFloor)
        {
            if (fromFloor < toFloor) return Direction.Up;
            if (fromFloor > toFloor) return Direction.Down;
            return Direction.Idle;
        }
        private bool IsValidFloor(int floor)
        {
            return floor >= 1 && floor <= maxFloors;
        }
    }
}