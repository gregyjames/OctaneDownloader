use reqwest::blocking::{Client, Response};
use reqwest::header::RANGE;
use std::ffi::{CStr, CString};
use std::io::Read;
use std::os::raw::{c_char, c_uchar};
use std::slice;
use lazy_static::lazy_static;
use std::time::Duration;

pub type CallDelegate = extern "C" fn(*const c_char);

lazy_static! {
    static ref CLIENT: Client = Client::builder()
        .pool_max_idle_per_host(20)  // Increase the number of idle connections kept alive for each host
        .timeout(Duration::from_secs(10))  // Set a shorter timeout for faster failure recovery
        .build()
        .expect("Failed to build client");
}

fn fetch_range(url: &str, range: &str) -> Result<Response, reqwest::Error> {
    CLIENT.get(url)
        .header(RANGE, range)
        .send()
}

fn call_callback(s: &str, callback: CallDelegate) {
    let c_string = CString::new(s).unwrap();
    callback(c_string.as_ptr());
}

#[no_mangle]
pub extern "C" fn download_partial_file(
    url: *const c_char,
    start: u64,
    end: u64,
    buffer: *mut c_uchar,
    buffer_len: usize,
    callback: CallDelegate
) -> i32 {
    let c_str = unsafe {
        assert!(!url.is_null());
        CStr::from_ptr(url)
    };
    let url_str = match c_str.to_str() {
        Ok(str) => str,
        Err(_) => return -1,
    };

    let range_header_value = format!("bytes={}-{}", start, end);

    let mut response = match fetch_range(url_str, &range_header_value) {
        Ok(resp) => resp,
        Err(_) => return -1,
    };

    if !response.status().is_success() {
        return -1;
    }

    let buffer_slice = unsafe {
        assert!(!buffer.is_null());
        slice::from_raw_parts_mut(buffer, buffer_len)
    };

    let mut total_read = 0;
    while total_read < buffer_len {
        // Adjust the buffer slice to start from where we left off
        let read_buf = &mut buffer_slice[total_read..];
        match response.read(read_buf) {
            Ok(0) => break, // EOF reached
            Ok(n) => total_read += n,
            Err(_) => return -1,
        }
    }

    call_callback(format!("start: {}, end: {}, total: {}, buffersize: {}", start, end, end-start, buffer_len).as_str(), callback);

    total_read as i32
}