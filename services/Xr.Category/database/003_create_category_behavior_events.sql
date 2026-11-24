CREATE DATABASE IF NOT EXISTS aspros_analytics;

CREATE TABLE IF NOT EXISTS aspros_analytics.category_behavior_events
(
    event_id String,
    run_id String,
    user_id String,
    category_id UInt64,
    event_type LowCardinality(String),
    event_time DateTime64(3, 'UTC')
)
ENGINE = MergeTree
PARTITION BY toYYYYMM(event_time)
ORDER BY (user_id, event_time, event_id);
