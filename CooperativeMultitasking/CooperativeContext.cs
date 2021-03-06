using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CooperativeMultitasking
{
    public class CooperativeContext
    {
        public static void Run(params Func<ICooperativeBroker, ValueTask>[] tasks)
        {
            CooperativeContext context = new CooperativeContext(tasks.Length);
            foreach (var task in tasks)
            {
                task(context.CreateBroker());
            }

            //It goes here when one of the tasks is finished but we need to keep the rest of the task running
            while (context._brokers.Count > 0)
            {
                context.ReleaseFirstFinishedBrokerAndInvokeNext();
            }
        }

        private readonly List<CooperativeBroker> _brokers = new List<CooperativeBroker>();

        private readonly int _threadId;

        private int _targetBrokersCount;

        private CooperativeContext(int maxCooperation)
        {
            this._threadId = Thread.CurrentThread.ManagedThreadId;
            this._targetBrokersCount = maxCooperation;
        }

        private CooperativeBroker CreateBroker()
        {
            var cooperativeBroker = new CooperativeBroker(this);
            this._brokers.Add(cooperativeBroker);
            return cooperativeBroker;
        }

        private void ReleaseFirstFinishedBrokerAndInvokeNext()
        {
            //IsNoAction means that the async method is finished
            var completedBroker = this._brokers.Find(i => i.IsNoAction)!;

            var index = this._brokers.IndexOf(completedBroker);
            this._brokers.RemoveAt(index);
            this._targetBrokersCount--;

            if (index == this._brokers.Count)
            {
                index = 0;
            }

            if (this._brokers.Count > 0)
            {
                this._brokers[index].InvokeContinuation();
            }
        }

        private void OnCompleted(CooperativeBroker broker)
        {
            if (this._threadId != Thread.CurrentThread.ManagedThreadId)
            {
                //That means that a real async action was used inside a work process
                //And this means that CooperativeContext is pointless
                broker.InvokeContinuation();
                return;
            }

            //It skips continuation invocations until all brokers are created.
            if (this._targetBrokersCount == this._brokers.Count)
            {
                var nextIndex = this._brokers.IndexOf(broker) + 1;
                if (nextIndex == this._brokers.Count)
                {
                    nextIndex = 0;
                }

                this._brokers[nextIndex].InvokeContinuation();
            }
        }

        private class CooperativeBroker : ICooperativeBroker
        {
            private readonly CooperativeContext _cooperativeContext;

            private Action? _continuation;

            public CooperativeBroker(CooperativeContext cooperativeContext)
                => this._cooperativeContext = cooperativeContext;

            public bool IsNoAction
                => this._continuation == null;

            public void GetResult() 
                => this._continuation = null;

            public bool IsCompleted 
                => false;//Preventing sync completion in async method state machine

            public void OnCompleted(Action continuation)
            {
                this._continuation = continuation;
                this._cooperativeContext.OnCompleted(this);
            }

            public ICooperativeBroker GetAwaiter() 
                => this;

            public void InvokeContinuation() 
                => this._continuation?.Invoke();
        }
    }

    public interface ICooperativeBroker : INotifyCompletion
    {
        void GetResult();
        bool IsCompleted { get; }
        ICooperativeBroker GetAwaiter();
    }
}