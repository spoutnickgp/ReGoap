using System.Linq;
using System;
using System.Collections.Generic;
using ReGoap.Core;
using ReGoap.Planner;
using GodotBase = Godot;

namespace ReGoap.Godot
{
    public abstract class GoapAgent<T, W> : GodotBase.Object, IReGoapAgent<T, W>, IReGoapAgentHelper {

        public bool Enabled {
            get { return this._isEnabled; }
            set {
                this._isEnabled = value;
                if (value) this.OnEnable(); else this.OnDisable();
            }
        }
        public float CalculationDelay = 0.5f;
        public bool BlackListGoalOnFailure;
        public bool IsPlanning
        {
            get { return startedPlanning; }
        }

        protected ReGoapState<T, W> state;
        protected float lastCalculationTime;
        protected IReGoapGoal<T, W> currentGoal;
        protected ReGoapActionState<T, W> currentActionState;
        protected Dictionary<IReGoapGoal<T, W>, float> goalBlacklist;
        protected List<IReGoapGoal<T, W>> possibleGoals;
        protected bool possibleGoalsDirty;
        protected List<ReGoapActionState<T, W>> startingPlan;
        protected Dictionary<T, W> planValues;
        protected bool interruptOnNextTransition;
        protected bool startedPlanning;

        protected List<IReGoapGoal<T, W>> goals;
        protected List<IReGoapAction<T, W>> actions;
        private bool _isEnabled = false;
        
        public GoapAgent() {
            this.lastCalculationTime = -100;
            this.goalBlacklist = new Dictionary<IReGoapGoal<T, W>, float>();
            this.goals = new List<IReGoapGoal<T, W>>();
            this.actions = new List<IReGoapAction<T, W>>();
        }

        public virtual void Update(float delta) {
            if (this.Enabled) {
                if (currentGoal == null) {
                    CalculateNewGoal();
                }
                if (currentActionState != null && currentGoal != null) {
                    currentActionState.Action.Tick(currentActionState.Settings, currentGoal.GetGoalState());
                }
            }
        }

        protected virtual void OnEnable() {
           
        }

        protected virtual void OnDisable() {
            if (currentActionState != null)
            {
                currentActionState.Action.Exit(null);
                currentActionState = null;
                currentGoal = null;
            }
        }

        protected virtual void UpdatePossibleGoals() {
            possibleGoalsDirty = false;
            if (goalBlacklist.Count > 0)
            {
                possibleGoals = new List<IReGoapGoal<T, W>>(goals.Count);
                foreach (var goal in goals)
                    if (!goalBlacklist.ContainsKey(goal))
                    {
                        possibleGoals.Add(goal);
                    }
                    else if (goalBlacklist[goal] < GodotBase.OS.GetTicksMsec())
                    {
                        goalBlacklist.Remove(goal);
                        possibleGoals.Add(goal);
                    }
            }
            else
            {
                possibleGoals = goals;
            }
        }

        protected virtual void TryWarnActionFailure(IReGoapAction<T, W> action) {
            if (action.IsInterruptable())
                WarnActionFailure(action);
            else
                action.AskForInterruption();
        }

        protected virtual bool CalculateNewGoal(bool forceStart = false) {
            if (IsPlanning)
                return false;
            if (!forceStart && (GodotBase.OS.GetTicksMsec() - lastCalculationTime <= CalculationDelay))
                return false;

            lastCalculationTime = GodotBase.OS.GetTicksMsec();
            interruptOnNextTransition = false;
            UpdatePossibleGoals();
            startedPlanning = true;
            this.GetPlanner().Plan(this, BlackListGoalOnFailure ? currentGoal : null, currentGoal != null ? currentGoal.GetPlan() : null, OnDonePlanning);
            return true;
        }

        protected virtual void OnDonePlanning(IReGoapGoal<T, W> newGoal) {
            startedPlanning = false;
            
            if (currentActionState != null)
                currentActionState.Action.Exit(null);
            currentActionState = null;
            currentGoal = newGoal;
            if (currentGoal == null)
            {
                GodotBase.GD.PrintErr("GoapAgent " + this + " could not find a plan.");
                return;
            }
            if (startingPlan != null)
            {
                for (int i = 0; i < startingPlan.Count; i++)
                {
                    startingPlan[i].Action.PlanExit(i > 0 ? startingPlan[i - 1].Action : null, i + 1 < startingPlan.Count ? startingPlan[i + 1].Action : null, startingPlan[i].Settings, currentGoal.GetGoalState());
                }
            }
            startingPlan = currentGoal.GetPlan().ToList();
            ClearPlanValues();
            for (int i = 0; i < startingPlan.Count; i++)
            {
                startingPlan[i].Action.PlanEnter(i > 0 ? startingPlan[i - 1].Action : null, i + 1 < startingPlan.Count ? startingPlan[i + 1].Action : null, startingPlan[i].Settings, currentGoal.GetGoalState());
            }
            currentGoal.Run(WarnGoalEnd);
            PushAction();
        }

        public static string PlanToString(IEnumerable<IReGoapAction<T, W>> plan) {
            var result = "GoapPlan(";
            var reGoapActions = plan as IReGoapAction<T, W>[] ?? plan.ToArray();
            for (var index = 0; index < reGoapActions.Length; index++)
            {
                var action = reGoapActions[index];
                result += string.Format("'{0}'{1}", action, index + 1 < reGoapActions.Length ? ", " : "");
            }
            result += ")";
            return result;
        }

        public virtual void WarnActionEnd(IReGoapAction<T, W> thisAction) {
            if (thisAction != currentActionState.Action)
                return;
            PushAction();
        }

        public virtual void WarnActionFailure(IReGoapAction<T, W> thisAction) {
            if (currentActionState != null && thisAction != currentActionState.Action)
            {
                GodotBase.GD.PrintErr(string.Format("[GoapAgent] Action {0} warned for failure but is not current action.", thisAction));
                return;
            }
            if (BlackListGoalOnFailure)
                goalBlacklist[currentGoal] = GodotBase.OS.GetTicksMsec() + currentGoal.GetErrorDelay();
            this.currentGoal = null;
            // CalculateNewGoal(true);
        }

        public virtual void WarnGoalEnd(IReGoapGoal<T, W> goal) {
            if (goal != currentGoal)
            {
                GodotBase.GD.PrintErr(string.Format("[GoapAgent] Goal {0} warned for end but is not current goal.", goal));
                return;
            }
            CalculateNewGoal();
        }

        public virtual void WarnPossibleGoal(IReGoapGoal<T, W> goal) {
            if ((currentGoal != null) && (goal.GetPriority() <= currentGoal.GetPriority()))
                return;
            if (currentActionState != null && !currentActionState.Action.IsInterruptable())
            {
                interruptOnNextTransition = true;
                currentActionState.Action.AskForInterruption();
            }
            else
                CalculateNewGoal();
        }

        protected virtual void PushAction() {
            if (interruptOnNextTransition)
            {
                CalculateNewGoal();
                return;
            }
            var plan = currentGoal.GetPlan();
            if (plan.Count == 0)
            {
                if (currentActionState != null)
                {
                    currentActionState.Action.Exit(currentActionState.Action);
                    currentActionState = null;
                }
                CalculateNewGoal();
            }
            else
            {
                var previous = currentActionState;
                currentActionState = plan.Dequeue();
                IReGoapAction<T, W> next = null;
                if (plan.Count > 0)
                    next = plan.Peek().Action;
                if (previous != null)
                    previous.Action.Exit(currentActionState.Action);
                currentActionState.Action.Run(previous != null ? previous.Action : null, next, currentActionState.Settings, currentGoal.GetGoalState(), WarnActionEnd, WarnActionFailure);
            }
        }

        public virtual bool IsActive() {
            return true;
        }

        public virtual List<ReGoapActionState<T, W>> GetStartingPlan() {
            return startingPlan;
        }

        protected virtual void ClearPlanValues() {
            if (planValues == null)
                planValues = new Dictionary<T, W>();
            else
            {
                planValues.Clear();
            }
        }

        public virtual W GetPlanValue(T key) {
            return planValues[key];
        }

        public virtual bool HasPlanValue(T key) {
            return planValues.ContainsKey(key);
        }

        public virtual void SetPlanValue(T key, W value) {
            planValues[key] = value;
        }

        public virtual List<IReGoapGoal<T, W>> GetGoalsSet() {
            if (this.possibleGoalsDirty) {
                this.UpdatePossibleGoals();
            }
            return possibleGoals;
        }

        public virtual List<IReGoapAction<T, W>> GetActionsSet() {
            return actions;
        }

        public virtual IReGoapGoal<T, W> GetCurrentGoal() {
            return currentGoal;
        }

        public virtual ReGoapState<T, W> InstantiateNewState() {
            return ReGoapState<T, W>.Instantiate();
        }

        // this only works if the ReGoapAgent has been inherited. For "special cases" you have to override this
        public virtual Type[] GetGenericArguments() {
            return GetType().BaseType.GetGenericArguments();
        }

        public abstract IReGoapMemory<T, W> GetMemory();

        protected abstract ReGoapPlanner<T, W> GetPlanner();
    }
}