using System;
using System.Collections.Generic;
using ReGoap.Core;

namespace ReGoap.Planner
{
    public struct ReGoapPlanWork<T, W>
    {
        public readonly IReGoapAgent<T, W> Agent;
        public readonly IReGoapGoal<T, W> BlacklistGoal;
        public readonly Queue<ReGoapActionState<T, W>> Actions;
        public readonly Action<IReGoapGoal<T, W>> Callback;

        public IReGoapGoal<T, W> NewGoal;

        public ReGoapPlanWork(IReGoapAgent<T, W> agent, IReGoapGoal<T, W> blacklistGoal, Queue<ReGoapActionState<T, W>> actions, Action<IReGoapGoal<T, W>> callback) : this()
        {
            Agent = agent;
            BlacklistGoal = blacklistGoal;
            Actions = actions;
            Callback = callback;
        }
    }

    public class ReGoapPlanner<T, W> : IGoapPlanner<T, W>
    {
        private IReGoapAgent<T, W> goapAgent;
        private IReGoapGoal<T, W> currentGoal;
        public bool Calculated;
        private readonly AStar<ReGoapState<T, W>> astar;
        private readonly ReGoapPlannerSettings settings;

        public ReGoapPlanner(ReGoapPlannerSettings settings = null)
        {
            this.settings = settings ?? new ReGoapPlannerSettings();
            astar = new AStar<ReGoapState<T, W>>(this.settings.MaxNodesToExpand);
        }

        public IReGoapGoal<T, W> Plan(IReGoapAgent<T, W> agent, IReGoapGoal<T, W> blacklistGoal = null, Queue<ReGoapActionState<T, W>> currentPlan = null, Action<IReGoapGoal<T, W>> callback = null)
        {
            goapAgent = agent;
            Calculated = false;
            currentGoal = null;
            var possibleGoals = new List<IReGoapGoal<T, W>>();
            foreach (var goal in goapAgent.GetGoalsSet())
            {
                if (goal == blacklistGoal)
                    continue;
                goal.Precalculations(this);
                if (goal.IsGoalPossible())
                    possibleGoals.Add(goal);
            }
            possibleGoals.Sort((x, y) => x.GetPriority().CompareTo(y.GetPriority()));

            var currentState = agent.GetMemory().GetWorldState();

            while (possibleGoals.Count > 0)
            {
                currentGoal = possibleGoals[possibleGoals.Count - 1];
                possibleGoals.RemoveAt(possibleGoals.Count - 1);
                var goalState = currentGoal.GetGoalState();

                // can't work with dynamic actions, of course
                if (!settings.UsingDynamicActions)
                {
                    var wantedGoalCheck = currentGoal.GetGoalState();
                    GoapActionStackData<T, W> stackData;
                    stackData.agent = goapAgent;
                    stackData.currentState = currentState;
                    stackData.goalState = goalState;
                    stackData.next = null;
                    stackData.settings = null;
                    // we check if the goal can be archived through actions first, so we don't brute force it with A* if we can't
                    foreach (var action in goapAgent.GetActionsSet())
                    {
                        action.Precalculations(stackData);
                        if (!action.CheckProceduralCondition(stackData))
                        {
                            continue;
                        }
                        // check if the effects of all actions can archieve currentGoal
                        var previous = wantedGoalCheck;
                        wantedGoalCheck = ReGoapState<T, W>.Instantiate();
                        previous.MissingDifference(action.GetEffects(stackData), ref wantedGoalCheck);
                    }
                    // finally push the current world state
                    var current = wantedGoalCheck;
                    wantedGoalCheck = ReGoapState<T, W>.Instantiate();
                    current.MissingDifference(GetCurrentAgent().GetMemory().GetWorldState(), ref wantedGoalCheck);
                    // can't validate goal 
                    if (wantedGoalCheck.Count > 0)
                    {
                        currentGoal = null;
                        continue;
                    }
                }

                goalState = goalState.Clone();
                var leaf = (ReGoapNode<T, W>)astar.Run(
                    ReGoapNode<T, W>.Instantiate(this, goalState, null, null, null), goalState, settings.MaxIterations, settings.PlanningEarlyExit);
                if (leaf == null)
                {
                    currentGoal = null;
                    continue;
                }

                var result = leaf.CalculatePath();
                if (currentPlan != null && currentPlan == result)
                {
                    currentGoal = null;
                    break;
                }
                if (result.Count == 0)
                {
                    currentGoal = null;
                    continue;
                }
                currentGoal.SetPlan(result);
                break;
            }
            Calculated = true;

            if (callback != null)
                callback(currentGoal);
            return currentGoal;
        }

        public IReGoapGoal<T, W> GetCurrentGoal()
        {
            return currentGoal;
        }

        public IReGoapAgent<T, W> GetCurrentAgent()
        {
            return goapAgent;
        }

        public bool IsPlanning()
        {
            return !Calculated;
        }

        public ReGoapPlannerSettings GetSettings()
        {
            return settings;
        }
    }
}