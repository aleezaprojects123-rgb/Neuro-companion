// TelemetryEmitter.cs
// NeuroCompanion — Phase 3, Step 4: Backend Integration Layer
//
// Receives observable patient interaction events from Unity components,
// queues them durably on disk, and delivers them to POST /api/v1/telemetry/events.
//
// Design rules:
//  - Every event gets a stable UUIDv4 event_id assigned on creation.
//  - Durable queue survives app crashes, restarts, and temporary backend unavailability.
//  - In-flight events are explicitly tracked and preserved across sudden process termination.
//  - Atomic disk replacement protects previous valid journal files on Windows/NTFS.
//  - Deduplication is guaranteed by the backend (idempotent on event_id).
//  - Permanent client errors (400, 422) undergo binary bisection to isolate and quarantine corrupt events.
//  - Quarantine records include full event payload and backend rejection reason without sensitive PII/audio.
//  - HTTP 429 respects Retry-After; 5xx and network errors use bounded exponential backoff.
//  - Zero data loss: patient interaction events are never silently discarded.
//  - No audio, video, biometric data, or raw keystrokes are recorded.
//  - No AI, scoring, or tier decisions are made here.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NeuroCompanion.Backend
{
    /// <summary>
    /// MonoBehaviour. Attach to [MANAGERS]/Telemetry_Emitter_Holder.
    /// Requires BackendClient to be present in the scene.
    /// </summary>
    public class TelemetryEmitter : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Configuration
        // -------------------------------------------------------------------------

        [Header("Telemetry Emitter")]
        [Tooltip("Maximum events to batch per send cycle.")]
        [SerializeField] private int _maxBatchSize = 50;

        [Tooltip("Seconds between automatic flush attempts.")]
        [SerializeField] private float _flushIntervalSeconds = 15f;

        [Tooltip("Maximum exponential backoff exponent (delay capped at 30s).")]
        [SerializeField] private int _maxRetryAttempts = 5;

        // -------------------------------------------------------------------------
        // Internal queue & tracking
        // -------------------------------------------------------------------------

        private readonly Queue<InteractionEvent> _pendingQueue = new();
        private readonly Dictionary<string, InteractionEvent> _inFlightEvents = new();
        private bool _flushInProgress = false;

        // -------------------------------------------------------------------------
        // Durable disk persistence paths
        // -------------------------------------------------------------------------

        private string JournalPath => Path.Combine(Application.persistentDataPath, "telemetry_offline_journal.json");
        private string JournalTmpPath => Path.Combine(Application.persistentDataPath, "telemetry_offline_journal.json.tmp");
        private string JournalBackupPath => Path.Combine(Application.persistentDataPath, "telemetry_offline_journal.json.bak");
        private string QuarantinePath => Path.Combine(Application.persistentDataPath, "telemetry_quarantine.json");

        [Serializable]
        private class JournalWrapper
        {
            public List<InteractionEvent> events = new();
        }

        [Serializable]
        private class QuarantineRecord
        {
            public string quarantine_timestamp_utc;
            public long http_status;
            public string rejection_response;
            public string event_id;
            public InteractionEvent rejected_event;
        }

        // -------------------------------------------------------------------------
        // Singleton Access
        // -------------------------------------------------------------------------

        public static TelemetryEmitter Instance { get; private set; }

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            LoadJournalFromDisk();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            StartCoroutine(AutoFlushCoroutine());
        }

        private IEnumerator AutoFlushCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(_flushIntervalSeconds);
                if (_pendingQueue.Count > 0 && !_flushInProgress)
                    StartCoroutine(FlushBatch());
            }
        }

        // -------------------------------------------------------------------------
        // Public API — emit observable interaction events
        // All factories validate required fields, assign type-safe enums, and persist.
        // -------------------------------------------------------------------------

        /// <summary>Emit an "activity_started" event.</summary>
        public void EmitActivityStarted(string patientId, string sessionId,
            TherapyArea therapyArea, string activityId)
        {
            Enqueue(BuildEvent(patientId, sessionId, therapyArea, activityId,
                EventType.ActivityStarted, null));
        }

        /// <summary>Emit an "activity_completed" event with optional completion status.</summary>
        public void EmitActivityCompleted(string patientId, string sessionId,
            TherapyArea therapyArea, string activityId,
            string completionStatus = CompletionStatus.Independent)
        {
            var payload = new InteractionPayload
            {
                completion_status = completionStatus
            };
            Enqueue(BuildEvent(patientId, sessionId, therapyArea, activityId,
                EventType.ActivityCompleted, payload));
        }

        /// <summary>Emit a "hint_requested" event.</summary>
        public void EmitHintRequested(string patientId, string sessionId,
            TherapyArea therapyArea, string activityId,
            int hintTier = 1, float latencySeconds = -1f,
            string substepIdentifier = null)
        {
            var payload = new InteractionPayload
            {
                hint_tier_delivered  = hintTier,
                latency_seconds      = latencySeconds,
                assistance_source    = AssistanceSource.UserRequested,
                substep_identifier   = substepIdentifier
            };
            Enqueue(BuildEvent(patientId, sessionId, therapyArea, activityId,
                EventType.HintRequested, payload));
        }

        /// <summary>Emit an "instruction_requested" event.</summary>
        public void EmitInstructionRequested(string patientId, string sessionId,
            TherapyArea therapyArea, string activityId)
        {
            Enqueue(BuildEvent(patientId, sessionId, therapyArea, activityId,
                EventType.InstructionRequested, null));
        }

        /// <summary>Emit an "instruction_repeated" event.</summary>
        public void EmitInstructionRepeated(string patientId, string sessionId,
            TherapyArea therapyArea, string activityId,
            float latencySeconds = -1f)
        {
            var payload = new InteractionPayload
            {
                latency_seconds   = latencySeconds,
                assistance_source = AssistanceSource.UserRequested
            };
            Enqueue(BuildEvent(patientId, sessionId, therapyArea, activityId,
                EventType.InstructionRepeated, payload));
        }

        /// <summary>Emit an "attempt_submitted" event with nullable isCorrect boolean support.</summary>
        public void EmitAttemptSubmitted(string patientId, string sessionId,
            TherapyArea therapyArea, string activityId,
            int attemptIndex, bool? isCorrect = null,
            float latencySeconds = -1f,
            string substepIdentifier = null)
        {
            var payload = new InteractionPayload
            {
                attempt_index      = attemptIndex,
                is_correct         = isCorrect,
                latency_seconds    = latencySeconds,
                substep_identifier = substepIdentifier
            };
            Enqueue(BuildEvent(patientId, sessionId, therapyArea, activityId,
                EventType.AttemptSubmitted, payload));
        }

        /// <summary>Emit a "demonstration_requested" event.</summary>
        public void EmitDemonstrationRequested(string patientId, string sessionId,
            TherapyArea therapyArea, string activityId,
            string substepIdentifier = null)
        {
            var payload = new InteractionPayload
            {
                assistance_source  = AssistanceSource.UserRequested,
                substep_identifier = substepIdentifier
            };
            Enqueue(BuildEvent(patientId, sessionId, therapyArea, activityId,
                EventType.DemonstrationRequested, payload));
        }

        /// <summary>
        /// Emit an "assistance_triggered" event.
        /// source: use AssistanceSource constants.
        /// </summary>
        public void EmitAssistanceTriggered(string patientId, string sessionId,
            TherapyArea therapyArea, string activityId,
            string source, string notes = null)
        {
            var payload = new InteractionPayload
            {
                assistance_source = source,
                notes             = notes
            };
            Enqueue(BuildEvent(patientId, sessionId, therapyArea, activityId,
                EventType.AssistanceTriggered, payload));
        }

        /// <summary>Emit an "activity_paused" event.</summary>
        public void EmitActivityPaused(string patientId, string sessionId,
            TherapyArea therapyArea, string activityId)
        {
            Enqueue(BuildEvent(patientId, sessionId, therapyArea, activityId,
                EventType.ActivityPaused, null));
        }

        /// <summary>Emit an "activity_resumed" event.</summary>
        public void EmitActivityResumed(string patientId, string sessionId,
            TherapyArea therapyArea, string activityId,
            float pauseDurationSeconds = -1f)
        {
            var payload = new InteractionPayload
            {
                pause_duration_seconds = pauseDurationSeconds
            };
            Enqueue(BuildEvent(patientId, sessionId, therapyArea, activityId,
                EventType.ActivityResumed, payload));
        }

        /// <summary>Emit an "activity_skipped" event.</summary>
        public void EmitActivitySkipped(string patientId, string sessionId,
            TherapyArea therapyArea, string activityId)
        {
            var payload = new InteractionPayload
            {
                completion_status = CompletionStatus.SkippedByUser
            };
            Enqueue(BuildEvent(patientId, sessionId, therapyArea, activityId,
                EventType.ActivitySkipped, payload));
        }

        // -------------------------------------------------------------------------
        // Raw enqueue for future extensibility
        // -------------------------------------------------------------------------

        /// <summary>
        /// Enqueue any fully-constructed InteractionEvent.
        /// Validates required fields before accepting and persisting the event.
        /// </summary>
        public bool EnqueueRaw(InteractionEvent evt)
        {
            if (!ValidateEvent(evt)) return false;
            Enqueue(evt);
            return true;
        }

        // -------------------------------------------------------------------------
        // Manual flush (e.g. before scene change or session end)
        // -------------------------------------------------------------------------

        /// <summary>Immediately flushes pending events to the backend.</summary>
        public void FlushNow()
        {
            if (_pendingQueue.Count > 0 && !_flushInProgress)
                StartCoroutine(FlushBatch());
        }

        /// <summary>Returns the number of events waiting to be sent.</summary>
        public int PendingCount => _pendingQueue.Count;

        // -------------------------------------------------------------------------
        // Internal helpers & Durable Journaling
        // -------------------------------------------------------------------------

        private void Enqueue(InteractionEvent evt)
        {
            if (!ValidateEvent(evt)) return;
            _pendingQueue.Enqueue(evt);
            SaveJournalToDisk();
            Debug.Log($"[TelemetryEmitter] Queued event '{evt.event_type}' for patient {evt.patient_id}. Queue depth: {_pendingQueue.Count}");
        }

        private void LoadJournalFromDisk()
        {
            try
            {
                string sourcePath = null;
                List<InteractionEvent> recoveredEvents = null;

                // Priority 1: Primary journal
                if (File.Exists(JournalPath))
                {
                    recoveredEvents = TryReadJournalFile(JournalPath);
                    if (recoveredEvents != null)
                    {
                        sourcePath = JournalPath;
                    }
                    else
                    {
                        Debug.LogWarning($"[TelemetryEmitter] Primary journal at '{JournalPath}' is missing, empty, or unreadable — checking backup.");
                    }
                }

                // Priority 2: Backup journal
                if (recoveredEvents == null && File.Exists(JournalBackupPath))
                {
                    recoveredEvents = TryReadJournalFile(JournalBackupPath);
                    if (recoveredEvents != null)
                    {
                        sourcePath = JournalBackupPath;
                        Debug.LogWarning($"[TelemetryEmitter] Recovered {recoveredEvents.Count} event(s) from backup journal: '{JournalBackupPath}'.");
                    }
                    else
                    {
                        Debug.LogWarning($"[TelemetryEmitter] Backup journal at '{JournalBackupPath}' is empty or unreadable — checking temporary journal.");
                    }
                }

                // Priority 3: Temporary journal
                if (recoveredEvents == null && File.Exists(JournalTmpPath))
                {
                    recoveredEvents = TryReadJournalFile(JournalTmpPath);
                    if (recoveredEvents != null)
                    {
                        sourcePath = JournalTmpPath;
                        Debug.LogWarning($"[TelemetryEmitter] Recovered {recoveredEvents.Count} event(s) from temporary journal: '{JournalTmpPath}'.");
                    }
                }

                if (recoveredEvents != null && recoveredEvents.Count > 0)
                {
                    int enqueuedCount = 0;
                    var seenIds = new HashSet<string>();
                    foreach (var evt in recoveredEvents)
                    {
                        if (seenIds.Add(evt.event_id))
                        {
                            _pendingQueue.Enqueue(evt);
                            enqueuedCount++;
                        }
                    }
                    Debug.Log($"[TelemetryEmitter] Enqueued {enqueuedCount} recovered offline event(s) into pending queue.");

                    // If recovered from backup or temporary file, re-persist to a verified primary journal FIRST
                    if (sourcePath != JournalPath)
                    {
                        bool persisted = PersistAndVerifyPrimaryJournal();
                        if (persisted)
                        {
                            // Only clean up recovery files after primary journal is verified on disk
                            if (File.Exists(JournalTmpPath)) File.Delete(JournalTmpPath);
                            if (File.Exists(JournalBackupPath)) File.Delete(JournalBackupPath);
                            Debug.Log("[TelemetryEmitter] Primary journal re-established and verified. Transient recovery files cleaned up.");
                        }
                        else
                        {
                            Debug.LogError("[TelemetryEmitter] Failed to persist primary journal from recovery. Retaining recovery files for safety.");
                        }
                    }
                    else
                    {
                        // Clean up any stale transient files left over from prior runs
                        if (File.Exists(JournalTmpPath)) File.Delete(JournalTmpPath);
                        if (File.Exists(JournalBackupPath)) File.Delete(JournalBackupPath);
                    }
                }
                else
                {
                    // If JournalPath exists and is verified valid empty journal, clean up transient files
                    if (File.Exists(JournalPath) && TryReadJournalFile(JournalPath) != null)
                    {
                        if (File.Exists(JournalTmpPath)) File.Delete(JournalTmpPath);
                        if (File.Exists(JournalBackupPath)) File.Delete(JournalBackupPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TelemetryEmitter] Failed during offline journal recovery: {ex.Message}");
            }
        }

        private List<InteractionEvent> TryReadJournalFile(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            try
            {
                var fi = new FileInfo(filePath);
                if (fi.Length == 0) return null;

                string json = File.ReadAllText(filePath);
                if (string.IsNullOrEmpty(json)) return null;

                var wrapper = JsonUtility.FromJson<JournalWrapper>(json);
                if (wrapper != null && wrapper.events != null)
                {
                    var validEvents = new List<InteractionEvent>();
                    var seenIds = new HashSet<string>();
                    foreach (var evt in wrapper.events)
                    {
                        if (ValidateEvent(evt) && seenIds.Add(evt.event_id))
                        {
                            validEvents.Add(evt);
                        }
                    }
                    return validEvents;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TelemetryEmitter] Journal candidate at '{filePath}' is unreadable or corrupt: {ex.Message}");
            }
            return null;
        }

        private bool PersistAndVerifyPrimaryJournal()
        {
            try
            {
                var allEvents = new List<InteractionEvent>(_pendingQueue);
                if (allEvents.Count == 0) return true;

                var wrapper = new JournalWrapper { events = allEvents };
                string json = JsonUtility.ToJson(wrapper, false);

                bool written = WriteJournalAtomic(json);
                if (!written) return false;

                // Verification check: ensure primary journal exists, is non-empty, and parses correctly
                var verifiedEvents = TryReadJournalFile(JournalPath);
                return verifiedEvents != null && verifiedEvents.Count == allEvents.Count;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TelemetryEmitter] Primary journal verification failed: {ex.Message}");
                return false;
            }
        }

        private void SaveJournalToDisk()
        {
            try
            {
                // Include both in-flight events and pending events without duplicates
                var allEvents = new List<InteractionEvent>();
                var seenIds = new HashSet<string>();

                foreach (var kvp in _inFlightEvents)
                {
                    if (seenIds.Add(kvp.Key))
                        allEvents.Add(kvp.Value);
                }

                foreach (var evt in _pendingQueue)
                {
                    if (seenIds.Add(evt.event_id))
                        allEvents.Add(evt);
                }

                if (allEvents.Count == 0)
                {
                    if (File.Exists(JournalPath)) File.Delete(JournalPath);
                    if (File.Exists(JournalTmpPath)) File.Delete(JournalTmpPath);
                    if (File.Exists(JournalBackupPath)) File.Delete(JournalBackupPath);
                    return;
                }

                var wrapper = new JournalWrapper { events = allEvents };
                string json = JsonUtility.ToJson(wrapper, false);

                bool success = WriteJournalAtomic(json);
                if (!success)
                {
                    Debug.LogError("[TelemetryEmitter] SaveJournalToDisk could not complete atomic replacement. In-memory queue preserved.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TelemetryEmitter] Failed to persist offline journal to disk: {ex.Message}");
            }
        }

        private bool WriteJournalAtomic(string json)
        {
            try
            {
                File.WriteAllText(JournalTmpPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TelemetryEmitter] Failed to write temporary journal '{JournalTmpPath}': {ex.Message}");
                return false;
            }

            if (!File.Exists(JournalPath))
            {
                try
                {
                    File.Move(JournalTmpPath, JournalPath);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[TelemetryEmitter] Failed to establish initial journal: {ex.Message}");
                    return false;
                }
            }

            // JournalPath already exists — perform safe replacement without pre-deleting
            try
            {
                // Win32/NTFS atomic replace with backup
                File.Replace(JournalTmpPath, JournalPath, JournalBackupPath, true);
                if (File.Exists(JournalBackupPath))
                {
                    try { File.Delete(JournalBackupPath); } catch { /* Non-fatal if backup locked */ }
                }
                return true;
            }
            catch (PlatformNotSupportedException)
            {
                // Fallback for non-Windows platforms: NEVER delete JournalPath first!
                // Step 1: Copy existing JournalPath to JournalBackupPath
                // Step 2: Copy JournalTmpPath over JournalPath
                // Step 3: Clean up backup only after successful copy
                try
                {
                    File.Copy(JournalPath, JournalBackupPath, true);
                    File.Copy(JournalTmpPath, JournalPath, true);
                    if (File.Exists(JournalTmpPath)) File.Delete(JournalTmpPath);
                    if (File.Exists(JournalBackupPath)) File.Delete(JournalBackupPath);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[TelemetryEmitter] Non-Windows atomic replace fallback failed: {ex.Message}. Existing journal preserved.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                // If File.Replace failed (e.g. transient file lock)
                // NEVER delete JournalPath. Retain JournalTmpPath and JournalPath.
                Debug.LogError($"[TelemetryEmitter] Atomic journal replacement failed: {ex.Message}. Existing journal preserved in '{JournalPath}'.");
                return false;
            }
        }

        private void AppendToQuarantine(InteractionEvent evt, long statusCode, string rawBody)
        {
            try
            {
                var record = new QuarantineRecord
                {
                    quarantine_timestamp_utc = DateTime.UtcNow.ToString("o"),
                    http_status = statusCode,
                    rejection_response = rawBody,
                    event_id = evt.event_id,
                    rejected_event = evt
                };

                // JSONL format: exactly one independent JSON document per line
                string line = JsonUtility.ToJson(record, false);
                if (!string.IsNullOrEmpty(line))
                {
                    using (var fileStream = new FileStream(QuarantinePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                    using (var writer = new StreamWriter(fileStream, System.Text.Encoding.UTF8))
                    {
                        writer.WriteLine(line);
                        writer.Flush();
                        fileStream.Flush(true); // Force commit to physical storage media
                    }
                    Debug.LogError($"[TelemetryEmitter] Quarantined invalid event {evt.event_id} (HTTP {statusCode}) to {QuarantinePath}: {rawBody}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TelemetryEmitter] Failed to append quarantine record: {ex.Message}");
            }
        }

        private IEnumerator FlushBatch(int retryCount = 0)
        {
            if (_flushInProgress || _pendingQueue.Count == 0) yield break;
            _flushInProgress = true;

            var batch = new List<InteractionEvent>();
            while (batch.Count < _maxBatchSize && _pendingQueue.Count > 0)
            {
                var evt = _pendingQueue.Dequeue();
                _inFlightEvents[evt.event_id] = evt;
                batch.Add(evt);
            }

            if (batch.Count == 0)
            {
                _flushInProgress = false;
                yield break;
            }

            yield return StartCoroutine(ProcessBatchSend(batch, retryCount));
        }

        private IEnumerator ProcessBatchSend(List<InteractionEvent> batch, int retryCount)
        {
            if (BackendClient.Instance == null)
            {
                Debug.LogWarning("[TelemetryEmitter] BackendClient not available — re-queuing batch.");
                RequeueBatch(batch);
                _flushInProgress = false;
                yield break;
            }

            bool completed = false;
            BackendResult<TelemetryResponse> sendResult = default;

            BackendClient.Instance.PostTelemetryEvents(batch, result =>
            {
                sendResult = result;
                completed = true;
            });

            while (!completed)
                yield return null;

            if (sendResult.IsSuccess)
            {
                Debug.Log($"[TelemetryEmitter] Sent {batch.Count} event(s). Inserted: {sendResult.Data?.inserted_count ?? 0}, Duplicates skipped: {sendResult.Data?.skipped_duplicates_count ?? 0}");
                foreach (var evt in batch)
                    _inFlightEvents.Remove(evt.event_id);

                SaveJournalToDisk();
                _flushInProgress = false;
                yield break;
            }

            long status = sendResult.StatusCode;

            if (status == 400 || status == 422)
            {
                Debug.LogWarning($"[TelemetryEmitter] Client validation error (HTTP {status}): {sendResult.RawBody}");

                if (batch.Count > 1)
                {
                    // Binary bisection: split into two halves to isolate invalid event without dropping valid events
                    int mid = batch.Count / 2;
                    var halfA = batch.GetRange(0, mid);
                    var halfB = batch.GetRange(mid, batch.Count - mid);

                    Debug.LogWarning($"[TelemetryEmitter] Bisecting batch of {batch.Count} into {halfA.Count} and {halfB.Count} to isolate invalid event.");

                    RequeueBatch(halfB);

                    // Immediately transmit first half
                    yield return StartCoroutine(ProcessBatchSend(halfA, 0));
                }
                else
                {
                    // Single invalid event isolated
                    var invalidEvt = batch[0];
                    _inFlightEvents.Remove(invalidEvt.event_id);
                    AppendToQuarantine(invalidEvt, status, sendResult.RawBody);
                    SaveJournalToDisk();
                    _flushInProgress = false;
                }
            }
            else if (status == 429)
            {
                float delay = sendResult.RetryAfterSeconds.HasValue ? Mathf.Max(sendResult.RetryAfterSeconds.Value, 1) : 10f;
                Debug.LogWarning($"[TelemetryEmitter] HTTP 429 Rate limited. Re-queuing batch and backing off for {delay} seconds.");
                RequeueBatch(batch);
                _flushInProgress = false;

                yield return new WaitForSeconds(delay);
                StartCoroutine(FlushBatch(0));
            }
            else
            {
                // Transient server error (5xx) or network unreachable
                float delay = Mathf.Min(Mathf.Pow(2, Mathf.Min(retryCount, _maxRetryAttempts)), 30f);
                Debug.LogWarning($"[TelemetryEmitter] Transient failure (HTTP {status}, {sendResult.ErrorMessage}). Re-queuing batch for retry in {delay}s (attempt {retryCount + 1}). Zero patient data discarded.");
                RequeueBatch(batch);
                _flushInProgress = false;

                yield return new WaitForSeconds(delay);
                StartCoroutine(FlushBatch(retryCount + 1));
            }
        }

        private void RequeueBatch(List<InteractionEvent> batch)
        {
            // Remove batch events from in-flight tracking since they are returning to pending
            foreach (var e in batch)
            {
                _inFlightEvents.Remove(e.event_id);
            }

            var existing = new List<InteractionEvent>(_pendingQueue);
            _pendingQueue.Clear();

            var seenIds = new HashSet<string>();
            foreach (var e in batch)
            {
                if (seenIds.Add(e.event_id))
                    _pendingQueue.Enqueue(e);
            }
            foreach (var e in existing)
            {
                if (seenIds.Add(e.event_id))
                    _pendingQueue.Enqueue(e);
            }

            SaveJournalToDisk();
        }

        private InteractionEvent BuildEvent(string patientId, string sessionId,
            TherapyArea therapyArea, string activityId,
            string eventType, InteractionPayload payload)
        {
            return new InteractionEvent
            {
                event_id      = Guid.NewGuid().ToString(),
                session_id    = sessionId,
                patient_id    = patientId,
                timestamp_utc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                therapy_area  = therapyArea,
                activity_id   = activityId,
                event_type    = eventType,
                payload       = payload
            };
        }

        private bool ValidateEvent(InteractionEvent evt)
        {
            if (evt == null) return false;
            if (string.IsNullOrEmpty(evt.event_id))
            {
                Debug.LogError("[TelemetryEmitter] Event rejected: event_id is required.");
                return false;
            }
            if (string.IsNullOrEmpty(evt.session_id))
            {
                Debug.LogError("[TelemetryEmitter] Event rejected: session_id is required.");
                return false;
            }
            if (string.IsNullOrEmpty(evt.patient_id))
            {
                Debug.LogError("[TelemetryEmitter] Event rejected: patient_id is required.");
                return false;
            }
            if (string.IsNullOrEmpty(evt.timestamp_utc))
            {
                Debug.LogError("[TelemetryEmitter] Event rejected: timestamp_utc is required.");
                return false;
            }
            if (!Enum.IsDefined(typeof(TherapyArea), evt.therapy_area))
            {
                Debug.LogError($"[TelemetryEmitter] Event rejected: invalid therapy_area '{(int)evt.therapy_area}'.");
                return false;
            }
            if (string.IsNullOrEmpty(evt.activity_id))
            {
                Debug.LogError("[TelemetryEmitter] Event rejected: activity_id is required.");
                return false;
            }
            if (string.IsNullOrEmpty(evt.event_type))
            {
                Debug.LogError("[TelemetryEmitter] Event rejected: event_type is required.");
                return false;
            }
            return true;
        }

        private void OnApplicationQuit()
        {
            SaveJournalToDisk();
            if (_pendingQueue.Count > 0 && !_flushInProgress)
            {
                Debug.Log($"[TelemetryEmitter] Application quitting with {_pendingQueue.Count} pending event(s). Attempting final flush.");
                FlushNow();
            }
        }
    }
}
