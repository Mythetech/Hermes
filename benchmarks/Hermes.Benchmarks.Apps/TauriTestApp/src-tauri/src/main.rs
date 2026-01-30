// Prevents additional console window on Windows in release
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

#[tauri::command]
fn benchmark_ready(time: f64) {
    println!("BENCHMARK_READY:{:.2}", time);
}

fn main() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![benchmark_ready])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
