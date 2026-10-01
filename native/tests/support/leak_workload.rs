//! Shared 50-cycle workload for the unit test and standalone Valgrind probe.
//! Operation numbers are the ABI 1 wire contract; calls cross the public C ABI.
use super::STATUS_OK;
pub fn run(call: impl Fn(u32, &serde_json::Value) -> (u32, serde_json::Value)) {
    println!("LEAK_PROBE_WORKLOAD_STARTED cycles=50");
    let (status, v) = call(
        1, /* OP_KEY_GENERATE */
        &serde_json::json!({ "algorithm": "ed25519" }),
    );
    assert_eq!(status, STATUS_OK);
    let handle = v["handle"].as_u64().unwrap();
    let root = serde_json::json!({
        "algorithm": v["algorithm"],
        "public_key": v["public_key"],
    });
    for _ in 0..50 {
        let (status, v) = call(
            5, /* OP_TOKEN_CREATE */
            &serde_json::json!({
                "root_handle": handle,
                "facts": [{ "source": "right(\"workspace.main\", \"read\")" }],
            }),
        );
        assert_eq!(status, STATUS_OK);
        let token = v["token"].as_str().unwrap().to_owned();
        let token_bytes = base64::decode(&token).unwrap();
        let req = || {
            serde_json::json!({
                "token": base64::encode(&token_bytes),
                "root": root,
            })
        };
        let (status, _) = call(6 /* OP_TOKEN_PARSE_VERIFY */, &req());
        assert_eq!(status, STATUS_OK);
        let (status, v) = call(
            8, /* OP_TOKEN_ATTENUATE */
            &serde_json::json!({
                "token": base64::encode(&token_bytes),
                "root": root,
                "block": { "source": "check if operation(\"read\");" },
            }),
        );
        assert_eq!(status, STATUS_OK);
        let child = v["token"].as_str().unwrap().to_owned();
        let (status, v) = call(
            10, /* OP_TOKEN_AUTHORIZE */
            &serde_json::json!({
                "token": child,
                "root": root,
                "facts": ["operation(\"read\")"],
                "policies": ["allow if right(\"workspace.main\", \"read\");"],
            }),
        );
        assert_eq!(status, STATUS_OK);
        assert_eq!(v["decision"], "allow");
        let (status, _) = call(12 /* OP_TOKEN_INSPECT */, &req());
        assert_eq!(status, STATUS_OK);
        let (status, _) = call(11 /* OP_TOKEN_REVOCATION_IDS */, &req());
        assert_eq!(status, STATUS_OK);
    }
    let (status, _) = call(
        13, /* OP_KEY_DESTROY */
        &serde_json::json!({ "handle": handle }),
    );
    assert_eq!(status, STATUS_OK);
    println!("LEAK_PROBE_WORKLOAD_COMPLETED cycles=50");
}
