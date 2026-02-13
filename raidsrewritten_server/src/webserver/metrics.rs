use axum::{
    extract::Request,
    http::StatusCode,
    middleware::Next,
    response::{IntoResponse, Response},
};
use lazy_static::lazy_static;
use prometheus::{Encoder, IntCounter, IntCounterVec, IntGauge, Opts, Registry, TextEncoder};
use tracing::error;

lazy_static! {
    pub static ref REGISTRY: Registry = Registry::new();
    pub static ref HTTP_REQUESTS_TOTAL: IntCounterVec = IntCounterVec::new(
        Opts::new("http_requests_total", "Total HTTP Requests"),
        &["endpoint", "method", "status"]
    )
    .expect("metric can be created");
    pub static ref CONNECTED_CLIENTS: IntGauge =
        IntGauge::new("connected_clients", "Connected Clients").expect("metric can be created");
    pub static ref CONNECTED_CLIENTS_TOTAL: IntCounter =
        IntCounter::new("connected_clients_total", "Total Connected Clients")
            .expect("metric can be created");
    pub static ref CONNECTED_PLAYERS: IntGauge =
        IntGauge::new("connected_players", "Connected Players").expect("metric can be created");
    pub static ref CONNECTED_PLAYERS_TOTAL: IntCounter =
        IntCounter::new("connected_players_total", "Total Connected Players")
            .expect("metric can be created");
    pub static ref MECHANICS_STARTED: IntCounterVec = IntCounterVec::new(
        Opts::new("mechanics_started", "Mechanics Started"),
        &["mechanic_id"]
    )
    .expect("metric can be created");
}

pub fn init_metrics() {
    REGISTRY
        .register(Box::new(HTTP_REQUESTS_TOTAL.clone()))
        .expect("collector can be registered");
    REGISTRY
        .register(Box::new(CONNECTED_CLIENTS.clone()))
        .expect("collector can be registered");
    REGISTRY
        .register(Box::new(CONNECTED_CLIENTS_TOTAL.clone()))
        .expect("collector can be registered");
    REGISTRY
        .register(Box::new(CONNECTED_PLAYERS.clone()))
        .expect("collector can be registered");
    REGISTRY
        .register(Box::new(CONNECTED_PLAYERS_TOTAL.clone()))
        .expect("collector can be registered");
    REGISTRY
        .register(Box::new(MECHANICS_STARTED.clone()))
        .expect("collector can be registered");
}

// https://oneuptime.com/blog/post/2026-01-07-rust-prometheus-custom-metrics/view
pub async fn metrics_middleware(request: Request, next: Next) -> Response {
    // Get request details
    let method = request.method().to_string();
    let path = request.uri().path().to_string();

    // Process request
    let response = next.run(request).await;

    // Record metrics
    let status = response.status().as_u16();

    HTTP_REQUESTS_TOTAL
        .with_label_values(&[path, method, status.to_string()])
        .inc();

    response
}

pub async fn get_metrics() -> Response {
    // Collect default process metrics
    let process_metrics = prometheus::gather();

    // Collect custom metrics
    let mut all_metrics = REGISTRY.gather();
    all_metrics.extend(process_metrics);

    // Encode metrics in Peometheus text format
    let encoder = TextEncoder::new();
    let mut buffer = Vec::new();

    match encoder.encode(&all_metrics, &mut buffer) {
        Ok(_) => {
            // Return metrics with correct content type
            (
                StatusCode::OK,
                [("content-type", "text/plain; version=0.0.4; charset=utf-8")],
                buffer,
            )
                .into_response()
        }
        Err(e) => {
            error!(error = %e, "Failed to encode metrics");
            StatusCode::INTERNAL_SERVER_ERROR.into_response()
        }
    }
}
