using System;
using System.Collections.Generic;
using Godot;
using ReGoap.Core;

namespace ReGoap.Godot {
    public abstract class GoapAction<T, W> : IReGoapAction<T, W> {
        public float Cost = 1;
        protected Action<IReGoapAction<T, W>> doneCallback;
        protected Action<IReGoapAction<T, W>> failCallback;
        protected IReGoapAction<T, W> previousAction;
        protected IReGoapAction<T, W> nextAction;

        protected bool interruptWhenPossible;

        protected String Name;

        public GoapAction(String name)
        {
            this.Name = name;
        }

        public string GetName() {
            return this.Name;
        }

        public bool IsActive() {
            return (true);
        }

        public virtual bool Validate(GoapActionStackData<T, W> stackData) {
            return this.GetPreconditions(stackData).MissingDifference(stackData.currentState) > 0;
        }

        public virtual void PostPlanCalculations(IReGoapAgent<T, W> goapAgent)
        {
        }

        public virtual bool IsInterruptable()
        {
            return true;
        }

        public virtual void AskForInterruption()
        {
            interruptWhenPossible = true;
        }

        public virtual void Precalculations(GoapActionStackData<T, W> stackData)
        {
        }

        public virtual List<ReGoapState<T, W>> GetSettings(GoapActionStackData<T, W> stackData)
        {
            if (stackData.settings != null) {
                return new List<ReGoapState<T, W>> { stackData.settings };
            } else {
                return new List<ReGoapState<T, W>> { ReGoapState<T, W>.Instantiate() };
            }
        }

        public virtual ReGoapState<T, W> GetPreconditions(GoapActionStackData<T, W> stackData) {
            return ReGoapState<T, W>.Instantiate();
        }

        public abstract ReGoapState<T, W> GetEffects(GoapActionStackData<T, W> stackData);

        public virtual float GetCost(GoapActionStackData<T, W> stackData)
        {
            return Cost;
        }

        public virtual bool CheckProceduralCondition(GoapActionStackData<T, W> stackData)
        {
            return true;
        }

        public virtual void Tick(ReGoapState<T, W> settings, ReGoapState<T, W> goalState) {
            // GD.Print("Tick: ", Name);
        }

        public virtual void Run(IReGoapAction<T, W> previous, IReGoapAction<T, W> next, ReGoapState<T, W> settings,
            ReGoapState<T, W> goalState, Action<IReGoapAction<T, W>> done, Action<IReGoapAction<T, W>> fail)
        {
            // GD.Print("Run: ", Name);
            interruptWhenPossible = false;
            doneCallback = done;
            failCallback = fail;
            previousAction = previous;
            nextAction = next;
        }

        public virtual void PlanEnter(IReGoapAction<T, W> previousAction, IReGoapAction<T, W> nextAction, ReGoapState<T, W> settings, ReGoapState<T, W> goalState)
        {
        }

        public virtual void PlanExit(IReGoapAction<T, W> previousAction, IReGoapAction<T, W> nextAction, ReGoapState<T, W> settings, ReGoapState<T, W> goalState)
        {
        }

        public virtual void Exit(IReGoapAction<T, W> next)
        {
            // GD.Print("Exit: ", Name);
        }

        public override string ToString()
        {
            return string.Format("GoapAction('{0}')", Name);
        }

        public virtual string ToString(GoapActionStackData<T, W> stackData)
        {
            string result = string.Format("GoapAction('{0}')", Name);
            if (stackData.settings != null && stackData.settings.Count > 0)
            {
                result += " - ";
                foreach (var pair in stackData.settings.GetValues())
                {
                    result += string.Format("{0}='{1}' ; ", pair.Key, pair.Value);
                }
            }
            return result;
        }
    }
}