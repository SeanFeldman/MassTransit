﻿// Copyright 2007-2011 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use 
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed 
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.Testing
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using Magnum.Extensions;

	public class ReceivedMessageListImpl :
		ReceivedMessageList,
		IDisposable
	{
		readonly HashSet<ReceivedMessage> _messages;
		readonly AutoResetEvent _received;
		TimeSpan _timeout = 12.Seconds();

		public ReceivedMessageListImpl()
		{
			_messages = new HashSet<ReceivedMessage>(new MessageIdEqualityComparer());
			_received = new AutoResetEvent(false);
		}

		public void Dispose()
		{
			using (_received)
			{
			}
		}

		public IEnumerator<ReceivedMessage> GetEnumerator()
		{
			lock (_messages)
				return _messages.ToList().GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public bool Any()
		{
			bool any;
			lock (_messages)
				any = _messages.Any();

			while (any == false)
			{
				if (_received.WaitOne(_timeout, true) == false)
					return false;

				lock (_messages)
					any = _messages.Any();
			}

			return true;
		}

		public bool Any<T>()
			where T : class
		{
			return Any<T>(x => true);
		}

		public bool Any<T>(Func<T, bool> filter)
			where T : class
		{
			bool any;
			IConsumeContext<T> consumeContext;

			Func<ReceivedMessage, bool> predicate =
				x => x.Context.TryGetContext(out consumeContext) && filter(consumeContext.Message);

			lock (_messages)
				any = _messages.Any(predicate);

			while (any == false)
			{
				if (_received.WaitOne(_timeout, true) == false)
					return false;

				lock (_messages)
				{
					any = _messages.Any(predicate);
				}
			}

			return true;
		}

		public void Add(ReceivedMessage message)
		{
			lock (_messages)
			{
				if (_messages.Add(message))
					_received.Set();
			}
		}

		public void Remove(ReceivedMessage message)
		{
			lock (_messages)
				_messages.Remove(message);
		}

		class MessageIdEqualityComparer :
			IEqualityComparer<ReceivedMessage>
		{
			public bool Equals(ReceivedMessage x, ReceivedMessage y)
			{
				return string.Equals(x.Context.MessageId, y.Context.MessageId);
			}

			public int GetHashCode(ReceivedMessage message)
			{
				return message.Context.MessageId.GetHashCode();
			}
		}
	}

	public class ReceivedMessageListImpl<T> :
		ReceivedMessageList<T>,
		IDisposable
	{
		readonly HashSet<ReceivedMessage<T>> _messages;
		readonly AutoResetEvent _received;
		TimeSpan _timeout = 8.Seconds();

		public ReceivedMessageListImpl()
		{
			_messages = new HashSet<ReceivedMessage<T>>(new MessageIdEqualityComparer());
			_received = new AutoResetEvent(false);
		}

		public void Dispose()
		{
			using (_received)
			{
			}
		}

		public IEnumerator<ReceivedMessage<T>> GetEnumerator()
		{
			lock (_messages)
				return _messages.ToList().GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public bool Any()
		{
			bool any;
			lock (_messages)
				any = _messages.Any();

			while (any == false)
			{
				if (_received.WaitOne(_timeout, true) == false)
					return false;

				lock (_messages)
					any = _messages.Any();
			}

			return true;
		}

		public void Add(ReceivedMessage<T> message)
		{
			lock (_messages)
			{
				if (_messages.Add(message))
					_received.Set();
			}
		}

		class MessageIdEqualityComparer :
			IEqualityComparer<ReceivedMessage<T>>
		{
			public bool Equals(ReceivedMessage<T> x, ReceivedMessage<T> y)
			{
				return string.Equals(x.Context.MessageId, y.Context.MessageId);
			}

			public int GetHashCode(ReceivedMessage<T> message)
			{
				return message.Context.MessageId.GetHashCode();
			}
		}
	}
}