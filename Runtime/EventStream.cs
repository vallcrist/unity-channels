using System;
using System.Collections.Generic;
using UnityEngine;

namespace BitCake.EventStreams
{
	public enum RegistryType
	{
		Persistent,
		RunOnce
	}

	public enum FetchHistory
	{
		Yes,
		No
	}

	public class ChannelRegister
	{
		public IDisposable disposer;
		public RegistryType registryType;

		public int triggerCount;
		
		public ChannelRegister(IDisposable disposer, RegistryType registryType)
		{
			this.disposer = disposer;
			this.registryType = registryType;
			this.triggerCount = 0;
		}

		public void IncreaseTriggerCount()
		{
			triggerCount++;
		}
	}

	public class EventStream<TArg> : IStream
	{
#if UNITY_EDITOR
		public static bool VerboseLogging = false;		
#endif
		public class Disposer : IDisposable
		{
			private IStream stream;
			private Context context;
			private Action<TArg> handler;
			private bool disposed = false;
			
			public Disposer(IStream stream, Context context, Action<TArg> handler)
			{
				this.stream = stream;
				this.context = context;
				this.handler = handler;
				disposed = false;
			}

			public void Dispose()
			{
				if (disposed)
				{
#if UNITY_EDITOR
					Debug.LogError($"[EventChannel][{stream.GetType()}] Disposing an EventChannel action more than once!");
#endif
					return;
				}
#if UNITY_EDITOR
				if (VerboseLogging)
					Debug.Log($"[EventChannel][{stream.GetType()}] Disposing an EventChannel action!");
#endif
				context.Action -= handler;
			}
		}
		public class Context
		{
			public object context;

			public event Action<TArg> Action;
			
			public List<ChannelRegister> registry;
			public TArg history;
			public bool hasHistory;
			
			public Context(object context)
			{
				this.context = context;
				
				Action = null;
				registry = new List<ChannelRegister>();
				hasHistory = false;
			}

			public void TriggerAction(TArg arg)
			{
				Action?.Invoke(arg);
			}
		}

		private Dictionary<object, Context> contextDatabase = new Dictionary<object, Context>();

		private Context FetchContext(object context)
		{
			Context channelContext;
			if (!contextDatabase.TryGetValue(context, out channelContext))
			{
#if UNITY_EDITOR
				if(VerboseLogging)
					Debug.Log($"[EventChannel][{GetType()}] Did not find context for object {context}, creating new");
#endif
				
				channelContext = new Context(context);
				contextDatabase.Add(context, channelContext);
			}

			return channelContext;
		}

		public Disposer Subscribe(Action<TArg> handler, RegistryType registryType = RegistryType.Persistent,
			FetchHistory fetchHistory = FetchHistory.No)
		{
			return Subscribe(this, handler, registryType, fetchHistory);
		}
		
		public Disposer Subscribe(object context, Action<TArg> handler, 
			RegistryType registryType = RegistryType.Persistent, FetchHistory fetchHistory = FetchHistory.No)
		{
			var ctx = FetchContext(context);

			// FetchHistory + RunOnce => no action gets added to the list, no disposer gets created
			if (fetchHistory == FetchHistory.Yes && ctx.hasHistory)
			{
#if UNITY_EDITOR
				if(VerboseLogging)
					Debug.Log($"[EventChannel][{GetType()}] Triggering history callback for handler with context {context}");
#endif
				handler?.Invoke(ctx.history);
				if (registryType == RegistryType.RunOnce)
					return null;
			}


			var disposer = new Disposer(this, ctx, handler);
			var register = new ChannelRegister(disposer, registryType);
			
			ctx.Action += handler;
			ctx.registry.Add(register);

#if UNITY_EDITOR
			if (VerboseLogging)
				Debug.Log($"[EventChannel][{GetType()}] Registered action for context {context}");
#endif
				
			return disposer;
		}

		public Context WithContext(object context)
		{
			return FetchContext(context);
		}
		
		public void Broadcast(TArg arg)
		{
			Broadcast(this, arg);
		}
		
		public void Broadcast(object context, TArg arg)
		{
			var ctx = FetchContext(context);

#if UNITY_EDITOR
			if (VerboseLogging)
				Debug.Log($"[EventChannel][{GetType()}] Triggering event for context {context} with value {arg}");
#endif

			ctx.TriggerAction(arg);
			
			ctx.history = arg;
			ctx.hasHistory = true;
			
			for (var i = ctx.registry.Count - 1; i >= 0; i--)
			{
				ctx.registry[i].IncreaseTriggerCount();
				if (ctx.registry[i].registryType != RegistryType.RunOnce) 
					continue;
				
				ctx.registry[i].disposer.Dispose();
				ctx.registry.RemoveAt(i);
			}
		}
	}
}