using System;
using System.Threading;
using System.Threading.Tasks;
using Config.SqlStreamStore.Delegates;
using SqlStreamStore;
using SqlStreamStore.Streams;
using SqlStreamStore.Subscriptions;

namespace Config.SqlStreamStore
{
    /// <summary>
    /// Watches for changes using a stream subscription. 
    /// </summary>
    public class SubscriptionBasedChangeWatcher : IChangeWatcher
    {
        private readonly IStreamStore _streamStore;
        private readonly StreamId _streamId;
        private readonly IConfigurationSettingsHooks _messageHooks;

        public SubscriptionBasedChangeWatcher(IStreamStore streamStore, StreamId streamId, IConfigurationSettingsHooks messageHooks)
        {
            _streamStore = streamStore;
            _streamId = streamId;
            _messageHooks = messageHooks;
        }

        public IDisposable WatchForChanges(int version, OnSettingsChanged onSettingsChanged, CancellationToken ct)
        {

            IStreamSubscription subscription = null;


            async Task StreamMessageReceived(IStreamSubscription _, StreamMessage streamMessage,
                CancellationToken cancellationToken)
            {
                var settings = await StreamStoreConfigRepository.BuildConfigurationSettingsFromMessage(streamMessage, _messageHooks, ct);
                await onSettingsChanged(settings, ct);
            };

            void SubscriptionDropped(IStreamSubscription _, SubscriptionDroppedReason reason,
                Exception exception = null)
            {
                if (reason != SubscriptionDroppedReason.Disposed)
                {
                    SetupSubscription();
                }
            };

            void SetupSubscription()
            {
                subscription = _streamStore.SubscribeToStream(
                    streamId: _streamId,
                    continueAfterVersion: version,
                    streamMessageReceived: StreamMessageReceived,
                    subscriptionDropped: SubscriptionDropped);
            }

            SetupSubscription();

            return subscription;
        }
    }
}