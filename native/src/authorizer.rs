//! Authorization over a verified token: ambient facts plus policies evaluated
//! by upstream, reported as a decision with structured errors.
//!
//! The token is verified against the root key first. Facts, checks, and
//! policies arrive as Datalog source parsed per item by upstream `code`, so a
//! malformed policy is a build-time `datalog_error`, never an authorization.
//! An evaluation that runs and denies (no matching policy, failed checks,
//! explicit deny, limits) is an ordinary `allow`/`deny` answer with error
//! entries — never a bridge failure and never an allow.
//!
//! No ambient time fact is injected: time-dependent policies need an explicit
//! `time(...)` fact from the caller, keeping evaluation deterministic.
//! Upstream default execution limits apply.

use biscuit_auth::error::{Logic as LogicError, Token as TokenError};
use biscuit_auth::{AuthorizerBuilder, Biscuit};

use crate::tokens::token_error;
use crate::{emit_error, emit_owned, BiscuitSharpBuffer, STATUS_INVALID_INPUT};

pub const OP_TOKEN_AUTHORIZE: u32 = 10;

fn invalid(output: *mut BiscuitSharpBuffer, message: String) -> u32 {
    emit_error(output, STATUS_INVALID_INPUT, "invalid_input", &message)
}

fn parse_request(input: &[u8]) -> Result<serde_json::Value, String> {
    serde_json::from_slice(input).map_err(|e| format!("invalid JSON request: {e}"))
}

fn decode_sources(
    req: &serde_json::Value,
    field: &str,
    output: *mut BiscuitSharpBuffer,
) -> Result<Vec<String>, u32> {
    match req.get(field) {
        None => Ok(Vec::new()),
        Some(v) => match serde_json::from_value::<Vec<String>>(v.clone()) {
            Ok(items) => Ok(items),
            Err(e) => Err(invalid(
                output,
                format!("field '{field}' must be an array of strings: {e}"),
            )),
        },
    }
}

fn failed_check_entry(check: &biscuit_auth::error::FailedCheck) -> serde_json::Value {
    use biscuit_auth::error::FailedCheck::*;
    match check {
        Block(c) => serde_json::json!({
            "code": "failed_check",
            "message": check.to_string(),
            "block_id": c.block_id,
            "check_id": c.check_id,
            "rule": c.rule,
        }),
        Authorizer(c) => serde_json::json!({
            "code": "failed_check",
            "message": check.to_string(),
            "block_id": null,
            "check_id": c.check_id,
            "rule": c.rule,
        }),
    }
}

fn emit_answer(
    output: *mut BiscuitSharpBuffer,
    decision: &str,
    allow_policy_index: Option<usize>,
    deny_policy_index: Option<usize>,
    errors: Vec<serde_json::Value>,
) -> u32 {
    emit_owned(
        output,
        serde_json::json!({
            "decision": decision,
            "allow_policy_index": allow_policy_index,
            "deny_policy_index": deny_policy_index,
            "errors": errors,
        })
        .to_string()
        .into_bytes(),
    )
}

/// Maps an evaluation outcome to the `allow`/`deny` answer contract.
/// `Ok(index)` is a clean allow. Every `FailedLogic` shape is a deny with
/// structured errors; anything else that escapes evaluation is a deny with an
/// `evaluation_failure` entry. No path reports allow.
fn emit_authorization(
    output: *mut BiscuitSharpBuffer,
    outcome: Result<usize, TokenError>,
) -> u32 {
    match outcome {
        Ok(index) => emit_answer(output, "allow", Some(index), None, Vec::new()),
        Err(TokenError::FailedLogic(logic)) => match logic {
            LogicError::Unauthorized { policy, checks } => {
                use biscuit_auth::error::MatchedPolicy::*;
                let mut errors: Vec<serde_json::Value> = checks.iter().map(failed_check_entry).collect();
                match policy {
                    Allow(index) => {
                        errors.push(serde_json::json!({
                            "code": "allow_policy_matched",
                            "message": format!(
                                "an allow policy matched (policy index: {index}) but {} check(s) failed",
                                errors.len(),
                            ),
                            "block_id": null,
                            "check_id": null,
                            "rule": null,
                        }));
                        emit_answer(output, "deny", Some(index), None, errors)
                    }
                    Deny(index) => {
                        errors.push(serde_json::json!({
                            "code": "deny_policy_matched",
                            "message": format!("a deny policy matched (policy index: {index})"),
                            "block_id": null,
                            "check_id": null,
                            "rule": null,
                        }));
                        emit_answer(output, "deny", None, Some(index), errors)
                    }
                }
            }
            LogicError::NoMatchingPolicy { checks } => {
                let mut errors: Vec<serde_json::Value> =
                    checks.iter().map(failed_check_entry).collect();
                errors.push(serde_json::json!({
                    "code": "no_matching_policy",
                    "message": "no allow policy matched",
                    "block_id": null,
                    "check_id": null,
                    "rule": null,
                }));
                emit_answer(output, "deny", None, None, errors)
            }
            LogicError::InvalidBlockRule(block, rule) => emit_answer(
                output,
                "deny",
                None,
                None,
                vec![serde_json::json!({
                    "code": "invalid_block_rule",
                    "message": format!("a rule in block {block} produces unbound variables: {rule}"),
                    "block_id": block,
                    "check_id": null,
                    "rule": rule,
                })],
            ),
            #[allow(unreachable_patterns)]
            _ => emit_answer(
                output,
                "deny",
                None,
                None,
                vec![serde_json::json!({
                    "code": "evaluation_failure",
                    "message": format!("authorization evaluation failed: {logic}"),
                    "block_id": null,
                    "check_id": null,
                    "rule": null,
                })],
            ),
        },
        Err(e) => emit_answer(
            output,
            "deny",
            None,
            None,
            vec![serde_json::json!({
                "code": "evaluation_failure",
                "message": format!("authorization evaluation failed: {e}"),
                "block_id": null,
                "check_id": null,
                "rule": null,
            })],
        ),
    }
}

/// OP_TOKEN_AUTHORIZE: `{"token", "root", "facts": [...], "checks": [...],
/// "policies": [...]}` → the decision answer. Malformed Datalog fails the
/// build with `datalog_error`; only completed evaluations produce answers.
pub fn op_token_authorize(input: &[u8], output: *mut BiscuitSharpBuffer) -> u32 {
    let req = match parse_request(input) {
        Ok(v) => v,
        Err(e) => return invalid(output, e),
    };
    let token = match crate::tokens::decode_token(&req) {
        Ok(t) => t,
        Err(e) => return invalid(output, e),
    };
    let root = match crate::tokens::decode_root(&req) {
        Ok(r) => r,
        Err(e) => return invalid(output, e),
    };
    let biscuit = match Biscuit::from(&token, root) {
        Ok(b) => b,
        Err(e) => return token_error(output, e),
    };
    let facts = match decode_sources(&req, "facts", output) {
        Ok(items) => items,
        Err(status) => return status,
    };
    let checks = match decode_sources(&req, "checks", output) {
        Ok(items) => items,
        Err(status) => return status,
    };
    let policies = match decode_sources(&req, "policies", output) {
        Ok(items) => items,
        Err(status) => return status,
    };
    let mut builder = AuthorizerBuilder::new();
    for source in facts.iter().chain(checks.iter()).chain(policies.iter()) {
        builder = match builder.code(source) {
            Ok(b) => b,
            Err(e) => return token_error(output, e),
        };
    }
    let mut authorizer = match builder.build(&biscuit) {
        Ok(a) => a,
        Err(e) => return token_error(output, e),
    };
    emit_authorization(output, authorizer.authorize())
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::STATUS_OK;
    use std::ptr;

    fn call(op: u32, body: serde_json::Value) -> (u32, serde_json::Value) {
        let bytes = body.to_string().into_bytes();
        let mut out = BiscuitSharpBuffer {
            data: ptr::null_mut(),
            len: 0,
        };
        let status = crate::biscuitsharp_call_v1(op, bytes.as_ptr(), bytes.len(), &mut out);
        assert!(!out.data.is_null() && out.len != 0, "op must emit a body");
        let slice =
            unsafe { std::slice::from_raw_parts(out.data as *const u8, out.len) };
        let value: serde_json::Value =
            serde_json::from_slice(slice).expect("output must be valid JSON");
        crate::biscuitsharp_free_v1(out);
        (status, value)
    }

    fn generate_root(algorithm: &str) -> (u64, serde_json::Value) {
        let (status, v) = call(
            crate::keys::OP_KEY_GENERATE,
            serde_json::json!({ "algorithm": algorithm }),
        );
        assert_eq!(status, STATUS_OK, "generate: {v}");
        let handle = v["handle"].as_u64().expect("handle");
        let (status, v) = call(
            crate::keys::OP_KEY_EXPORT_PUBLIC,
            serde_json::json!({ "handle": handle }),
        );
        assert_eq!(status, STATUS_OK, "export public: {v}");
        let root = serde_json::json!({ "algorithm": v["algorithm"], "public_key": v["public_key"] });
        (handle, root)
    }

    fn create(handle: u64, facts: Vec<&str>) -> Vec<u8> {
        let items: Vec<serde_json::Value> =
            facts.iter().map(|f| serde_json::json!({ "source": f })).collect();
        let (status, v) = call(
            crate::tokens::OP_TOKEN_CREATE,
            serde_json::json!({ "root_handle": handle, "facts": items }),
        );
        assert_eq!(status, STATUS_OK, "create: {v}");
        base64::decode(v["token"].as_str().expect("token")).expect("base64")
    }

    fn authorize(
        token: &[u8],
        root: &serde_json::Value,
        facts: Vec<&str>,
        checks: Vec<&str>,
        policies: Vec<&str>,
    ) -> (u32, serde_json::Value) {
        call(
            OP_TOKEN_AUTHORIZE,
            serde_json::json!({
                "token": base64::encode(token),
                "root": root,
                "facts": facts,
                "checks": checks,
                "policies": policies,
            }),
        )
    }

    #[test]
    fn allow_reports_policy_index_without_errors() {
        let (handle, root) = generate_root("ed25519");
        let token = create(handle, vec!["right(\"workspace.main\", \"read\")"]);
        let (status, v) = authorize(
            &token,
            &root,
            vec!["resource(\"/src/Foo.cs\")", "operation(\"read\")"],
            vec![],
            vec!["allow if right(\"workspace.main\", \"read\");"],
        );
        assert_eq!(status, STATUS_OK);
        assert_eq!(v["decision"], "allow");
        assert_eq!(v["allow_policy_index"], 0);
        assert!(v["deny_policy_index"].is_null());
        assert_eq!(v["errors"].as_array().expect("errors").len(), 0);
        let _ = handle;
    }

    #[test]
    fn deny_without_match_lists_no_matching_policy() {
        let (handle, root) = generate_root("ed25519");
        let token = create(handle, vec!["right(\"workspace.main\", \"read\")"]);
        let (status, v) = authorize(
            &token,
            &root,
            vec!["operation(\"write\")"],
            vec![],
            vec!["allow if right(\"workspace.main\", \"write\");"],
        );
        assert_eq!(status, STATUS_OK);
        assert_eq!(v["decision"], "deny");
        assert!(v["allow_policy_index"].is_null());
        let errors = v["errors"].as_array().expect("errors");
        assert!(errors.iter().any(|e| e["code"] == "no_matching_policy"));
        let _ = handle;
    }

    #[test]
    fn allow_matched_but_check_failed_is_deny_with_failed_checks() {
        let (handle, root) = generate_root("ed25519");
        let token = create(handle, vec!["right(\"workspace.main\", \"read\")"]);
        // Attenuate to read-only, then request write: the allow policy matches
        // on the token facts, but the attenuation check fails.
        let (status, v) = call(
            crate::tokens::OP_TOKEN_ATTENUATE,
            serde_json::json!({
                "token": base64::encode(&token),
                "root": root,
                "block": { "source": "check if operation(\"read\");" },
            }),
        );
        assert_eq!(status, STATUS_OK, "attenuate: {v}");
        let child = base64::decode(v["token"].as_str().expect("token")).expect("base64");
        let (status, v) = authorize(
            &child,
            &root,
            vec!["operation(\"write\")"],
            vec![],
            vec!["allow if right(\"workspace.main\", \"read\");"],
        );
        assert_eq!(status, STATUS_OK);
        assert_eq!(v["decision"], "deny");
        assert_eq!(v["allow_policy_index"], 0, "allow matched: {v}");
        assert!(v["deny_policy_index"].is_null());
        let errors = v["errors"].as_array().expect("errors");
        let failed: Vec<&serde_json::Value> =
            errors.iter().filter(|e| e["code"] == "failed_check").collect();
        assert!(!failed.is_empty(), "failed checks reported: {v}");
        assert!(failed.iter().any(|e| e["rule"]
            .as_str()
            .expect("rule")
            .contains("operation")));
        assert!(failed.iter().all(|e| !e["block_id"].is_null()));
        // The same request for "read" is allowed: attenuation narrows, not widens.
        let (status, v) = authorize(
            &child,
            &root,
            vec!["operation(\"read\")"],
            vec![],
            vec!["allow if right(\"workspace.main\", \"read\");"],
        );
        assert_eq!(status, STATUS_OK);
        assert_eq!(v["decision"], "allow");
        let _ = handle;
    }

    #[test]
    fn explicit_deny_reports_policy_index() {
        let (handle, root) = generate_root("ed25519");
        let token = create(handle, vec!["right(\"workspace.main\", \"read\")"]);
        // Policies are first-match-wins in order, so the deny comes first.
        let (status, v) = authorize(
            &token,
            &root,
            vec!["operation(\"read\")", "banned(\"workspace.main\")"],
            vec![],
            vec![
                "deny if banned(\"workspace.main\");",
                "allow if right(\"workspace.main\", \"read\");",
            ],
        );
        assert_eq!(status, STATUS_OK);
        assert_eq!(v["decision"], "deny");
        assert_eq!(v["deny_policy_index"], 0);
        assert!(v["allow_policy_index"].is_null());
        let errors = v["errors"].as_array().expect("errors");
        assert!(errors.iter().any(|e| e["code"] == "deny_policy_matched"));
        let _ = handle;
    }

    #[test]
    fn malformed_datalog_is_a_build_error_not_a_decision() {
        let (handle, root) = generate_root("ed25519");
        let token = create(handle, vec!["right(\"a\", \"read\")"]);
        let (status, v) = authorize(
            &token,
            &root,
            vec![],
            vec![],
            vec!["allow if"],
        );
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "datalog_error");
        let (status, v) = authorize(
            &token,
            &root,
            vec!["fact("],
            vec![],
            vec!["allow if right(\"a\", \"read\");"],
        );
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "datalog_error");
        let _ = handle;
    }

    #[test]
    fn invalid_tokens_never_authorize() {
        let (handle, root) = generate_root("ed25519");
        let token = create(handle, vec!["right(\"a\", \"read\")"]);
        let mut tampered = token.clone();
        let last = tampered.len() - 1;
        tampered[last] ^= 0xFF;
        let (status, v) = authorize(
            &tampered,
            &root,
            vec![],
            vec![],
            vec!["allow if right(\"a\", \"read\");"],
        );
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "signature_error");
        let (other, _) = generate_root("ed25519");
        let wrong = {
            let (status, v) = call(
                crate::keys::OP_KEY_EXPORT_PUBLIC,
                serde_json::json!({ "handle": other }),
            );
            assert_eq!(status, STATUS_OK);
            serde_json::json!({ "algorithm": v["algorithm"], "public_key": v["public_key"] })
        };
        let (status, v) = authorize(
            &token,
            &wrong,
            vec![],
            vec![],
            vec!["allow if right(\"a\", \"read\");"],
        );
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "signature_error");
        let _ = handle;
    }

    #[test]
    fn repeated_authorization_is_deterministic() {
        let (handle, root) = generate_root("secp256r1");
        let token = create(handle, vec!["right(\"a\", \"read\")"]);
        let first = authorize(
            &token,
            &root,
            vec!["operation(\"read\")"],
            vec![],
            vec!["allow if right(\"a\", \"read\");"],
        );
        assert_eq!(first.0, STATUS_OK);
        assert_eq!(first.1["decision"], "allow");
        for _ in 0..5 {
            let again = authorize(
                &token,
                &root,
                vec!["operation(\"read\")"],
                vec![],
                vec!["allow if right(\"a\", \"read\");"],
            );
            assert_eq!(again, first);
        }
        let _ = handle;
    }

    #[test]
    fn concurrent_authorization() {
        std::thread::scope(|s| {
            for _ in 0..8 {
                s.spawn(|| {
                    let (handle, root) = generate_root("ed25519");
                    let token = create(handle, vec!["right(\"a\", \"read\")"]);
                    for _ in 0..10 {
                        let (status, v) = authorize(
                            &token,
                            &root,
                            vec!["operation(\"read\")"],
                            vec![],
                            vec!["allow if right(\"a\", \"read\");"],
                        );
                        assert_eq!(status, STATUS_OK);
                        assert_eq!(v["decision"], "allow");
                    }
                    let _ = handle;
                });
            }
        });
    }
}
