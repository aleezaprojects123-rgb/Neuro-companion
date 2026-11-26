// Phase5_2_VerificationTest.cs
// NeuroCompanion — Phase 5.2 Verification Test Suite
//
// Tests:
//  1. TelemetryTracker initialization and state tracking.
//  2. Step lifecycle, attempt counts, and error burst logic.
//  3. Patient intent flags (help, repeat, break, dismiss).
//  4. Patient autonomy actions (pause, resume, skip) without error penalties.
//  5. Schema validation rules (non-negative counts, millisecond bounds).
//  6. InteractionTelemetryContextDto generation matching Phase 4 schemas.
//  7. Live end-to-end telemetry evaluation via FastAPI backend.

using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using NeuroCompanion.Backend;

namespace NeuroCompanion.Tests
{
    public static class Phase5_2_VerificationTest
    {
        private static int _testsPassed = 0;
        private static int _testsFailed = 0;

        public static void RunAllTests()
        {
            Debug.Log("==========================================================");
            Debug.Log("[Phase 5.2 Test Harness] Starting Telemetry Verification...");
            Debug.Log("==========================================================");

            _testsPassed = 0;
            _testsFailed = 0;

            Test01_TrackerInitializationAndStepLifecycle();
            Test02_AttemptTrackingAndErrorCounts();
            Test03_PatientIntentFlags();
            Test04_PatientAutonomyPauseSkipNonPenalizing();
            Test05_TelemetryValidationAndSanitization();
            Test06_ContextDtoGenerationAndBackendCompatibility();
            Test07_LiveBackendAssistanceWithTelemetryContext();

            Debug.Log("==========================================================");
            Debug.Log($"[Phase 5.2 Test Harness] Complete: {_testsPassed} PASSED, {_testsFailed} FAILED.");
            Debug.Log("==========================================================");
        }

        private static void AssertTrue(bool condition, string testName, string details = "")
        {
            if (condition)
            {
                _testsPassed++;
                Debug.Log($"[PASS] {testName} {details}");
            }
            else
            {
                _testsFailed++;
                Debug.LogError($"[FAIL] {testName} {details}");
            }
        }

        private static void Test01_TrackerInitializationAndStepLifecycle()
        {
            var go = new GameObject("TestTracker_01");
            var tracker = go.AddComponent<PatientTelemetryTracker>();

            tracker.StartActivity("P001", "SES-101", "OrientationClockTask", TherapyArea.OrientationRoom, "orientation", 3);

            AssertTrue(tracker.PatientId == "P001", "Test01: PatientId correctly set");
            AssertTrue(tracker.SessionId == "SES-101", "Test01: SessionId correctly set");
            AssertTrue(tracker.CurrentActivityId == "OrientationClockTask", "Test01: ActivityId set");
            AssertTrue(tracker.CurrentTherapyArea == TherapyArea.OrientationRoom, "Test01: TherapyArea set");
            AssertTrue(tracker.CurrentStepIndex == 1, "Test01: Step index initialized to 1");
            AssertTrue(tracker.StepTotalSteps == 3, "Test01: Total steps is 3");
            AssertTrue(tracker.IsActivityActive == true, "Test01: Activity is active");

            tracker.BeginStep(2, "Find the number 12 on the clock");
            AssertTrue(tracker.CurrentStepIndex == 2, "Test01: Step index advanced to 2");

            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void Test02_AttemptTrackingAndErrorCounts()
        {
            var go = new GameObject("TestTracker_02");
            var tracker = go.AddComponent<PatientTelemetryTracker>();

            tracker.StartActivity("P001", "SES-101", "FlowerColorMatch", TherapyArea.CognitiveGarden, "cognitive_selection", 2);

            // Record incorrect attempt
            tracker.RecordAttempt(isCorrect: false, overrideLatencyMs: 1400f);
            AssertTrue(tracker.StepErrorCount == 1, "Test02: Step error count is 1");
            AssertTrue(tracker.CumulativeSessionErrors == 1, "Test02: Cumulative session errors is 1");

            // Record second incorrect attempt
            tracker.RecordAttempt(isCorrect: false, overrideLatencyMs: 2100f);
            AssertTrue(tracker.StepErrorCount == 2, "Test02: Step error count is 2");
            AssertTrue(tracker.CumulativeSessionErrors == 2, "Test02: Cumulative session errors is 2");

            // Record correct attempt
            tracker.RecordAttempt(isCorrect: true, overrideLatencyMs: 950f);
            AssertTrue(tracker.StepErrorCount == 2, "Test02: Step error count remains 2");
            AssertTrue(tracker.CumulativeSessionErrors == 2, "Test02: Cumulative session errors remains 2");

            // Advance step: step error count resets, cumulative remains
            tracker.BeginStep(2);
            AssertTrue(tracker.StepErrorCount == 0, "Test02: Step error count reset on new step");
            AssertTrue(tracker.CumulativeSessionErrors == 2, "Test02: Cumulative session errors preserved across steps");

            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void Test03_PatientIntentFlags()
        {
            var go = new GameObject("TestTracker_03");
            var tracker = go.AddComponent<PatientTelemetryTracker>();

            tracker.StartActivity("P001", "SES-101", "MemoryCardTask", TherapyArea.ReminiscenceCorner, "memory", 3);

            tracker.RecordPatientHelpRequest();
            tracker.RecordPatientRepeatRequest();
            tracker.RecordPatientBreakRequest();
            tracker.RecordPatientDismissedPrompt();
            tracker.RecordTargetMiss();
            tracker.RecordRepeatedClick();

            var snapshot = tracker.BuildCurrentContextSnapshot();

            AssertTrue(snapshot.patient_requested_help == true, "Test03: Help flag captured");
            AssertTrue(snapshot.patient_requested_repeat == true, "Test03: Repeat flag captured");
            AssertTrue(snapshot.patient_requested_break == true, "Test03: Break flag captured");
            AssertTrue(snapshot.patient_dismissed_prompt == true, "Test03: Dismiss flag captured");
            AssertTrue(snapshot.target_miss_count == 1, "Test03: Target miss count captured");
            AssertTrue(snapshot.repeated_click_count == 1, "Test03: Repeated click count captured");

            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void Test04_PatientAutonomyPauseSkipNonPenalizing()
        {
            var go = new GameObject("TestTracker_04");
            var tracker = go.AddComponent<PatientTelemetryTracker>();

            tracker.StartActivity("P001", "SES-101", "ClockOrientation", TherapyArea.OrientationRoom, "orientation", 2);

            tracker.PauseActivity();
            AssertTrue(tracker.IsActivityPaused == true, "Test04: Activity paused");

            tracker.ResumeActivity();
            AssertTrue(tracker.IsActivityPaused == false, "Test04: Activity resumed");

            // Autonomy guarantee: pausing and resuming must never increase error counts
            AssertTrue(tracker.StepErrorCount == 0, "Test04: Pause/resume does not increment errors");
            AssertTrue(tracker.CumulativeSessionErrors == 0, "Test04: Cumulative errors remains 0");

            tracker.SkipActivity();
            AssertTrue(tracker.IsActivityActive == false, "Test04: Skip marks activity inactive without error");
            AssertTrue(tracker.CumulativeSessionErrors == 0, "Test04: Skip does not increment cumulative errors");

            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void Test05_TelemetryValidationAndSanitization()
        {
            var go = new GameObject("TestTracker_05");
            var tracker = go.AddComponent<PatientTelemetryTracker>();

            tracker.StartActivity("P001", "SES-101", "SanitizationTask", TherapyArea.CognitiveGarden, "selection", 1);

            // Attempt with negative latency must be clamped to >= 0
            tracker.RecordAttempt(false, overrideLatencyMs: -500f);
            var snapshot = tracker.BuildCurrentContextSnapshot();

            AssertTrue(snapshot.response_latency_ms >= 0f, "Test05: Negative latency clamped to >= 0");
            AssertTrue(snapshot.idle_duration_ms >= 0f, "Test05: Idle duration >= 0");
            AssertTrue(snapshot.step_index >= 1, "Test05: Step index >= 1");
            AssertTrue(snapshot.step_total_steps >= 1, "Test05: Total steps >= 1");
            AssertTrue(snapshot.step_error_count >= 0, "Test05: Step error count >= 0");
            AssertTrue(snapshot.target_miss_count >= 0, "Test05: Target miss count >= 0");
            AssertTrue(snapshot.repeated_click_count >= 0, "Test05: Repeated click count >= 0");
            AssertTrue(!string.IsNullOrEmpty(snapshot.client_version), "Test05: Client version present");

            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void Test06_ContextDtoGenerationAndBackendCompatibility()
        {
            var go = new GameObject("TestTracker_06");
            var tracker = go.AddComponent<PatientTelemetryTracker>();

            tracker.StartActivity("P001", "SES-101", "OrientationClock", TherapyArea.OrientationRoom, "orientation", 3);
            tracker.BeginStep(1);
            tracker.RecordPatientHelpRequest();

            var snapshot = tracker.BuildCurrentContextSnapshot();
            var req = new AssistanceEvaluateRequest
            {
                request_id = "phase5-2-test-req",
                seconds_since_last_prompt = 60.0f,
                context = snapshot
            };

            string json = JsonUtility.ToJson(req);

            AssertTrue(json.Contains("\"patient_id\":\"P001\""), "Test06: JSON contains patient_id");
            AssertTrue(json.Contains("\"session_id\":\"SES-101\""), "Test06: JSON contains session_id");
            AssertTrue(json.Contains("\"task_name\":\"OrientationClock\""), "Test06: JSON contains task_name");
            AssertTrue(json.Contains("\"patient_requested_help\":true"), "Test06: JSON contains patient_requested_help");

            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void Test07_LiveBackendAssistanceWithTelemetryContext()
        {
            var go = new GameObject("TestTracker_07");
            var tracker = go.AddComponent<PatientTelemetryTracker>();

            tracker.StartActivity("P001", "SES-101", "LiveTelemetryActivity", TherapyArea.OrientationRoom, "orientation", 2);
            tracker.BeginStep(1);
            // Simulate genuine patient interaction: patient pressed help
            tracker.RecordPatientHelpRequest();

            var snapshot = tracker.BuildCurrentContextSnapshot();
            var req = new AssistanceEvaluateRequest
            {
                request_id = "phase5-2-live-eval",
                seconds_since_last_prompt = 60.0f,
                context = snapshot
            };

            string url = "http://127.0.0.1:8000/api/v1/assistance/evaluate";
            string jsonBody = JsonUtility.ToJson(req);

            byte[] bytes = Encoding.UTF8.GetBytes(jsonBody);
            using var webReq = new UnityWebRequest(url, "POST");
            webReq.uploadHandler = new UploadHandlerRaw(bytes);
            webReq.downloadHandler = new DownloadHandlerBuffer();
            webReq.timeout = 8;
            webReq.SetRequestHeader("Content-Type", "application/json");
            webReq.SetRequestHeader("Accept", "application/json");

            var asyncOp = webReq.SendWebRequest();
            while (!asyncOp.isDone)
            {
                System.Threading.Thread.Sleep(10);
            }

            AssertTrue(webReq.result == UnityWebRequest.Result.Success, "Test07: Live POST evaluate with tracker snapshot success", $"code={webReq.responseCode}");
            AssertTrue(webReq.responseCode == 200, "Test07: HTTP 200 OK");

            var resp = JsonUtility.FromJson<AssistanceEvaluateResponse>(webReq.downloadHandler.text);
            AssertTrue(resp != null, "Test07: Response deserialized");
            AssertTrue(resp.patient_id == "P001", "Test07: PatientId matches in response");
            AssertTrue(resp.session_id == "SES-101", "Test07: SessionId matches in response");
            AssertTrue(!string.IsNullOrEmpty(resp.runtime_status), "Test07: Runtime status returned");

            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
