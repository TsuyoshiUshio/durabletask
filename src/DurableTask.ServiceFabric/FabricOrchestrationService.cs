﻿//  ----------------------------------------------------------------------------------
//  Copyright Microsoft Corporation
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ----------------------------------------------------------------------------------

namespace DurableTask.ServiceFabric
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Fabric;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DurableTask.History;
    using DurableTask.Tracking;
    using Microsoft.ServiceFabric.Data;

    class FabricOrchestrationService : IOrchestrationService
    {
        readonly IReliableStateManager stateManager;
        readonly IFabricOrchestrationServiceInstanceStore instanceStore;
        readonly SessionsProvider orchestrationProvider;
        readonly ActivityProvider<string, TaskMessageItem> activitiesProvider;
        readonly ScheduledMessageProvider scheduledMessagesProvider;
        readonly FabricOrchestrationProviderSettings settings;
        readonly CancellationTokenSource cancellationTokenSource;

        ConcurrentDictionary<string, SessionInformation> sessionInfos = new ConcurrentDictionary<string, SessionInformation>();

        public FabricOrchestrationService(IReliableStateManager stateManager,
            SessionsProvider orchestrationProvider,
            IFabricOrchestrationServiceInstanceStore instanceStore,
            FabricOrchestrationProviderSettings settings,
            CancellationTokenSource cancellationTokenSource)
        {
            this.stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            this.orchestrationProvider = orchestrationProvider;
            this.instanceStore = instanceStore;
            this.settings = settings;
            this.cancellationTokenSource = cancellationTokenSource;
            this.activitiesProvider = new ActivityProvider<string, TaskMessageItem>(this.stateManager, Constants.ActivitiesQueueName, cancellationTokenSource.Token);
            this.scheduledMessagesProvider = new ScheduledMessageProvider(this.stateManager, Constants.ScheduledMessagesDictionaryName, orchestrationProvider, cancellationTokenSource.Token);
        }

        public Task StartAsync()
        {
            return Task.WhenAll(this.activitiesProvider.StartAsync(),
                this.scheduledMessagesProvider.StartAsync(),
                this.instanceStore.StartAsync(),
                this.orchestrationProvider.StartAsync());
        }

        public Task StopAsync()
        {
            return StopAsync(false);
        }

        public Task StopAsync(bool isForced)
        {
            if (!this.cancellationTokenSource.IsCancellationRequested)
            {
                this.cancellationTokenSource.Cancel();
            }

            return CompletedTask.Default;
        }

        public Task CreateAsync()
        {
            return CreateAsync(true);
        }

        public Task CreateAsync(bool recreateInstanceStore)
        {
            return DeleteAsync(deleteInstanceStore: recreateInstanceStore);
            // Actual creation will be done on demand when we call GetOrAddAsync in StartAsync method.
        }

        public Task CreateIfNotExistsAsync()
        {
            return CompletedTask.Default;
        }

        public Task DeleteAsync()
        {
            return DeleteAsync(true);
        }

        public Task DeleteAsync(bool deleteInstanceStore)
        {
            List<Task> tasks = new List<Task>();
            tasks.Add(this.stateManager.RemoveAsync(Constants.OrchestrationDictionaryName));
            tasks.Add(this.stateManager.RemoveAsync(Constants.ScheduledMessagesDictionaryName));
            tasks.Add(this.stateManager.RemoveAsync(Constants.ActivitiesQueueName));

            if (deleteInstanceStore)
            {
                tasks.Add(this.stateManager.RemoveAsync(Constants.InstanceStoreDictionaryName));
            }

            return Task.WhenAll(tasks);
        }

        public bool IsMaxMessageCountExceeded(int currentMessageCount, OrchestrationRuntimeState runtimeState)
        {
            return false;
        }

        public int GetDelayInSecondsAfterOnProcessException(Exception exception)
        {
            return GetDelayForFetchOrProcessException(exception);
        }

        public int GetDelayInSecondsAfterOnFetchException(Exception exception)
        {
            return GetDelayForFetchOrProcessException(exception);
        }

        public int TaskOrchestrationDispatcherCount => this.settings.TaskOrchestrationDispatcherSettings.DispatcherCount;
        public int MaxConcurrentTaskOrchestrationWorkItems => this.settings.TaskOrchestrationDispatcherSettings.MaxConcurrentOrchestrations;

        // Note: Do not rely on cancellationToken parameter to this method because the top layer does not yet implement any cancellation.
        public async Task<TaskOrchestrationWorkItem> LockNextTaskOrchestrationWorkItemAsync(TimeSpan receiveTimeout, CancellationToken cancellationToken)
        {
            var currentSession = await this.orchestrationProvider.AcceptSessionAsync(receiveTimeout);

            if (currentSession == null)
            {
                return null;
            }

            List<Message<Guid, TaskMessageItem>> newMessages;
            try
            {
                newMessages = await this.orchestrationProvider.ReceiveSessionMessagesAsync(currentSession);
            }
            catch(Exception)
            {
                this.orchestrationProvider.TryUnlockSession(currentSession.SessionId, abandon: true);
                throw;
            }

            var currentRuntimeState = new OrchestrationRuntimeState(currentSession.SessionState);
            var workItem = new TaskOrchestrationWorkItem()
            {
                NewMessages = newMessages.Select(m => m.Value.TaskMessage).ToList(),
                InstanceId = currentSession.SessionId.InstanceId,
                OrchestrationRuntimeState = currentRuntimeState
            };

            if (newMessages.Count == 0)
            {
                if (currentRuntimeState.ExecutionStartedEvent == null)
                {
                    ProviderEventSource.Log.UnexpectedCodeCondition($"Orchestration with no execution started event found: {currentSession.SessionId}");
                    return null;
                }

                bool isComplete = currentRuntimeState.OrchestrationStatus.IsTerminalState();
                if (isComplete)
                {
                    await this.ReleaseTaskOrchestrationWorkItemAsync(workItem);
                }

                this.orchestrationProvider.TryUnlockSession(currentSession.SessionId, isComplete: isComplete);
                return null;
            }

            var sessionInfo = new SessionInformation()
            {
                Instance = currentSession.SessionId,
                LockTokens = newMessages.Select(m => m.Key).ToList()
            };

            if (!this.sessionInfos.TryAdd(workItem.InstanceId, sessionInfo))
            {
                ProviderEventSource.Log.UnexpectedCodeCondition($"{nameof(FabricOrchestrationService)}.{nameof(LockNextTaskOrchestrationWorkItemAsync)} : Multiple receivers processing the same session : {currentSession.SessionId.InstanceId}?");
            }

            return workItem;
        }

        public Task RenewTaskOrchestrationWorkItemLockAsync(TaskOrchestrationWorkItem workItem)
        {
            throw new NotImplementedException();
        }

        public async Task CompleteTaskOrchestrationWorkItemAsync(
            TaskOrchestrationWorkItem workItem,
            OrchestrationRuntimeState newOrchestrationRuntimeState,
            IList<TaskMessage> outboundMessages,
            IList<TaskMessage> orchestratorMessages,
            IList<TaskMessage> timerMessages,
            TaskMessage continuedAsNewMessage,
            OrchestrationState orchestrationState)
        {
            SessionInformation sessionInfo = GetSessionInfo(workItem.InstanceId);

            if (continuedAsNewMessage != null)
            {
                throw new Exception("ContinueAsNew is not supported yet");
            }

            IList<OrchestrationInstance> sessionsToEnqueue = null;
            List<Message<string, TaskMessageItem>> scheduledMessages = null;
            List<Message<string, TaskMessageItem>> activityMessages = null;

            await RetryHelper.ExecuteWithRetryOnTransient(async () =>
            {
                bool retryOnException;
                do
                {
                    try
                    {
                        retryOnException = false;
                        sessionsToEnqueue = null;
                        scheduledMessages = null;
                        activityMessages = null;

                        using (var txn = this.stateManager.CreateTransaction())
                        {
                            if (outboundMessages?.Count > 0)
                            {
                                activityMessages = outboundMessages.Select(m => new Message<string, TaskMessageItem>(Guid.NewGuid().ToString(), new TaskMessageItem(m))).ToList();
                                await this.activitiesProvider.SendBatchBeginAsync(txn, activityMessages);
                            }

                            if (timerMessages?.Count > 0)
                            {
                                scheduledMessages = timerMessages.Select(m => new Message<string, TaskMessageItem>(Guid.NewGuid().ToString(), new TaskMessageItem(m))).ToList();
                                await this.scheduledMessagesProvider.SendBatchBeginAsync(txn, scheduledMessages);
                            }

                            if (orchestratorMessages?.Count > 0)
                            {
                                if (workItem.OrchestrationRuntimeState?.ParentInstance != null)
                                {
                                    sessionsToEnqueue = await this.orchestrationProvider.TryAppendMessageBatchAsync(txn, orchestratorMessages.Select(tm => new TaskMessageItem(tm)));
                                }
                                else
                                {
                                    await this.orchestrationProvider.AppendMessageBatchAsync(txn, orchestratorMessages.Select(tm => new TaskMessageItem(tm)));
                                    sessionsToEnqueue = orchestratorMessages.Select(m => m.OrchestrationInstance).ToList();
                                }
                            }

                            await this.orchestrationProvider.CompleteMessages(txn, sessionInfo.Instance, sessionInfo.LockTokens);

                            // When an orchestration is completed, we won't update the instance store or drop session as part
                            // of this transaction (to avoid dropping session in an already big transaction where we may complete a few messages).
                            // Instead, we drop the session and update instance store as part of Releasing the work item.
                            // However, framework passes us 'null' value for 'newOrchestrationRuntimeState' when orchestration is completed and
                            // if we updated the session state to null and this transaction succeded, and a node failures occurs and we
                            // never call Release method, we will lose the runtime state of orchestration and never will be able to
                            // mark it as complete even if it is. So we use the work item's runtime state when 'newOrchestrationRuntimeState' is null
                            // so that the latest state is what is stored for the session.
                            // As part of Release, we are going to remove the row anyway for the session and it doesn't matter to update it to 'null'.
                            await this.orchestrationProvider.UpdateSessionState(txn, sessionInfo.Instance, newOrchestrationRuntimeState ?? workItem.OrchestrationRuntimeState);

                            // We skip writing to instanceStore when orchestration reached terminal state to avoid a minor timing issue that
                            // wait for an orchestration completes but another orchestration with the same name cannot be started immediately
                            // because the session is still in store. We update the instance store on orchestration completion and drop the
                            // session as part of a single transaction when we release the work item.
                            if (this.instanceStore != null && orchestrationState != null && !orchestrationState.OrchestrationStatus.IsTerminalState())
                            {
                                await this.instanceStore.WriteEntitesAsync(txn, new InstanceEntityBase[]
                                {
                                    new OrchestrationStateInstanceEntity()
                                    {
                                        State = orchestrationState
                                    }
                                });
                            }
                            await txn.CommitAsync();
                        }
                    }
                    catch (FabricReplicationOperationTooLargeException ex)
                    {
                        ProviderEventSource.Log.ExceptionInReliableCollectionOperations($"OrchestrationInstance = {sessionInfo.Instance}, Action = {nameof(CompleteTaskOrchestrationWorkItemAsync)}", ex.ToString());
                        retryOnException = true;
                        newOrchestrationRuntimeState = null;
                        outboundMessages = null;
                        timerMessages = null;
                        orchestratorMessages = null;
                        if (orchestrationState != null)
                        {
                            orchestrationState.OrchestrationStatus = OrchestrationStatus.Failed;
                            orchestrationState.Output = $"Fabric exception when trying to process orchestration: {ex}. Investigate and consider reducing the serialization size of orchestration inputs/outputs/overall length to avoid the issue.";
                        }
                    }
                } while (retryOnException);
            }, uniqueActionIdentifier: $"OrchestrationId = '{workItem.InstanceId}', Action = '{nameof(CompleteTaskOrchestrationWorkItemAsync)}'");

            if (activityMessages != null)
            {
                this.activitiesProvider.SendBatchComplete(activityMessages);
            }
            if (scheduledMessages != null)
            {
                this.scheduledMessagesProvider.SendBatchComplete(scheduledMessages);
            }
            if (sessionsToEnqueue != null)
            {
                foreach (var instance in sessionsToEnqueue)
                {
                    this.orchestrationProvider.TryEnqueueSession(instance);
                }
            }
        }

        public Task AbandonTaskOrchestrationWorkItemAsync(TaskOrchestrationWorkItem workItem)
        {
            SessionInformation sessionInfo = TryRemoveSessionInfo(workItem.InstanceId);
            if (sessionInfo == null)
            {
                ProviderEventSource.Log.UnexpectedCodeCondition($"{nameof(AbandonTaskOrchestrationWorkItemAsync)} : Could not get a session info object while trying to abandon session {workItem.InstanceId}");
            }
            else
            {
                this.orchestrationProvider.TryUnlockSession(sessionInfo.Instance, abandon: true);
            }
            return CompletedTask.Default;
        }

        public async Task ReleaseTaskOrchestrationWorkItemAsync(TaskOrchestrationWorkItem workItem)
        {
            bool isComplete = workItem.OrchestrationRuntimeState.OrchestrationStatus.IsTerminalState();

            if (isComplete)
            {
                await RetryHelper.ExecuteWithRetryOnTransient(async () =>
                {
                    using (var txn = this.stateManager.CreateTransaction())
                    {
                        await this.orchestrationProvider.DropSession(txn, workItem.OrchestrationRuntimeState.OrchestrationInstance);
                        await this.instanceStore.WriteEntitesAsync(txn, new InstanceEntityBase[]
                        {
                            new OrchestrationStateInstanceEntity()
                            {
                                State = Utils.BuildOrchestrationState(workItem.OrchestrationRuntimeState)
                            }
                        });
                        await txn.CommitAsync();
                    }
                }, uniqueActionIdentifier: $"OrchestrationId = '{workItem.InstanceId}', Action = '{nameof(ReleaseTaskOrchestrationWorkItemAsync)}'");

                ProviderEventSource.Log.OrchestrationFinished(workItem.InstanceId,
                    workItem.OrchestrationRuntimeState.OrchestrationStatus.ToString(),
                    (workItem.OrchestrationRuntimeState.CompletedTime - workItem.OrchestrationRuntimeState.CreatedTime).TotalSeconds,
                    workItem.OrchestrationRuntimeState.Output,
                    workItem.OrchestrationRuntimeState.OrchestrationInstance.ExecutionId);
            }

            SessionInformation sessionInfo = TryRemoveSessionInfo(workItem.InstanceId);
            if (sessionInfo != null)
            {
                this.orchestrationProvider.TryUnlockSession(sessionInfo.Instance, isComplete: isComplete);
            }
        }

        public int TaskActivityDispatcherCount => this.settings.TaskActivityDispatcherSettings.DispatcherCount;
        public int MaxConcurrentTaskActivityWorkItems => this.settings.TaskActivityDispatcherSettings.MaxConcurrentActivities;

        // Note: Do not rely on cancellationToken parameter to this method because the top layer does not yet implement any cancellation.
        public async Task<TaskActivityWorkItem> LockNextTaskActivityWorkItem(TimeSpan receiveTimeout, CancellationToken cancellationToken)
        {
            var message = await this.activitiesProvider.ReceiveAsync(receiveTimeout);

            if (message != null)
            {
                return new TaskActivityWorkItem()
                {
                    Id = message.Key,
                    TaskMessage = message.Value.TaskMessage
                };
            }

            return null;
        }

        public async Task CompleteTaskActivityWorkItemAsync(TaskActivityWorkItem workItem, TaskMessage responseMessage)
        {
            bool added = false;

            await RetryHelper.ExecuteWithRetryOnTransient(async () =>
            {
                bool retryOnException;
                do
                {
                    try
                    {
                        added = false;
                        retryOnException = false;

                        using (var txn = this.stateManager.CreateTransaction())
                        {
                            await this.activitiesProvider.CompleteAsync(txn, workItem.Id);
                            added = await this.orchestrationProvider.TryAppendMessageAsync(txn, new TaskMessageItem(responseMessage));
                            await txn.CommitAsync();
                        }
                    }
                    catch (FabricReplicationOperationTooLargeException ex)
                    {
                        ProviderEventSource.Log.ExceptionInReliableCollectionOperations($"OrchestrationInstance = {responseMessage.OrchestrationInstance}, ActivityId = {workItem.Id}, Action = {nameof(CompleteTaskActivityWorkItemAsync)}", ex.ToString());
                        retryOnException = true;
                        var originalEvent = responseMessage.Event;
                        int taskScheduledId = GetTaskScheduledId(originalEvent);
                        string details = $"Fabric exception when trying to save activity result: {ex}. Consider reducing the serialization size of activity result to avoid the issue.";
                        responseMessage.Event = new TaskFailedEvent(originalEvent.EventId, taskScheduledId, ex.Message, details);
                    }
                } while (retryOnException);
            }, uniqueActionIdentifier: $"OrchestrationId = '{responseMessage.OrchestrationInstance.InstanceId}', ActivityId = '{workItem.Id}', Action = '{nameof(CompleteTaskActivityWorkItemAsync)}'");

            if (added)
            {
                this.orchestrationProvider.TryEnqueueSession(responseMessage.OrchestrationInstance);
            }
        }

        public Task AbandonTaskActivityWorkItemAsync(TaskActivityWorkItem workItem)
        {
            this.activitiesProvider.Abandon(workItem.Id);
            return CompletedTask.Default;
        }

        public Task<TaskActivityWorkItem> RenewTaskActivityWorkItemLockAsync(TaskActivityWorkItem workItem)
        {
            return Task.FromResult(workItem);
        }

        int GetTaskScheduledId(HistoryEvent historyEvent)
        {
            TaskCompletedEvent tce = historyEvent as TaskCompletedEvent;
            if (tce != null)
            {
                return tce.TaskScheduledId;
            }

            TaskFailedEvent tfe = historyEvent as TaskFailedEvent;
            if (tfe != null)
            {
                return tfe.TaskScheduledId;
            }

            return -1;
        }

        int GetDelayForFetchOrProcessException(Exception exception)
        {
            //Todo: Need to fine tune
            if (exception is TimeoutException)
            {
                return 1;
            }

            if (exception is FabricNotReadableException)
            {
                return 2;
            }

            return 0;
        }

        SessionInformation GetSessionInfo(string sessionId)
        {
            ProviderEventSource.Log.TraceMessage(sessionId, $"{nameof(GetSessionInfo)} - Getting session info");
            SessionInformation sessionInfo;
            if (!this.sessionInfos.TryGetValue(sessionId, out sessionInfo))
            {
                var message = $"{nameof(GetSessionInfo)}. Trying to get a session that's not in locked sessions {sessionId}";
                ProviderEventSource.Log.UnexpectedCodeCondition(message);
                throw new Exception(message);
            }

            return sessionInfo;
        }

        SessionInformation TryRemoveSessionInfo(string sessionId)
        {
            SessionInformation sessionInfo;
            var removed = this.sessionInfos.TryRemove(sessionId, out sessionInfo);
            ProviderEventSource.Log.TraceMessage(sessionId, $"{nameof(TryRemoveSessionInfo)}: Removed = {removed}");
            return sessionInfo;
        }

        class SessionInformation
        {
            public OrchestrationInstance Instance { get; set; }

            public List<Guid> LockTokens { get; set; }
        }
    }
}