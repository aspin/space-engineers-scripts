using System;
using System.Collections.Generic;
using System.Text;

namespace Mixins
{
    public interface IActionState
    {
        bool IsComplete();
        string Error();
    }

    public abstract class StateMachine<T, R> where R : IActionState
    {

        bool isActive = false;
        T actionInProgress;
        readonly Dictionary<T, ActionHandler<R>> handlers;
        public readonly R State;

        public StateMachine(Dictionary<T, ActionHandler<R>> handlers, R state)
        {
            this.handlers = handlers;
            this.State = state;

            actionInProgress = EmptyAction();

            foreach (T action in Actions())
            {
                if (!this.handlers.ContainsKey(action))
                {
                    throw new ArgumentException(string.Format("not all actions were  implemented: {0}", action));
                }
            }
        }

        public void Process(T action)
        {
            if (!isActive || CanOverride(action))
            {
                OnNewAction(action);
            }
            else
            {
                OnContinueAction();
            }

        }

        // returns if update needs to be toggled
        public bool OnNewAction(T action)
        {
            // short circuit if state object is not initialized correctly
            if (!State.IsComplete())
            {
                // TODO: custom type?
                throw new Exception("state invalid");
            }

            // short circuit if trying to override another action with non-overrideable
            if (isActive && !CanOverride(action))
            {
                return true;
            }

            actionInProgress = action;

            ActionHandler<R> handler = this.handlers[action];
            isActive = !handler.Start(State);

            UpdateStatus();
            return isActive;
        }

        public bool OnContinueAction()
        {
            ActionHandler<R> handler = this.handlers[actionInProgress];
            isActive = !handler.Handle(State);

            UpdateStatus();
            return isActive;
        }

        private void UpdateStatus()
        {
            if (!isActive)
            {
                actionInProgress = EmptyAction();
            }
        }

        // indicates if actions can override other methods
        public abstract bool CanOverride(T action);

        public abstract T[] Actions();

        public abstract T EmptyAction();


    }

    public delegate int ActionStageHandler<T>(int stage, T state) where T : IActionState;

    public abstract class ActionHandler<T> where T : IActionState
    {
        readonly Dictionary<int, ActionStageHandler<T>> handlers;
        int stage;


        protected ActionHandler()
        {
            handlers = Handlers();
        }

        protected abstract Dictionary<int, ActionStageHandler<T>> Handlers();

        internal bool Start(T state)
        {
            stage = 0;
            return Handle(state);
        }

        internal bool Handle(T state)
        {
            stage = handlers[stage](stage, state);
            return stage == -1;
        }
    }
}
