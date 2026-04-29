using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Business.Workflow
{
    /// <summary>
    /// Generic, immutable state machine.
    /// Build once via <see cref="StateMachineBuilder{TState}"/> and reuse.
    /// </summary>
    public sealed class StateMachine<TState>
    {
        private readonly Dictionary<TState, HashSet<TState>> _transitions;
        private readonly HashSet<TState> _finalStates;
        private readonly IEqualityComparer<TState> _comparer;

        internal StateMachine(
            Dictionary<TState, HashSet<TState>> transitions,
            HashSet<TState> finalStates,
            IEqualityComparer<TState> comparer)
        {
            _transitions = transitions;
            _finalStates = finalStates;
            _comparer = comparer ?? EqualityComparer<TState>.Default;
        }

        public IReadOnlyCollection<TState> GetValidTransitions(TState current)
        {
            HashSet<TState> nexts;
            if (_transitions.TryGetValue(current, out nexts))
            {
                return nexts.ToList().AsReadOnly();
            }
            return new List<TState>().AsReadOnly();
        }

        public bool CanTransition(TState current, TState target)
        {
            HashSet<TState> nexts;
            return _transitions.TryGetValue(current, out nexts) && nexts.Contains(target);
        }

        public bool IsFinal(TState state) => _finalStates.Contains(state);

        /// <summary>
        /// Returns the new state on a valid transition; throws
        /// <see cref="InvalidOperationException"/> on an invalid one.
        /// </summary>
        public TState Transition(TState current, TState target)
        {
            if (!CanTransition(current, target))
            {
                var valid = GetValidTransitions(current);
                throw new InvalidOperationException(
                    $"Cannot transition from {current} to {target}. Valid: {(valid.Count == 0 ? "(none)" : string.Join(", ", valid))}");
            }
            return target;
        }
    }

    /// <summary>
    /// Fluent builder for <see cref="StateMachine{TState}"/>.
    /// </summary>
    public sealed class StateMachineBuilder<TState>
    {
        private readonly Dictionary<TState, HashSet<TState>> _transitions;
        private readonly HashSet<TState> _finalStates;
        private readonly IEqualityComparer<TState> _comparer;

        public StateMachineBuilder(IEqualityComparer<TState> comparer = null)
        {
            _comparer = comparer ?? EqualityComparer<TState>.Default;
            _transitions = new Dictionary<TState, HashSet<TState>>(_comparer);
            _finalStates = new HashSet<TState>(_comparer);
        }

        public StateMachineBuilder<TState> Allow(TState from, params TState[] tos)
        {
            if (tos == null || tos.Length == 0) return this;
            HashSet<TState> set;
            if (!_transitions.TryGetValue(from, out set))
            {
                set = new HashSet<TState>(_comparer);
                _transitions[from] = set;
            }
            foreach (var to in tos) set.Add(to);
            return this;
        }

        public StateMachineBuilder<TState> Final(params TState[] states)
        {
            if (states == null) return this;
            foreach (var s in states) _finalStates.Add(s);
            return this;
        }

        public StateMachine<TState> Build()
        {
            // Final states implicitly have no outbound transitions.
            foreach (var f in _finalStates)
            {
                if (!_transitions.ContainsKey(f))
                {
                    _transitions[f] = new HashSet<TState>(_comparer);
                }
            }
            return new StateMachine<TState>(_transitions, _finalStates, _comparer);
        }
    }
}
