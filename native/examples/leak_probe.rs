//! Run under Valgrind directly, without Rust's libtest event-channel lifetime.
use biscuitsharp_native::{
    biscuitsharp_call_v1, biscuitsharp_free_v1, BiscuitSharpBuffer, STATUS_OK,
};

#[path = "../tests/support/leak_workload.rs"]
mod leak_workload;

fn call(operation: u32, body: &serde_json::Value) -> (u32, serde_json::Value) {
    let input = body.to_string();
    let mut output = BiscuitSharpBuffer {
        data: std::ptr::null_mut(),
        len: 0,
    };
    // SAFETY: input and output remain valid for the duration of the call.
    let status =
        unsafe { biscuitsharp_call_v1(operation, input.as_ptr(), input.len(), &mut output) };
    let parsed = if output.data.is_null() || output.len == 0 {
        None
    } else {
        // SAFETY: the ABI returned a readable buffer owned until the matching free.
        let bytes = unsafe { std::slice::from_raw_parts(output.data, output.len) };
        Some(serde_json::from_slice(bytes))
    };
    biscuitsharp_free_v1(output);
    let value = parsed
        .expect("operation must emit a body")
        .expect("output must be JSON");
    (status, value)
}

fn main() {
    leak_workload::run(call);
    println!("LEAK_PROBE_PASSED cycles=50");
}
