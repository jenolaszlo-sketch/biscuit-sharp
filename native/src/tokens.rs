//! Token lifecycle over verified bytes: create, parse/verify, attenuate, seal,
//! revocation identifiers, inspection.
//!
//! Every operation verifies the token against the caller-supplied root public
//! key first; the bridge never returns data derived from unverified bytes.
//! Datalog arrives as source strings parsed by upstream (`code`), with optional
//! typed parameters substituted through `code_with_params` — callers never
//! interpolate untrusted strings themselves.

use std::collections::HashMap;

use biscuit_auth::builder::Term as BuilderTerm;
use biscuit_auth::error::{Format as FormatError, Token as TokenError};
use biscuit_auth::{
    Algorithm, Biscuit, BiscuitBuilder, BlockBuilder, PublicKey, UnverifiedBiscuit,
};

use crate::{emit_error, emit_owned, BiscuitSharpBuffer, STATUS_INVALID_INPUT};

pub const OP_TOKEN_CREATE: u32 = 5;
pub const OP_TOKEN_PARSE_VERIFY: u32 = 6;
// 7 = token_serialize: reserved; managed tokens already hold canonical bytes,
// so no serialize operation is needed.
pub const OP_TOKEN_ATTENUATE: u32 = 8;
pub const OP_TOKEN_SEAL: u32 = 9;
// 10 = token_authorize: implemented in authorizer.rs.
pub const OP_TOKEN_REVOCATION_IDS: u32 = 11;
pub const OP_TOKEN_INSPECT: u32 = 12;

fn invalid(output: *mut BiscuitSharpBuffer, message: String) -> u32 {
    emit_error(output, STATUS_INVALID_INPUT, "invalid_input", &message)
}

/// Maps an upstream token failure to a stable envelope code so managed callers
/// can raise the matching typed exception. See docs/native-boundary.md.
pub(crate) fn token_error(output: *mut BiscuitSharpBuffer, e: TokenError) -> u32 {
    let (code, message) = match &e {
        TokenError::AppendOnSealed | TokenError::AlreadySealed => ("sealed_token", e.to_string()),
        TokenError::Format(f) => match f {
            FormatError::Signature(_) | FormatError::SealedSignature => {
                ("signature_error", e.to_string())
            }
            _ => ("format_error", e.to_string()),
        },
        TokenError::Language(_) => ("datalog_error", e.to_string()),
        _ => ("token_error", e.to_string()),
    };
    emit_error(output, STATUS_INVALID_INPUT, code, &message)
}

fn parse_request(input: &[u8]) -> Result<serde_json::Value, String> {
    serde_json::from_slice(input).map_err(|e| format!("invalid JSON request: {e}"))
}

fn required_u64(req: &serde_json::Value, field: &str) -> Result<u64, String> {
    req.get(field)
        .and_then(|v| v.as_u64())
        .filter(|&h| h != 0)
        .ok_or_else(|| format!("missing nonzero integer field '{field}'"))
}

fn required_str<'a>(req: &'a serde_json::Value, field: &str) -> Result<&'a str, String> {
    req.get(field)
        .and_then(|v| v.as_str())
        .ok_or_else(|| format!("missing string field '{field}'"))
}

pub(crate) fn decode_root(req: &serde_json::Value) -> Result<PublicKey, String> {
    let root = req
        .get("root")
        .ok_or_else(|| "missing object field 'root'".to_owned())?;
    let name = required_str(root, "algorithm")?;
    let algorithm = name
        .parse::<Algorithm>()
        .map_err(|e| format!("unknown root algorithm '{name}': {e}"))?;
    let b64 = required_str(root, "public_key")?;
    let bytes =
        base64::decode(b64).map_err(|e| format!("root public_key is not valid base64: {e}"))?;
    PublicKey::from_bytes(&bytes, algorithm).map_err(|e| format!("invalid root public key: {e}"))
}

pub(crate) fn decode_token(req: &serde_json::Value) -> Result<Vec<u8>, String> {
    let s = required_str(req, "token")?;
    base64::decode(s).map_err(|e| format!("token is not valid base64: {e}"))
}

/// Typed Datalog parameters: `{"name": {"type": "str"|"int"|"bool"|"bytes", "value": ...}}`.
/// Bytes values are base64. Anything else (notably floats, dates, nested
/// structures) is rejected rather than coerced.
#[derive(serde::Deserialize)]
#[serde(tag = "type", content = "value", rename_all = "lowercase")]
enum ParamValue {
    Str(String),
    Int(i64),
    Bool(bool),
    Bytes(String),
}

#[derive(serde::Deserialize)]
struct DatalogItem {
    source: String,
    #[serde(default)]
    params: HashMap<String, ParamValue>,
}

/// A managed fact/check item with its parameters decoded to upstream terms.
/// Decoding happens before any builder runs so request-shape problems stay
/// `invalid_input` while upstream parse problems stay `datalog_error`.
struct PreparedItem {
    source: String,
    terms: Option<HashMap<String, BuilderTerm>>,
}

fn prepare_items(
    req: &serde_json::Value,
    field: &str,
    output: *mut BiscuitSharpBuffer,
) -> Result<Vec<PreparedItem>, u32> {
    let items: Vec<DatalogItem> = match req.get(field) {
        None => Vec::new(),
        Some(v) => match serde_json::from_value(v.clone()) {
            Ok(items) => items,
            Err(e) => {
                return Err(invalid(
                    output,
                    format!("field '{field}' must be an array of {{source, params?}}: {e}"),
                ));
            }
        },
    };
    let mut prepared = Vec::with_capacity(items.len());
    for item in items {
        let terms = decode_terms(&item, output)?;
        prepared.push(PreparedItem {
            source: item.source,
            terms,
        });
    }
    Ok(prepared)
}

fn decode_terms(
    item: &DatalogItem,
    output: *mut BiscuitSharpBuffer,
) -> Result<Option<HashMap<String, BuilderTerm>>, u32> {
    if item.params.is_empty() {
        return Ok(None);
    }
    if !has_placeholder(&item.source) {
        return Err(invalid(
            output,
            "params supplied for a template without placeholders".to_owned(),
        ));
    }
    let mut terms = HashMap::with_capacity(item.params.len());
    for (name, value) in &item.params {
        let term = match value {
            ParamValue::Str(s) => BuilderTerm::Str(s.clone()),
            ParamValue::Int(i) => BuilderTerm::Integer(*i),
            ParamValue::Bool(b) => BuilderTerm::Bool(*b),
            ParamValue::Bytes(b64) => match base64::decode(b64) {
                Ok(bytes) => BuilderTerm::Bytes(bytes),
                Err(e) => {
                    return Err(invalid(
                        output,
                        format!("param '{name}' is not valid base64: {e}"),
                    ));
                }
            },
        };
        terms.insert(name.clone(), term);
    }
    Ok(Some(terms))
}

/// Detects `{name}` template placeholders (alphabetic start, then alphanumeric,
/// `_` or `:`). Set literals like `{1, 2}` do not match.
fn has_placeholder(source: &str) -> bool {
    let bytes = source.as_bytes();
    let mut i = 0;
    while i < bytes.len() {
        if bytes[i] == b'{' && i + 1 < bytes.len() && bytes[i + 1].is_ascii_alphabetic() {
            let mut j = i + 2;
            while j < bytes.len()
                && (bytes[j].is_ascii_alphanumeric() || bytes[j] == b'_' || bytes[j] == b':')
            {
                j += 1;
            }
            if j < bytes.len() && bytes[j] == b'}' {
                return true;
            }
            i = j;
        } else {
            i += 1;
        }
    }
    false
}

fn emit_token(output: *mut BiscuitSharpBuffer, bytes: Vec<u8>) -> u32 {
    emit_owned(
        output,
        serde_json::json!({ "token": base64::encode(bytes) })
            .to_string()
            .into_bytes(),
    )
}

fn emit_strings(output: *mut BiscuitSharpBuffer, field: &str, values: Vec<String>) -> u32 {
    emit_owned(
        output,
        serde_json::json!({ field: values })
            .to_string()
            .into_bytes(),
    )
}

/// OP_TOKEN_CREATE: `{"root_handle", "facts": [...], "rules": [...], "checks": [...]}` →
/// `{"token"}`. Facts, rules, and checks are `{source, params?}` items applied in
/// order through upstream `code`/`code_with_params`, then signed by the root key.
pub fn op_token_create(input: &[u8], output: *mut BiscuitSharpBuffer) -> u32 {
    let req = match parse_request(input) {
        Ok(v) => v,
        Err(e) => return invalid(output, e),
    };
    let handle = match required_u64(&req, "root_handle") {
        Ok(h) => h,
        Err(e) => return invalid(output, e),
    };
    let facts = match prepare_items(&req, "facts", output) {
        Ok(items) => items,
        Err(status) => return status,
    };
    let rules = match prepare_items(&req, "rules", output) {
        Ok(items) => items,
        Err(status) => return status,
    };
    let checks = match prepare_items(&req, "checks", output) {
        Ok(items) => items,
        Err(status) => return status,
    };
    crate::keys::use_keypair(output, handle, |pair| {
        let mut builder = BiscuitBuilder::new();
        for item in facts.iter().chain(rules.iter()).chain(checks.iter()) {
            builder = match &item.terms {
                None => builder.code(&item.source),
                Some(terms) => {
                    builder.code_with_params(&item.source, terms.clone(), HashMap::new())
                }
            }?;
        }
        let token = builder.build(pair)?;
        let bytes = token.to_vec()?;
        Ok(serde_json::json!({ "token": base64::encode(bytes) }))
    })
}

/// Verifies the token against the root key. All token reads and mutations go
/// through here, so the bridge never operates on unverified bytes.
fn verify_token(token: &[u8], root: PublicKey) -> Result<Biscuit, TokenError> {
    Biscuit::from(token, root)
}

fn decode_token_and_root(req: &serde_json::Value) -> Result<(Vec<u8>, PublicKey), String> {
    Ok((decode_token(req)?, decode_root(req)?))
}

/// OP_TOKEN_PARSE_VERIFY: `{"token", "root"}` → `{"token"}` with canonical
/// upstream-serialized bytes. A valid token is still not an authorized request.
pub fn op_token_parse_verify(input: &[u8], output: *mut BiscuitSharpBuffer) -> u32 {
    let req = match parse_request(input) {
        Ok(v) => v,
        Err(e) => return invalid(output, e),
    };
    let (token, root) = match decode_token_and_root(&req) {
        Ok(t) => t,
        Err(e) => return invalid(output, e),
    };
    let biscuit = match verify_token(&token, root) {
        Ok(b) => b,
        Err(e) => return token_error(output, e),
    };
    match biscuit.to_vec() {
        Ok(bytes) => emit_token(output, bytes),
        Err(e) => token_error(output, e),
    }
}

/// OP_TOKEN_ATTENUATE: `{"token", "root", "block": {source, params?}}` →
/// `{"token"}` with the block appended. Appending to a sealed token fails with
/// `sealed_token`, never silently.
pub fn op_token_attenuate(input: &[u8], output: *mut BiscuitSharpBuffer) -> u32 {
    let req = match parse_request(input) {
        Ok(v) => v,
        Err(e) => return invalid(output, e),
    };
    let block: DatalogItem = match req.get("block") {
        Some(v) => match serde_json::from_value(v.clone()) {
            Ok(item) => item,
            Err(e) => {
                return invalid(
                    output,
                    format!("field 'block' must be a {{source, params?}} item: {e}"),
                );
            }
        },
        None => return invalid(output, "missing object field 'block'".to_owned()),
    };
    let prepared = PreparedItem {
        terms: match decode_terms(&block, output) {
            Ok(t) => t,
            Err(status) => return status,
        },
        source: block.source,
    };
    let (token, root) = match decode_token_and_root(&req) {
        Ok(t) => t,
        Err(e) => return invalid(output, e),
    };
    let biscuit = match verify_token(&token, root) {
        Ok(b) => b,
        Err(e) => return token_error(output, e),
    };
    let builder = BlockBuilder::new();
    let builder = match &prepared.terms {
        None => builder.code(&prepared.source),
        Some(terms) => builder.code_with_params(&prepared.source, terms.clone(), HashMap::new()),
    };
    let builder = match builder {
        Ok(b) => b,
        Err(e) => return token_error(output, e),
    };
    match biscuit.append(builder) {
        Ok(child) => match child.to_vec() {
            Ok(bytes) => emit_token(output, bytes),
            Err(e) => token_error(output, e),
        },
        Err(e) => token_error(output, e),
    }
}

/// OP_TOKEN_SEAL: `{"token", "root"}` → `{"token"}` that cannot be further
/// attenuated. Sealing a sealed token fails with `sealed_token`.
pub fn op_token_seal(input: &[u8], output: *mut BiscuitSharpBuffer) -> u32 {
    let req = match parse_request(input) {
        Ok(v) => v,
        Err(e) => return invalid(output, e),
    };
    let (token, root) = match decode_token_and_root(&req) {
        Ok(t) => t,
        Err(e) => return invalid(output, e),
    };
    let biscuit = match verify_token(&token, root) {
        Ok(b) => b,
        Err(e) => return token_error(output, e),
    };
    match biscuit.seal() {
        Ok(sealed) => match sealed.to_vec() {
            Ok(bytes) => emit_token(output, bytes),
            Err(e) => token_error(output, e),
        },
        Err(e) => token_error(output, e),
    }
}

/// OP_TOKEN_REVOCATION_IDS: `{"token", "root"}` → `{"revocation_ids": [...]}`.
/// Identifiers are read after verification; the revocation store itself lives
/// in the consuming application.
pub fn op_token_revocation_ids(input: &[u8], output: *mut BiscuitSharpBuffer) -> u32 {
    let req = match parse_request(input) {
        Ok(v) => v,
        Err(e) => return invalid(output, e),
    };
    let (token, root) = match decode_token_and_root(&req) {
        Ok(t) => t,
        Err(e) => return invalid(output, e),
    };
    let biscuit = match verify_token(&token, root) {
        Ok(b) => b,
        Err(e) => return token_error(output, e),
    };
    emit_strings(
        output,
        "revocation_ids",
        biscuit
            .revocation_identifiers()
            .iter()
            .map(base64::encode)
            .collect(),
    )
}

/// OP_TOKEN_INSPECT: `{"token", "root"}` → structural facts plus the verified
/// root's algorithm. Sealed state is probed through upstream `seal()`: a token
/// is sealed exactly when sealing it again fails.
pub fn op_token_inspect(input: &[u8], output: *mut BiscuitSharpBuffer) -> u32 {
    let req = match parse_request(input) {
        Ok(v) => v,
        Err(e) => return invalid(output, e),
    };
    let raw = match decode_token(&req) {
        Ok(t) => t,
        Err(e) => return invalid(output, e),
    };
    // Structural reads come from the unverified token; validity is established
    // separately below, so inspection never reports on unverified bytes.
    let unverified = match UnverifiedBiscuit::from(&raw) {
        Ok(t) => t,
        Err(e) => return token_error(output, e),
    };
    let block_count = unverified.block_count();
    if block_count > 4096 {
        return invalid(
            output,
            format!("refusing to print {block_count} blocks for inspection"),
        );
    }
    let mut sources = Vec::with_capacity(block_count);
    let mut versions = Vec::with_capacity(block_count);
    for i in 0..block_count {
        match unverified.print_block_source(i) {
            Ok(s) => sources.push(s),
            Err(e) => return token_error(output, e),
        }
        match unverified.block_version(i) {
            Ok(v) => versions.push(v),
            Err(e) => return token_error(output, e),
        }
    }
    let revocation_ids: Vec<String> = unverified
        .revocation_identifiers()
        .iter()
        .map(base64::encode)
        .collect();
    let root_key_id: Option<u32> = unverified.root_key_id();
    let is_sealed = unverified.seal().is_err();
    let root = match decode_root(&req) {
        Ok(r) => r,
        Err(e) => return invalid(output, e),
    };
    if let Err(e) = unverified.verify(root) {
        return token_error(output, TokenError::Format(e));
    }
    let algorithm = root.algorithm_string().to_owned();
    let body = serde_json::json!({
        "block_count": block_count,
        "is_sealed": is_sealed,
        "root_key_id": root_key_id,
        "signature_algorithm": algorithm,
        "root_key_algorithm": algorithm,
        "revocation_ids": revocation_ids,
        "block_sources": sources,
        "block_versions": versions,
        "token_size": raw.len(),
    });
    emit_owned(output, body.to_string().into_bytes())
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::STATUS_OK;
    use biscuit_auth::KeyPair;
    use std::ptr;

    /// Calls an op with raw bytes, returning (status, optional parsed body) and
    /// freeing the native buffer exactly once like managed callers must.
    fn call_bytes(op: u32, bytes: &[u8]) -> (u32, Option<serde_json::Value>) {
        let mut out = BiscuitSharpBuffer {
            data: ptr::null_mut(),
            len: 0,
        };
        // SAFETY: input is borrowed for the call; output is a valid writable struct.
        let status =
            unsafe { crate::biscuitsharp_call_v1(op, bytes.as_ptr(), bytes.len(), &mut out) };
        let value = if out.data.is_null() || out.len == 0 {
            None
        } else {
            let slice = unsafe { std::slice::from_raw_parts(out.data as *const u8, out.len) };
            Some(serde_json::from_slice(slice).expect("output must be valid JSON"))
        };
        crate::biscuitsharp_free_v1(out);
        (status, value)
    }

    fn call(op: u32, body: serde_json::Value) -> (u32, serde_json::Value) {
        let bytes = body.to_string().into_bytes();
        let (status, value) = call_bytes(op, &bytes);
        (status, value.expect("op must emit a body"))
    }

    fn generate_root(algorithm: &str) -> u64 {
        let (status, v) = call(
            crate::keys::OP_KEY_GENERATE,
            serde_json::json!({ "algorithm": algorithm }),
        );
        assert_eq!(status, STATUS_OK, "generate root: {v}");
        v["handle"].as_u64().expect("handle")
    }

    fn destroy(handle: u64) {
        let (status, v) = call(
            crate::keys::OP_KEY_DESTROY,
            serde_json::json!({ "handle": handle }),
        );
        assert_eq!(status, STATUS_OK, "destroy: {v}");
    }

    fn root_object(handle: u64) -> serde_json::Value {
        let (status, v) = call(
            crate::keys::OP_KEY_EXPORT_PUBLIC,
            serde_json::json!({ "handle": handle }),
        );
        assert_eq!(status, STATUS_OK, "export public: {v}");
        serde_json::json!({ "algorithm": v["algorithm"], "public_key": v["public_key"] })
    }

    fn fact(source: &str) -> serde_json::Value {
        serde_json::json!({ "source": source })
    }

    fn create_with_rules(
        handle: u64,
        facts: Vec<serde_json::Value>,
        rules: Vec<serde_json::Value>,
    ) -> (u32, serde_json::Value) {
        call(
            OP_TOKEN_CREATE,
            serde_json::json!({ "root_handle": handle, "facts": facts, "rules": rules, "checks": [] }),
        )
    }

    fn create(
        handle: u64,
        facts: Vec<serde_json::Value>,
        checks: Vec<serde_json::Value>,
    ) -> Vec<u8> {
        let (status, v) = call(
            OP_TOKEN_CREATE,
            serde_json::json!({ "root_handle": handle, "facts": facts, "checks": checks }),
        );
        assert_eq!(status, STATUS_OK, "create: {v}");
        base64::decode(v["token"].as_str().expect("token")).expect("base64")
    }

    fn parse(token: &[u8], root: &serde_json::Value) -> (u32, serde_json::Value) {
        call(
            OP_TOKEN_PARSE_VERIFY,
            serde_json::json!({ "token": base64::encode(token), "root": root }),
        )
    }

    fn inspect(token: &[u8], root: &serde_json::Value) -> serde_json::Value {
        let (status, v) = call(
            OP_TOKEN_INSPECT,
            serde_json::json!({ "token": base64::encode(token), "root": root }),
        );
        assert_eq!(status, STATUS_OK, "inspect: {v}");
        v
    }

    fn attenuate(token: &[u8], root: &serde_json::Value, source: &str) -> (u32, serde_json::Value) {
        call(
            OP_TOKEN_ATTENUATE,
            serde_json::json!({
                "token": base64::encode(token),
                "root": root,
                "block": { "source": source },
            }),
        )
    }

    fn seal(token: &[u8], root: &serde_json::Value) -> (u32, serde_json::Value) {
        call(
            OP_TOKEN_SEAL,
            serde_json::json!({ "token": base64::encode(token), "root": root }),
        )
    }

    fn revocation_ids(token: &[u8], root: &serde_json::Value) -> Vec<String> {
        let (status, v) = call(
            OP_TOKEN_REVOCATION_IDS,
            serde_json::json!({ "token": base64::encode(token), "root": root }),
        );
        assert_eq!(status, STATUS_OK, "revocation ids: {v}");
        v["revocation_ids"]
            .as_array()
            .expect("array")
            .iter()
            .map(|id| id.as_str().expect("string").to_owned())
            .collect()
    }

    #[test]
    fn create_parse_roundtrip_is_canonical() {
        let h = generate_root("ed25519");
        let root = root_object(h);
        let token = create(
            h,
            vec![
                fact("right(\"workspace.main\", \"read\")"),
                fact("right(\"workspace.main\", \"write\")"),
            ],
            vec![fact("check if right(\"workspace.main\", \"read\");")],
        );
        assert!(!token.is_empty());
        let (status, v) = parse(&token, &root);
        assert_eq!(status, STATUS_OK, "parse: {v}");
        let canonical = base64::decode(v["token"].as_str().expect("token")).expect("base64");
        assert_eq!(canonical, token, "parse returns canonical bytes");
        destroy(h);
    }

    #[test]
    fn rules_derive_facts_in_authority_block() {
        let h = generate_root("ed25519");
        let root = root_object(h);
        let (status, v) = create_with_rules(
            h,
            vec![fact("role(\"admin\")")],
            vec![fact("right(\"a\", \"read\") <- role(\"admin\");")],
        );
        assert_eq!(status, STATUS_OK, "create with rule: {v}");
        let token = base64::decode(v["token"].as_str().expect("token")).expect("base64");
        assert_eq!(parse(&token, &root).0, STATUS_OK);
        let view = inspect(&token, &root);
        assert!(view["block_sources"][0]
            .as_str()
            .expect("source")
            .contains("role(\"admin\")"));
        // Malformed rules are Datalog errors, like facts and checks.
        let (status, v) = create_with_rules(h, vec![], vec![fact("right(")]);
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "datalog_error");
        destroy(h);
    }

    #[test]
    fn malformed_datalog_is_a_datalog_error() {
        let h = generate_root("ed25519");
        let (status, v) = call(
            OP_TOKEN_CREATE,
            serde_json::json!({ "root_handle": h, "facts": [fact("right(\"unclosed\"")] }),
        );
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "datalog_error");
        let (status, v) = call(
            OP_TOKEN_CREATE,
            serde_json::json!({ "root_handle": h, "checks": [fact("check if")] }),
        );
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "datalog_error");
        destroy(h);
    }

    #[test]
    fn typed_params_substitute_without_interpolation() {
        let h = generate_root("ed25519");
        let root = root_object(h);
        let (status, v) = call(
            OP_TOKEN_CREATE,
            serde_json::json!({
                "root_handle": h,
                "facts": [
                    {
                        "source": "right({resource}, {operation})",
                        "params": {
                            "resource": { "type": "str", "value": "workspace.main" },
                            "operation": { "type": "str", "value": "read" },
                        },
                    },
                    { "source": "level({n})", "params": { "n": { "type": "int", "value": 3 } } },
                    { "source": "flag({f})", "params": { "f": { "type": "bool", "value": true } } },
                    {
                        "source": "blob({b})",
                        "params": { "b": { "type": "bytes", "value": base64::encode(b"abc") } },
                    },
                ],
            }),
        );
        assert_eq!(status, STATUS_OK, "parameterized create: {v}");
        let token = base64::decode(v["token"].as_str().expect("token")).expect("base64");
        let view = inspect(&token, &root);
        let sources = view["block_sources"][0].as_str().expect("source");
        assert!(sources.contains("workspace.main"), "substituted: {sources}");
        assert!(sources.contains('3'), "int substituted: {sources}");
        destroy(h);
    }

    #[test]
    fn params_without_placeholders_are_rejected() {
        let h = generate_root("ed25519");
        // A set literal is not a placeholder: braces alone do not opt into params.
        let (status, v) = call(
            OP_TOKEN_CREATE,
            serde_json::json!({
                "root_handle": h,
                "facts": [{
                    "source": "perms(\"a\", {1, 2})",
                    "params": { "x": { "type": "str", "value": "y" } },
                }],
            }),
        );
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "invalid_input");
        destroy(h);
    }

    #[test]
    fn wrong_root_is_a_signature_error() {
        let h = generate_root("ed25519");
        let root = root_object(h);
        let token = create(h, vec![fact("right(\"a\", \"read\")")], vec![]);
        let other = generate_root("ed25519");
        let wrong = root_object(other);
        let (status, v) = parse(&token, &wrong);
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "signature_error");
        // Sanity: the right root still verifies.
        assert_eq!(parse(&token, &root).0, STATUS_OK);
        destroy(h);
        destroy(other);
    }

    #[test]
    fn tampered_truncated_garbage_tokens_fail() {
        let h = generate_root("ed25519");
        let root = root_object(h);
        let token = create(h, vec![fact("right(\"a\", \"read\")")], vec![]);

        // Flipping the final signature byte breaks the signature deterministically.
        let mut tampered = token.clone();
        let last = tampered.len() - 1;
        tampered[last] ^= 0xFF;
        let (status, v) = parse(&tampered, &root);
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "signature_error");

        // Flipping a middle byte breaks either framing or a signature.
        let mut mid = token.clone();
        let half = mid.len() / 2;
        mid[half] ^= 0xFF;
        assert_ne!(parse(&mid, &root).0, STATUS_OK);

        // Truncation breaks framing.
        let truncated = &token[..token.len() - 20];
        let (status, v) = parse(truncated, &root);
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "format_error");

        // Garbage breaks framing.
        let (status, v) = parse(b"definitely not a token", &root);
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "format_error");

        // Invalid base64 never reaches upstream.
        let (status, v) = call(
            OP_TOKEN_PARSE_VERIFY,
            serde_json::json!({ "token": "!!!", "root": root }),
        );
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "invalid_input");

        destroy(h);
    }

    #[test]
    fn unknown_root_handle_is_rejected() {
        let (status, v) = call(
            OP_TOKEN_CREATE,
            serde_json::json!({ "root_handle": u64::MAX, "facts": [fact("right(\"a\", \"read\")")] }),
        );
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "invalid_input");
    }

    #[test]
    fn attenuate_appends_a_block() {
        let h = generate_root("ed25519");
        let root = root_object(h);
        let parent = create(h, vec![fact("right(\"a\", \"read\")")], vec![]);
        let (status, v) = attenuate(&parent, &root, "check if operation(\"read\");");
        assert_eq!(status, STATUS_OK, "attenuate: {v}");
        let child = base64::decode(v["token"].as_str().expect("token")).expect("base64");
        assert_ne!(child, parent);
        assert_eq!(parse(&child, &root).0, STATUS_OK);
        let view = inspect(&child, &root);
        assert_eq!(view["block_count"], 2);
        assert!(view["block_sources"][1]
            .as_str()
            .expect("source")
            .contains("operation"));
        assert_eq!(revocation_ids(&child, &root).len(), 2);
        // Malformed attenuation source is a Datalog error on the parent, intact.
        let (status, v) = attenuate(&parent, &root, "check if");
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "datalog_error");
        assert_eq!(parse(&parent, &root).0, STATUS_OK);
        destroy(h);
    }

    #[test]
    fn seal_forbids_further_attenuation() {
        let h = generate_root("ed25519");
        let root = root_object(h);
        let parent = create(h, vec![fact("right(\"a\", \"read\")")], vec![]);
        let (status, v) = seal(&parent, &root);
        assert_eq!(status, STATUS_OK, "seal: {v}");
        let sealed = base64::decode(v["token"].as_str().expect("token")).expect("base64");
        let view = inspect(&sealed, &root);
        assert_eq!(view["is_sealed"], true);
        // Sealing flips the chain terminator; it does not append a block.
        assert_eq!(view["block_count"], 1);
        let (status, v) = attenuate(&sealed, &root, "check if operation(\"read\");");
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "sealed_token");
        let (status, v) = seal(&sealed, &root);
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "sealed_token");
        destroy(h);
    }

    #[test]
    fn p256_tokens_verify_with_secp256r1() {
        let h = generate_root("secp256r1");
        let root = root_object(h);
        assert_eq!(root["algorithm"], "secp256r1");
        let token = create(h, vec![fact("right(\"a\", \"read\")")], vec![]);
        assert_eq!(parse(&token, &root).0, STATUS_OK);
        let view = inspect(&token, &root);
        assert_eq!(view["signature_algorithm"], "secp256r1");
        assert_eq!(view["root_key_algorithm"], "secp256r1");
        destroy(h);
    }

    #[test]
    fn revocation_ids_grow_with_blocks() {
        let h = generate_root("ed25519");
        let root = root_object(h);
        let parent = create(h, vec![fact("right(\"a\", \"read\")")], vec![]);
        let parent_ids = revocation_ids(&parent, &root);
        assert_eq!(parent_ids.len(), 1);
        assert!(parent_ids
            .iter()
            .all(|id| !base64::decode(id).expect("b64").is_empty()));
        let (status, v) = attenuate(&parent, &root, "check if operation(\"read\");");
        assert_eq!(status, STATUS_OK);
        let child = base64::decode(v["token"].as_str().expect("token")).expect("base64");
        let child_ids = revocation_ids(&child, &root);
        assert_eq!(child_ids.len(), 2);
        assert!(child_ids.contains(&parent_ids[0]), "authority id persists");
        destroy(h);
    }

    #[test]
    fn inspect_reports_structural_facts() {
        let h = generate_root("ed25519");
        let root = root_object(h);
        let token = create(h, vec![fact("right(\"workspace.main\", \"read\")")], vec![]);
        let view = inspect(&token, &root);
        assert_eq!(view["block_count"], 1);
        assert_eq!(view["is_sealed"], false);
        assert_eq!(view["signature_algorithm"], "ed25519");
        assert_eq!(view["root_key_algorithm"], "ed25519");
        assert_eq!(view["token_size"], token.len());
        assert!(view["root_key_id"].is_null(), "no root key id was set");
        let sources = view["block_sources"].as_array().expect("sources");
        assert_eq!(sources.len(), 1);
        assert!(sources[0]
            .as_str()
            .expect("source")
            .contains("workspace.main"));
        let versions = view["block_versions"].as_array().expect("versions");
        assert_eq!(versions.len(), 1);
        let version = versions[0].as_u64().expect("version");
        assert!(
            (3..=6).contains(&version),
            "schema version in range: {version}"
        );
        assert_eq!(parse(&token, &root).0, STATUS_OK);
        destroy(h);
    }

    #[test]
    fn malformed_envelopes_are_rejected() {
        let (status, v) = call_bytes(OP_TOKEN_CREATE, b"{oops");
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v.expect("body")["code"], "invalid_input");

        // Invalid UTF-8 never reaches the JSON parser as text.
        let (status, v) = call_bytes(OP_TOKEN_CREATE, &[0xFF, 0xFE, 0x00]);
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v.expect("body")["code"], "invalid_input");

        // Oversized inputs are rejected before parsing.
        let big = vec![0u8; crate::MAX_INPUT_BYTES + 1];
        let (status, v) = call_bytes(OP_TOKEN_CREATE, &big);
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v.expect("body")["code"], "invalid_input");
    }

    #[test]
    fn concurrent_create_parse_inspect() {
        std::thread::scope(|s| {
            for _ in 0..8 {
                s.spawn(|| {
                    let h = generate_root("ed25519");
                    let root = root_object(h);
                    for _ in 0..10 {
                        let token = create(h, vec![fact("right(\"a\", \"read\")")], vec![]);
                        assert_eq!(parse(&token, &root).0, STATUS_OK);
                        let view = inspect(&token, &root);
                        assert_eq!(view["block_count"], 1);
                    }
                    // Handles are per-test; other threads use their own roots.
                    let _ = &root;
                    destroy(h);
                });
            }
        });
        // Cross-check one direct-upstream scenario: a token built purely with
        // upstream calls verifies through the bridge.
        let direct_root = KeyPair::new_with_algorithm("ed25519".parse().unwrap());
        let direct_token = biscuit_auth::Biscuit::builder()
            .code("right(\"direct\", \"read\")")
            .expect("code")
            .build(&direct_root)
            .expect("build")
            .to_vec()
            .expect("serialize");
        let direct_public = direct_root.public();
        let root = serde_json::json!({
            "algorithm": direct_public.algorithm_string(),
            "public_key": base64::encode(direct_public.to_bytes()),
        });
        assert_eq!(parse(&direct_token, &root).0, STATUS_OK);
    }
}
