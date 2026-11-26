// Phase5_1_VerificationTest.cs
// NeuroCompanion — Phase 5.1 Verification Test Harness
//
// Runs in Unity batchmode (-executeMethod NeuroCompanion.Tests.Phase5_1_VerificationTest.RunAllTests)
// Verifies:
//  1. C# Serialization of AssistanceEvaluateRequest to valid JSON
//  2. C# Deserialization of live backend AssistanceEvaluateResponse and CarlActionDto
//  3. Live HTTP GET /api/v1/health via UnityWebRequest
//  4. Live HTTP POST /api/v1/assistance/evaluate via UnityWebRequest
//  5. Live HTTP 422 Unprocessable Entity error handling
//  6. Unreachable backend / network error handling

using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using NeuroCompanion.Backend;

namespace NeuroCompanion.Tests
{
    public static class Phase5_1_VerificationTest
    {
        private static int _testsPassed = 0;
        private static int _testsFailed = 0;

        public static void RunAllTests()
        {
            Debug.Log("==========================================================");
            Debug.Log("[Phase 5.1 Test Harness] Starting Verification Suite...");
            Debug.Log("==========================================================");

            _testsPassed = 0;
            _testsFailed = 0;

            Test01_RequestSerialization();
            Test02_ResponseDeserialization();
            Test03_LiveBackendHealth();
            Test04_LiveAssistanceEvaluate();
            Test05_LiveInvalidRequest422Handling();
            Test06_UnreachableHostHandling();

            Debug.Log("==========================================================");
            Debug.Log($"[Phase 5.1 Test Harness] Complete: {_testsPassed} PASSED, {_testsFailed} FAILED.");
            Debug.Log("==========================================================");

            if (_testsFailed > 0)
            {
                EditorApplication.Exit(1);
            }
            else
            {
                EditorApplication.Exit(0);
            }
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

        private static void Test01_RequestSerialization()
        {
            var req = AssistanceEvaluateRequest.Create(
                patientId: "P001",
                sessionId: "SES-101",
                taskName: "OrientationCardSelect",
                stepIndex: 2,
                stepTotalSteps: 5,
                idleDurationMs: 4200f,
                stepErrorCount: 1,
                patientRequestedHelp: false,
                caregiverLockedStrategy: "AS04_VISUAL_HINT"
            );

            AssertTrue(req.context != null, "Test01: Request context created");
            AssertTrue(req.context.patient_id == "P001", "Test01: PatientId matches");
            AssertTrue(req.context.step_index == 2, "Test01: Step index matches");
            AssertTrue(req.context.caregiver_locked_strategy == "AS04_VISUAL_HINT", "Test01: Caregiver strategy set");

            string json = JsonUtility.ToJson(req);
            AssertTrue(json.Contains("\"patient_id\":\"P001\""), "Test01: JSON contains patient_id");
            AssertTrue(json.Contains("\"caregiver_locked_strategy\":\"AS04_VISUAL_HINT\""), "Test01: JSON contains caregiver_locked_strategy");
        }

        private static void Test02_ResponseDeserialization()
        {
            string sampleJsonResponse = @"{
                ""request_id"": ""test-req-001"",
                ""patient_id"": ""P001"",
                ""session_id"": ""SES-101"",
                ""runtime_status"": ""APPROVED"",
                ""decision_id"": ""dec-1234-uuid"",
                ""decision_status"": ""APPROVED"",
                ""selected_strategy_id"": ""AS01_REPEAT_INSTRUCTION"",
                ""decision_reason"": ""Idle latency (3500ms) exceeded threshold (1200.0ms)."",
                ""policy_rules_applied"": [""CDR_PROHIBITION"", ""DATA_SUFFICIENCY_CHECK""],
                ""caregiver_control_applied"": false,
                ""patient_choice_required"": false,
                ""patient_choice_status"": ""NOT_REQUIRED"",
                ""requires_carl_action"": true,
                ""carl_action"": {
                    ""action_type"": ""REPEAT_CURRENT_INSTRUCTION"",
                    ""strategy_id"": ""AS01_REPEAT_INSTRUCTION"",
                    ""is_optional"": false,
                    ""description"": ""Carl executes REPEAT_CURRENT_INSTRUCTION based on evaluated safe policy recommendation.""
                },
                ""created_at"": ""2026-11-21T19:46:38.216065+00:00""
            }";

            var resp = JsonUtility.FromJson<AssistanceEvaluateResponse>(sampleJsonResponse);

            AssertTrue(resp != null, "Test02: Response not null");
            AssertTrue(resp.request_id == "test-req-001", "Test02: RequestId matches");
            AssertTrue(resp.patient_id == "P001", "Test02: PatientId matches");
            AssertTrue(resp.runtime_status == "APPROVED", "Test02: Runtime status matches");
            AssertTrue(resp.decision_status == "APPROVED", "Test02: Decision status matches");
            AssertTrue(resp.selected_strategy_id == "AS01_REPEAT_INSTRUCTION", "Test02: Strategy ID matches");
            AssertTrue(resp.requires_carl_action == true, "Test02: requires_carl_action is true");
            AssertTrue(resp.policy_rules_applied != null && resp.policy_rules_applied.Length == 2, "Test02: Policy rules array parsed");
            AssertTrue(resp.carl_action != null, "Test02: CarlActionDto parsed");
            AssertTrue(resp.carl_action.action_type == CarlActionType.REPEAT_CURRENT_INSTRUCTION, "Test02: CarlActionType constant matches");
            AssertTrue(resp.carl_action.is_optional == false, "Test02: is_optional is false");
        }

        private static void Test03_LiveBackendHealth()
        {
            string url = "http://127.0.0.1:8000/api/v1/health";
            using var req = UnityWebRequest.Get(url);
            req.timeout = 5;
            var asyncOp = req.SendWebRequest();

            while (!asyncOp.isDone)
            {
                System.Threading.Thread.Sleep(10);
            }

            AssertTrue(req.result == UnityWebRequest.Result.Success, "Test03: GET /health Success", $"code={req.responseCode}");
            AssertTrue(req.responseCode == 200, "Test03: HTTP 200 OK");

            string text = req.downloadHandler.text;
            var health = JsonUtility.FromJson<HealthResponse>(text);
            AssertTrue(health != null && health.status == "ok", "Test03: Health status is ok");
            AssertTrue(health.model_loaded == true, "Test03: Model is loaded");
        }

        private static void Test04_LiveAssistanceEvaluate()
        {
            string url = "http://127.0.0.1:8000/api/v1/assistance/evaluate";
            string jsonBody = @"{
                ""request_id"": ""unity-eval-test-01"",
                ""seconds_since_last_prompt"": 60.0,
                ""context"": {
                    ""session_id"": ""SES-101"",
                    ""patient_id"": ""P001"",
                    ""task_name"": ""OrientationCardSelect"",
                    ""task_domain"": ""cognitive_selection"",
                    ""step_index"": 1,
                    ""step_total_steps"": 3,
                    ""response_latency_ms"": 1200.0,
                    ""idle_duration_ms"": 3500.0,
                    ""target_miss_count"": 0,
                    ""repeated_click_count"": 0,
                    ""step_error_count"": 0,
                    ""cumulative_session_errors"": 0,
                    ""consecutive_error_count"": 0,
                    ""session_duration_minutes"": 2.5,
                    ""patient_requested_help"": false,
                    ""patient_requested_repeat"": false,
                    ""patient_requested_break"": false,
                    ""patient_dismissed_prompt"": false,
                    ""client_version"": ""3.0.0""
                }
            }";

            byte[] bytes = Encoding.UTF8.GetBytes(jsonBody);
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = 8;
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/json");

            var asyncOp = req.SendWebRequest();
            while (!asyncOp.isDone)
            {
                System.Threading.Thread.Sleep(10);
            }

            AssertTrue(req.result == UnityWebRequest.Result.Success, "Test04: POST /assistance/evaluate Success", $"code={req.responseCode}");
            AssertTrue(req.responseCode == 200, "Test04: HTTP 200 OK");

            string text = req.downloadHandler.text;
            var response = JsonUtility.FromJson<AssistanceEvaluateResponse>(text);
            AssertTrue(response != null, "Test04: Response deserialized");
            AssertTrue(response.request_id == "unity-eval-test-01", "Test04: RequestId matches");
            AssertTrue(response.decision_status == "APPROVED", "Test04: Decision status is APPROVED");
            AssertTrue(response.carl_action != null, "Test04: CarlAction is present");
            AssertTrue(response.carl_action.action_type == CarlActionType.REPEAT_CURRENT_INSTRUCTION, "Test04: ActionType is REPEAT_CURRENT_INSTRUCTION");
        }

        private static void Test05_LiveInvalidRequest422Handling()
        {
            string url = "http://127.0.0.1:8000/api/v1/assistance/evaluate";
            // Missing required patient_id and session_id
            string invalidBody = @"{
                ""request_id"": ""invalid-req-001"",
                ""context"": {
                    ""patient_id"": """",
                    ""session_id"": """"
                }
            }";

            byte[] bytes = Encoding.UTF8.GetBytes(invalidBody);
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = 5;
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/json");

            var asyncOp = req.SendWebRequest();
            while (!asyncOp.isDone)
            {
                System.Threading.Thread.Sleep(10);
            }

            AssertTrue(req.responseCode == 422, "Test05: Returns HTTP 422 for missing identifiers", $"code={req.responseCode}");
            AssertTrue(req.downloadHandler.text.Contains("patient_id"), "Test05: 422 response body contains detail message");
        }

        private static void Test06_UnreachableHostHandling()
        {
            string url = "http://127.0.0.1:9999/api/v1/health";
            using var req = UnityWebRequest.Get(url);
            req.timeout = 2;

            var asyncOp = req.SendWebRequest();
            while (!asyncOp.isDone)
            {
                System.Threading.Thread.Sleep(10);
            }

            AssertTrue(req.result == UnityWebRequest.Result.ConnectionError, "Test06: Reports ConnectionError for unreachable port", $"error={req.error}");
        }
    }
}
