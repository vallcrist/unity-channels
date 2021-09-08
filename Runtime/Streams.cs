using System;
using System.Collections.Generic;

namespace BitCake.EventStreams
{
	public interface IStream
	{
		
	}

	/// <summary>
	/// Channels entry point
	/// </summary>
	public class EventStreams
	{
		private static Dictionary<Type, IStream> streams = new Dictionary<Type, IStream>();

		public static T Get<T>() where T : IStream, new()
		{
			Type streamType = typeof(T);
			IStream stream;

			if (streams.TryGetValue(streamType, out stream))
			{
				return (T) stream;
			}

			return (T) Bind(streamType);
		}

		private static IStream Bind(Type signalType)
		{
			IStream stream;
			if (streams.TryGetValue(signalType, out stream))
			{
				UnityEngine.Debug.LogError(string.Format("Signal already registered for type {0}",
					signalType.ToString()));
				return stream;
			}

			stream = (IStream) Activator.CreateInstance(signalType);
			streams.Add(signalType, stream);
			return stream;
		}
	}
}