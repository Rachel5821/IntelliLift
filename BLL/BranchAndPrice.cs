using Microsoft.VisualBasic;

namespace BLL
{
    public class BranchAndPrice
    {
        //private int ColumnGeneration(MasterModel masterModel, PartialSolution partialSolutions)
        //{

        //}
        //        private static ProblemSolutionMM ColumnGeneration(MasterModel masterModel, PartialSolution partialSolutions) throws IloException, IOException {
        //        long startTime;
        //        ProblemSolutionMM problemSolutionMM;
        //        ProblemSolutionSP problemSolutionSP;
        //        int device = -1;
        //        int startDevice;
        //        int iteration = 0;
        //        lastTimeInLag = 0;

        //        while(true) {
        //            // Create columns for each device consequently
        //            device = (device + 1) % problemInstance.getNumbClients();

        //            while (true) {
        //                timesInMaster++;
        //                startTime = System.currentTimeMillis();
        //                //masterModel.ModelToFile("out.lp");
        //                problemSolutionMM = masterModel.Solve(partialSolutions);
        //                timeInMaster += (System.currentTimeMillis() - startTime);
        //                iteration++;

        //                int result = CreateNewColumns(masterModel, partialSolutions, problemSolutionMM);
        //                if(result == -1) return null;
        //                if(result == 0)  break;
        //            }

        //    startDevice = device;
        //            while(true) {
        //                subModel[device].ChangeCoefficientsInSubModel(problemSolutionMM.getDuals1(), partialSolutions, 1, 0);
        //    startTime = System.currentTimeMillis();
        //                problemSolutionSP = subModel[device].Solve();

        //    timeInSub += (System.currentTimeMillis() - startTime);
        //                //timeInSubs[timesInSub] = System.currentTimeMillis() - startTime;
        //                timesInSub++;

        //                if(problemSolutionSP == null) {
        //                    return null;
        //                }

        //if (problemSolutionSP.IsReducedCostPositive(problemSolutionMM.getDuals2()[device], Helpers.EPS))
        //{
        //    masterModel.AddColumnToSetOfColumnsAndModel(problemSolutionSP.ConvertResults(), device);

        //    //try to solve it not to optimality, did not work
        //    /*for(int i = 0; i < NUMB_SOLUTIONS_TO_SUB_MODEL; i++) {
        //        if(problemSolutionSPMany[i] != null && (Math.abs(problemSolutionSPMany[i].getObjValue() - problemSolutionSPMany[0].getObjValue()) < 0.15)){
        //            if(!Heuristic.LatencyControlWithoutDominating(problemInstance.ToSingleDevice(device), problemSolutionSPMany[i].getX(), timeInLevels, true))
        //                startTime = 0;
        //            masterModel.AddColumnToSetOfColumnsAndModel(problemSolutionSPMany[0].ConvertResults(), device);
        //        }
        //        else{
        //            break;
        //        }
        //    }*/
        //    break;
        //}
        //else
        //{
        //    //at this point the solution to the sub-model must be optimal
        //    /*subModel[device].ChangeCoefficientsInSubModel(problemSolutionMM.getDuals1(), partialSolutions, 1, 0);
        //    problemSolutionSP = subModel[device].Solve();
        //    if(problemSolutionSP.IsReducedCostPositive(problemSolutionMM.getDuals2()[device], Helpers.EPS)){
        //        masterModel.AddColumnToSetOfColumnsAndModel(problemSolutionSP.ConvertResults(), device);
        //        break;
        //    }
        //    else{*/
        //    device = (device + 1) % problemInstance.getNumbClients();
        //    if (device == startDevice)
        //    {
        //        if (numbNodes == 0) LB = problemSolutionMM.getObjValue();
        //        return problemSolutionMM;
        //    }
        //    //}
        //}
        //            }

        //            //Lagrangian relaxation
        //            if (doLR)
        //{
        //    int result = LagrangeRelaxation(problemSolutionMM, problemSolutionSP, iteration, device, partialSolutions, subModel);
        //    if (result == 0) return null;
        //    if (result == 1) return problemSolutionMM;
        //}
        //        }
        //    }

        //    }
    }
}
