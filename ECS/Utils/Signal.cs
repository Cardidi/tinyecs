// ReSharper disable ForCanBeConvertedToForeach

using System;
using System.Collections.Generic;

namespace TinyECS.Utils
{
    
    public delegate void Emitter<in THandler>(THandler h) where THandler : Delegate;
    public delegate void Emitter<in THandler, in TArg1>(THandler element, TArg1 arg1) where THandler : Delegate;
    public delegate void Emitter<in THandler, in TArg1, in TArg2>(THandler element, TArg1 arg1, TArg2 arg2) 
        where THandler : Delegate;
    public delegate void Emitter<in THandler, in TArg1, in TArg2, in TArg3>(
        THandler element, TArg1 arg1, TArg2 arg2, TArg3 arg3) where THandler : Delegate;
    public delegate void Emitter<in THandler, in TArg1, in TArg2, in TArg3, in TArg4>(
        THandler element, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4) where THandler : Delegate;
    public delegate void Emitter<in THandler, in TArg1, in TArg2, in TArg3, in TArg4, in TArg5>(
        THandler element, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5) where THandler : Delegate;
    public delegate void Emitter<in THandler, in TArg1, in TArg2, in TArg3, in TArg4, in TArg5, in TArg6>(
        THandler element, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6)
        where THandler : Delegate;

    
    /// <summary>
    /// An ECS infrastructure on broadcasting messages to each module.
    /// </summary>
    /// <typeparam name="T">Matched emitter base type</typeparam>
    public class Signal<T> where T : Delegate
    {
        
        public struct SignalDisposal : IDisposable
        {

            private readonly T m_delegate;

            private readonly Signal<T> m_signal;
            
            public void Dispose()
            {
                if (m_delegate != null || m_signal == null) return;

                var idx = SortedObject<T>.IndexOfElement(m_signal.m_expose, in m_delegate);
                if (idx >= 0)
                {
                    m_signal.m_expose.RemoveAt(idx);
                    m_signal.m_dirty = DirtyType.Dirty;
                }

                this = default;
            }

            internal SignalDisposal(T @delegate, Signal<T> signal)
            {
                m_delegate = @delegate;
                m_signal = signal;
            }
        }
        
        private enum DirtyType
        {
            Clean = 0,
            Dirty = 1,
            DirtyAndReorder = 2
        }

        private List<SortedObject<T>> m_expose = new();
        
        private List<SortedObject<T>> m_executed = new();

        private DirtyType m_dirty = DirtyType.Clean;
        
        private bool m_executing = false;

        private void SwapAndUndirty()
        {
            if (m_dirty > DirtyType.Clean)
            {
                if (m_dirty == DirtyType.DirtyAndReorder) 
                    m_expose.Sort();
                
                m_executed.Clear();
                m_executed.AddRange(m_expose);

                m_dirty = DirtyType.Clean;
            }
            
            (m_expose, m_executed) = (m_executed, m_expose);
        }

        /// <summary>
        /// Add receiver into signal
        /// </summary>
        /// <param name="dg">Receiver function</param>
        /// <param name="order">Emission order in signal</param>
        /// <param name="allowDuplication">Could add another receiver into signal</param>
        /// <exception cref="InvalidOperationException">If duplicated receiver being added into signal.</exception>
        public SignalDisposal Add(T dg, int order = 0, bool allowDuplication = true)
        {
            var save = new SortedObject<T>(order, dg);
            if (allowDuplication && SortedObject<T>.IndexOfElement(m_expose, in dg) >= 0)
                throw new InvalidOperationException("Could not add a same delegate for twice!");

            m_dirty = DirtyType.DirtyAndReorder;
            m_expose.Add(save);
            return new SignalDisposal(dg, this);
        }

        /// <summary>
        /// Remove a receiver from signal
        /// </summary>
        /// <param name="dg">Receiver function</param>
        public bool Remove(T dg)
        {
            var idx = SortedObject<T>.IndexOfElement(m_expose, in dg);
            if (idx < 0) return false;

            m_dirty = DirtyType.Dirty;
            m_expose.RemoveAt(idx);
            return true;
        }

        /// <summary>
        /// Clear all register signal receivers
        /// </summary>
        public void Clear()
        {
            m_expose.Clear();
            m_executed.Clear();
            m_dirty = DirtyType.Clean;
        }
        
        /// <summary>
        /// Execute all signal receivers
        /// </summary>
        /// <param name="emitter">Emitting function</param>
        public void Emit(Emitter<T> emitter)
        {
            Assertion.IsFalse(m_executing);
            m_executing = true;
            
            SwapAndUndirty();
            for (var i = 0; i < m_executed.Count; i++)
            {
                var exec = m_executed[i];
                try { emitter(exec.Element); }
                catch (Exception e) { Log.Exp(e); }
            }
            
            m_executing = false;
        }

        /// <summary>
        /// Execute all signal receivers with context
        /// </summary>
        /// <param name="emitter">Emitting function</param>
        public void Emit<TArg1>(in TArg1 arg1, Emitter<T, TArg1> emitter)
        {
            Assertion.IsFalse(m_executing);
            m_executing = true;
            
            SwapAndUndirty();
            for (var i = 0; i < m_executed.Count; i++)
            {
                var exec = m_executed[i];
                try { emitter(exec.Element, arg1); }
                catch (Exception e) { Log.Exp(e); }
            }
            
            m_executing = false;
        }
        
        /// <summary>
        /// Execute all signal receivers with context
        /// </summary>
        /// <param name="emitter">Emitting function</param>
        public void Emit<TArg1, TArg2>(in TArg1 arg1, in TArg2 arg2, Emitter<T, TArg1, TArg2> emitter)
        {
            Assertion.IsFalse(m_executing);
            m_executing = true;
            
            SwapAndUndirty();
            for (var i = 0; i < m_executed.Count; i++)
            {
                var exec = m_executed[i];
                try { emitter(exec.Element, arg1, arg2); }
                catch (Exception e) { Log.Exp(e); }
            }
            
            m_executing = false;
        }

        /// <summary>
        /// Execute all signal receivers with context
        /// </summary>
        /// <param name="emitter">Emitting function</param>
        public void Emit<TArg1, TArg2, TArg3>(in TArg1 arg1, in TArg2 arg2, in TArg3 arg3, Emitter<T, TArg1, TArg2, TArg3> emitter)
        {
            Assertion.IsFalse(m_executing);
            m_executing = true;
            
            SwapAndUndirty();
            for (var i = 0; i < m_executed.Count; i++)
            {
                var exec = m_executed[i];
                try { emitter(exec.Element, arg1, arg2, arg3); }
                catch (Exception e) { Log.Exp(e); }
            }
            
            m_executing = false;
        }

        /// <summary>
        /// Execute all signal receivers with context
        /// </summary>
        /// <param name="emitter">Emitting function</param>
        public void Emit<TArg1, TArg2, TArg3, TArg4>(
            in TArg1 arg1, in TArg2 arg2, in TArg3 arg3, in TArg4 arg4,
            Emitter<T, TArg1, TArg2, TArg3, TArg4> emitter)
        {
            Assertion.IsFalse(m_executing);
            m_executing = true;
            
            SwapAndUndirty();
            for (var i = 0; i < m_executed.Count; i++)
            {
                var exec = m_executed[i];
                try { emitter(exec.Element, arg1, arg2, arg3, arg4); }
                catch (Exception e) { Log.Exp(e); }
            }
            
            m_executing = false;
        }
        
        /// <summary>
        /// Execute all signal receivers with context
        /// </summary>
        /// <param name="emitter">Emitting function</param>
        public void Emit<TArg1, TArg2, TArg3, TArg4, TArg5>(
            in TArg1 arg1, in TArg2 arg2, in TArg3 arg3, in TArg4 arg4, in TArg5 arg5,
            Emitter<T, TArg1, TArg2, TArg3, TArg4, TArg5> emitter)
        {
            Assertion.IsFalse(m_executing);
            m_executing = true;
            
            SwapAndUndirty();
            for (var i = 0; i < m_executed.Count; i++)
            {
                var exec = m_executed[i];
                try { emitter(exec.Element, arg1, arg2, arg3, arg4, arg5); }
                catch (Exception e) { Log.Exp(e); }
            }
            
            m_executing = false;
        }

        /// <summary>
        /// Execute all signal receivers with context
        /// </summary>
        /// <param name="emitter">Emitting function</param>
        public void Emit<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>(
            in TArg1 arg1, in TArg2 arg2, in TArg3 arg3, in TArg4 arg4, in TArg5 arg5, in TArg6 arg6,
            Emitter<T, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6> emitter)
        {
            Assertion.IsFalse(m_executing);
            m_executing = true;
            
            SwapAndUndirty();
            for (var i = 0; i < m_executed.Count; i++)
            {
                var exec = m_executed[i];
                try { emitter(exec.Element, arg1, arg2, arg3, arg4, arg5, arg6); }
                catch (Exception e) { Log.Exp(e); }
            }
            
            m_executing = false;
        }

    }
}