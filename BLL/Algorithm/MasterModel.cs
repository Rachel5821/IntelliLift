using ILOG.Concert;
using ILOG.CPLEX;
using Project;
using System;
using System.Collections.Generic;

namespace Project.Algorithm
{
    public class BranchingConstraintInfo
    {
        public int RequestIndex { get; set; }
        public List<int> ElevatorGroup { get; set; }
        public bool IsAssigned { get; set; }

        public BranchingConstraintInfo(int requestIndex, List<int> elevatorGroup, bool isAssigned)
        {
            RequestIndex = requestIndex;
            ElevatorGroup = new List<int>(elevatorGroup); // העתקה עמוקה
            IsAssigned = isAssigned;
        }
    }

    public class MasterModel
    {
        private Cplex cplex;
        private ProblemInstance instance;
        private IObjective objective;
        private List<INumVar> scheduleVars; //משתנים עבור כל לוח זמנים

        //אילוצים
        private IRange[] requestConstraints;
        private IRange[] elevatorConstraints;
        private List<IRange> branchingConstraints;
        private Dictionary<IRange, BranchingConstraintInfo> constraintInfoMap; // מיפוי בין אילוצים למידע שלהם
        private ScheduleCollection schedules;

        public MasterModel(ProblemInstance problemInstance)
        {
            cplex = new Cplex();
            instance = problemInstance;
            scheduleVars = new List<INumVar>();
            branchingConstraints = new List<IRange>();
            constraintInfoMap = new Dictionary<IRange, BranchingConstraintInfo>();
            schedules = new ScheduleCollection();
            InitializeModel();
        }

        public MasterModel(MasterModel other)
        {
            cplex = new Cplex();
            instance = other.instance;
            scheduleVars = new List<INumVar>();
            branchingConstraints = new List<IRange>();
            constraintInfoMap = new Dictionary<IRange, BranchingConstraintInfo>();
            schedules = new ScheduleCollection(other.schedules);

            // העתקת כל column מהמודל המקורי
            foreach (var schedule in other.schedules.GetAllSchedules())
            {
                AddSchedule(schedule, schedule.ElevatorIndex);
            }

            // העתקת אילוצי branching
            // שים לב: זו העתקה פשטנית שעשויה לא לעבוד מושלם
            foreach (var constraint in other.branchingConstraints)
            {
                if (other.constraintInfoMap.TryGetValue(constraint, out var info))
                {
                    AddBranchingConstraint(info.RequestIndex, info.ElevatorGroup, info.IsAssigned);
                }
            }
        }

        private void InitializeModel()
        {
            try
            {
                objective = cplex.AddMinimize();
                //אילוץ לבקשות
                var unassignedRequests = instance.GetUnassignedRequests();
                requestConstraints = new IRange[unassignedRequests.Count];
                for (int i = 0; i < unassignedRequests.Count; i++)
                {
                    requestConstraints[i] = cplex.AddRange(1.0, 1.0);
                }
                //אילוץ למעליות
                elevatorConstraints = new IRange[instance.numElevators];
                for (int i = 0; i < instance.numElevators; i++)
                {
                    elevatorConstraints[i] = cplex.AddRange(1.0, 1.0);
                }
                cplex.SetParam(Cplex.Param.RootAlgorithm, Cplex.Algorithm.Primal);
            }
            catch (ILOG.Concert.Exception ex)
            {
                Console.WriteLine("Error initializing master model: " + ex.Message);
                throw;
            }
        }

        // שינוי במחלקת MasterModel בפונקציה AddSchedule

        public void AddSchedule(Schedule schedule, int elevatorIndex)
        {
            try
            {
                schedules.Add(schedule);
                ILOG.Concert.Column column = cplex.Column(objective, schedule.TotalCost);
                column = column.And(cplex.Column(elevatorConstraints[elevatorIndex], 1.0));
                var unassignedRequests = instance.GetUnassignedRequests();
                for (int i = 0; i < unassignedRequests.Count; i++)
                {
                    var request = unassignedRequests[i];
                    if (schedule.ServedRequestsContains(request.Id))
                    {
                        column = column.And(cplex.Column(requestConstraints[i], 1.0));
                    }
                }
                foreach (var branchingConstraint in branchingConstraints)
                {
                    if (constraintInfoMap.TryGetValue(branchingConstraint, out var constraintInfo))
                    {
                        bool affectsSchedule = false;

                        if (schedule.ServedRequestsContains(constraintInfo.RequestIndex))
                        {
                            if (constraintInfo.IsAssigned && constraintInfo.ElevatorGroup.Contains(elevatorIndex))
                            {
                                column = column.And(cplex.Column(branchingConstraint, 1.0));
                                affectsSchedule = true;
                            }
                            else if (!constraintInfo.IsAssigned && !constraintInfo.ElevatorGroup.Contains(elevatorIndex))
                            {
                                column = column.And(cplex.Column(branchingConstraint, 1.0));
                                affectsSchedule = true;
                            }
                        }

                        if (!affectsSchedule)
                        {
                            column = column.And(cplex.Column(branchingConstraint, 0.0));
                        }
                    }
                }
                // שינוי מ-NumVarType.Bool ל-NumVarType.Float לקבלת רלקסציה לינארית
                INumVar var = cplex.NumVar(column, 0, 1, NumVarType.Float);
                scheduleVars.Add(var);
            }
            catch (ILOG.Concert.Exception ex)
            {
                Console.WriteLine("Error adding schedule: " + ex.Message);
                throw;
            }
        }

        public void AddBranchingConstraint(int requestIndex, List<int> elevatorGroup, bool assign)
        {
            try
            {
                ILinearNumExpr expr = cplex.LinearNumExpr();
                for (int i = 0; i < scheduleVars.Count; i++)
                {
                    Schedule schedule = schedules.GetSchedule(i);
                    int elevatorIndex = schedule.ElevatorIndex;
                    if (schedule.ServedRequestsContains(requestIndex) && elevatorGroup.Contains(elevatorIndex))
                    {
                        expr.AddTerm(1.0, scheduleVars[i]);
                    }
                }
                IRange constraint;
                if (assign)
                {
                    constraint = cplex.AddEq(expr, 1.0);
                }
                else
                {
                    constraint = cplex.AddEq(expr, 0.0);
                }

                // שמירת המידע על האילוץ
                BranchingConstraintInfo constraintInfo = new BranchingConstraintInfo(requestIndex, elevatorGroup, assign);
                constraintInfoMap[constraint] = constraintInfo;

                branchingConstraints.Add(constraint);
            }
            catch (ILOG.Concert.Exception ex)
            {
                Console.WriteLine("Error adding branching constraint: " + ex.Message);
                throw;
            }
        }

        public void RemoveBranchingConstraint()
        {
            if (branchingConstraints.Count > 0)
            {
                try
                {
                    var constraint = branchingConstraints[branchingConstraints.Count - 1];
                    cplex.Remove(constraint);

                    // הסרת המידע מהמפה
                    if (constraintInfoMap.ContainsKey(constraint))
                    {
                        constraintInfoMap.Remove(constraint);
                    }

                    branchingConstraints.RemoveAt(branchingConstraints.Count - 1);
                }
                catch (ILOG.Concert.Exception ex)
                {
                    Console.WriteLine("Error removing branching constraint: " + ex.Message);
                    throw;
                }
            }
        }

        public Solution Solve()
        {
            try
            {
                if (cplex.Solve())
                {
                    double[] values = cplex.GetValues(scheduleVars.ToArray());
                    double objValue = cplex.GetObjValue();

                    double[] requestDuals = cplex.GetDuals(requestConstraints);
                    double[] elevatorDuals = cplex.GetDuals(elevatorConstraints);

                    return new Solution(values, schedules, objValue, requestDuals, elevatorDuals);
                }
                return null;
            }
            catch (ILOG.Concert.Exception ex)
            {
                Console.WriteLine("Error solving model: " + ex.Message);
                throw;
            }
        }

        public double[] GetRequestDualPrices()
        {
            try
            {
                return cplex.GetDuals(requestConstraints);
            }
            catch (ILOG.Concert.Exception ex)
            {
                Console.WriteLine("Error getting dual prices: " + ex.Message);
                throw;
            }
        }

        public double[] GetElevatorDualPrices()
        {
            try
            {
                return cplex.GetDuals(elevatorConstraints);
            }
            catch (ILOG.Concert.Exception ex)
            {
                Console.WriteLine("Error getting dual prices: " + ex.Message);
                throw;
            }
        }

        public ScheduleCollection Schedules
        {
            get { return schedules; }
        }

        public ProblemInstance Instance
        {
            get { return instance; }
        }

        public void End()
        {
            try
            {
                cplex.End();
            }
            catch (ILOG.Concert.Exception ex)
            {
                Console.WriteLine("Error ending cplex: " + ex.Message);
            }
        }
    }
}