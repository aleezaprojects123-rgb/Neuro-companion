// AssistanceResponseParser.cs
// NeuroCompanion — Phase 5.3: AI Response Parsing and Validation
//
// Dedicated response processing layer that safely deserializes, validates, and
// normalizes responses from the Phase 4 FastAPI Backend (POST /api/v1/assistance/evaluate).
//
// Responsibilities:
//  - Safe deserialization using strongly-typed DTOs (BackendModels.cs).
//  - Validation of mandatory schema fields (request_id, patient_id, session_id, runtime_status).
//  - StrategyID normalization (AS01–AS10) with robust prefix matching.
//  - Evaluation of policy authorization (requires_carl_action flag).
//  - Suppression of AS10, denied, cooldown, or insufficient-data actions.
//  - Production of normalized AssistanceEvaluationResult structures.

using System;
using UnityEngine;

namespace NeuroCompanion.Backend
{
    /// <summary>
    /// Clean, normalized result representing an evaluated AI assistance decision.
    /// Grounded strictly in the Phase 4.8 Pydantic response schema.
    /// </summary>
    [Serializable]
    public class AssistanceEvaluationResult
    {
        public bool IsValid;
        public bool IsActionPermitted;
        public string RequestId;
        public string PatientId;
        public string SessionId;
        public string RuntimeStatus;
        public string DecisionId;
        public string DecisionStatus;
        public string StrategyId;          // Canonical AS01_REPEAT .. AS10_INDEPENDENT
        public string RawStrategyId;
        public string CanonicalActionType; // Canonical CarlActionType
        public bool IsOptional;
        public string ActionDescription;
        public string DecisionReason;
        public string[] PolicyRulesApplied;
        public bool CaregiverControlApplied;
        public bool PatientChoiceRequired;
        public string PatientChoiceStatus;
        public string CreatedAt;
        public string ErrorMessage;

        public static AssistanceEvaluationResult CreateError(string errorMsg, string reqId = null)
        {
            return new AssistanceEvaluationResult
            {
                IsValid = false,
                IsActionPermitted = false,
                RequestId = reqId ?? "error-req",
                RuntimeStatus = "ERROR",
                ErrorMessage = errorMsg
            };
        }

        public static AssistanceEvaluationResult CreateNoAction(string reason, string reqId = null, string decisionStatus = "NO_ACTION")
        {
            return new AssistanceEvaluationResult
            {
                IsValid = true,
                IsActionPermitted = false,
                RequestId = reqId,
                RuntimeStatus = "NEUTRAL",
                DecisionStatus = decisionStatus,
                DecisionReason = reason
            };
        }
    }

    /// <summary>
    /// Static parsing and validation engine for AI assistance responses.
    /// </summary>
    public static class AssistanceResponseParser
    {
        /// <summary>
        /// Parses a raw JSON response string into an AssistanceEvaluationResult.
        /// </summary>
        public static AssistanceEvaluationResult ParseJson(string jsonResponse)
        {
            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                return AssistanceEvaluationResult.CreateError("Empty or null JSON response string.");
            }

            try
            {
                var dto = JsonUtility.FromJson<AssistanceEvaluateResponse>(jsonResponse);
                if (dto == null)
                {
                    return AssistanceEvaluationResult.CreateError("JsonUtility returned null object.");
                }
                return ValidateAndNormalize(dto);
            }
            catch (Exception ex)
            {
                return AssistanceEvaluationResult.CreateError($"JSON Deserialization Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates and normalizes an already deserialized AssistanceEvaluateResponse DTO.
        /// </summary>
        public static AssistanceEvaluationResult ValidateAndNormalize(AssistanceEvaluateResponse dto)
        {
            if (dto == null)
            {
                return AssistanceEvaluationResult.CreateError("AssistanceEvaluateResponse DTO is null.");
            }

            // 1. Mandatory schema field validation
            if (string.IsNullOrWhiteSpace(dto.patient_id) || string.IsNullOrWhiteSpace(dto.session_id))
            {
                return AssistanceEvaluationResult.CreateError(
                    "Missing mandatory patient_id or session_id in backend response.", dto.request_id);
            }

            string runtimeStatus = dto.runtime_status ?? "UNKNOWN";
            if (runtimeStatus.Equals("ERROR", StringComparison.OrdinalIgnoreCase))
            {
                return AssistanceEvaluationResult.CreateError(
                    dto.decision_reason ?? "Backend returned runtime ERROR status.", dto.request_id);
            }

            var result = new AssistanceEvaluationResult
            {
                IsValid = true,
                RequestId = dto.request_id,
                PatientId = dto.patient_id,
                SessionId = dto.session_id,
                RuntimeStatus = runtimeStatus,
                DecisionId = dto.decision_id,
                DecisionStatus = dto.decision_status ?? "UNKNOWN",
                DecisionReason = dto.decision_reason ?? "",
                PolicyRulesApplied = dto.policy_rules_applied ?? new string[0],
                CaregiverControlApplied = dto.caregiver_control_applied,
                PatientChoiceRequired = dto.patient_choice_required,
                PatientChoiceStatus = dto.patient_choice_status ?? "NOT_REQUIRED",
                CreatedAt = dto.created_at ?? ""
            };

            // 2. Normalize Strategy ID
            result.RawStrategyId = dto.selected_strategy_id;
            result.StrategyId = NormalizeStrategyId(dto.selected_strategy_id);

            // 3. Evaluate Authorization (requires_carl_action flag)
            // Safety Rule: Only when requires_carl_action is TRUE does Carl have authorization to act!
            if (!dto.requires_carl_action)
            {
                result.IsActionPermitted = false;
                result.ActionDescription = "Policy did not authorize a Carl companion action.";
                return result;
            }

            // 4. Special Rule for AS10 (Independent Completion)
            // Even if authorized, AS10 explicitly mandates patient independence without intervention.
            if (result.StrategyId == StrategyID.AS10_INDEPENDENT)
            {
                result.IsActionPermitted = false;
                result.CanonicalActionType = CarlActionType.ALLOW_INDEPENDENT_COMPLETION;
                result.ActionDescription = "Patient completing independently. Carl remains silent and attentive.";
                return result;
            }

            // 5. Validate CarlAction payload
            if (dto.carl_action == null)
            {
                result.IsActionPermitted = false;
                result.ErrorMessage = "requires_carl_action was true, but carl_action payload was null.";
                return result;
            }

            result.CanonicalActionType = NormalizeActionType(dto.carl_action.action_type);
            result.IsOptional = dto.carl_action.is_optional;
            result.ActionDescription = dto.carl_action.description ?? "";

            // Strategy AS08 must always be optional (never forced break)
            if (result.StrategyId == StrategyID.AS08_BREAK_PAUSE)
            {
                result.IsOptional = true;
            }

            // If action type is unknown or invalid, do not permit execution
            if (string.IsNullOrEmpty(result.CanonicalActionType))
            {
                result.IsActionPermitted = false;
                result.ErrorMessage = $"Unsupported or invalid CarlActionType: {dto.carl_action.action_type}";
                return result;
            }

            result.IsActionPermitted = true;
            return result;
        }

        /// <summary>
        /// Normalizes arbitrary strategy identifier strings to canonical StrategyID constants.
        /// Handles prefix matching (AS01..AS10) seamlessly.
        /// </summary>
        public static string NormalizeStrategyId(string rawStrategyId)
        {
            if (string.IsNullOrWhiteSpace(rawStrategyId)) return null;

            string upper = rawStrategyId.Trim().ToUpperInvariant();

            if (upper.StartsWith("AS01")) return StrategyID.AS01_REPEAT;
            if (upper.StartsWith("AS02")) return StrategyID.AS02_SIMPLIFY;
            if (upper.StartsWith("AS03")) return StrategyID.AS03_STEP_GUIDANCE;
            if (upper.StartsWith("AS04")) return StrategyID.AS04_VISUAL_HINT;
            if (upper.StartsWith("AS05")) return StrategyID.AS05_DEMONSTRATION;
            if (upper.StartsWith("AS06")) return StrategyID.AS06_SLOWER_SPEECH;
            if (upper.StartsWith("AS07")) return StrategyID.AS07_RESPONSE_TIME;
            if (upper.StartsWith("AS08")) return StrategyID.AS08_BREAK_PAUSE;
            if (upper.StartsWith("AS09")) return StrategyID.AS09_VERBAL_EXPLANATION;
            if (upper.StartsWith("AS10")) return StrategyID.AS10_INDEPENDENT;

            return null;
        }

        /// <summary>
        /// Normalizes raw action type strings to canonical CarlActionType constants.
        /// </summary>
        public static string NormalizeActionType(string rawActionType)
        {
            if (string.IsNullOrWhiteSpace(rawActionType)) return null;

            string upper = rawActionType.Trim().ToUpperInvariant();

            if (upper.Contains("REPEAT")) return CarlActionType.REPEAT_CURRENT_INSTRUCTION;
            if (upper.Contains("SIMPLIFY")) return CarlActionType.SIMPLIFY_CURRENT_INSTRUCTION;
            if (upper.Contains("STEP")) return CarlActionType.PROVIDE_STEP_BY_STEP_GUIDANCE;
            if (upper.Contains("HINT")) return CarlActionType.SHOW_VISUAL_HINT;
            if (upper.Contains("DEMONSTRATION")) return CarlActionType.SHOW_DEMONSTRATION;
            if (upper.Contains("SLOW")) return CarlActionType.SLOW_SPEECH;
            if (upper.Contains("RESPONSE_TIME") || upper.Contains("ADDITIONAL")) return CarlActionType.ALLOW_ADDITIONAL_RESPONSE_TIME;
            if (upper.Contains("BREAK") || upper.Contains("PAUSE")) return CarlActionType.OFFER_OPTIONAL_BREAK;
            if (upper.Contains("EXPLANATION") || upper.Contains("VERBAL")) return CarlActionType.PROVIDE_VERBAL_EXPLANATION;
            if (upper.Contains("INDEPENDENT") || upper.Contains("SILENT")) return CarlActionType.ALLOW_INDEPENDENT_COMPLETION;

            return null;
        }
    }
}
