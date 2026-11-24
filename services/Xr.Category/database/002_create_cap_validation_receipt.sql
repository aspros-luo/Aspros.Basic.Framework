-- Manual business-side idempotency receipt table for the CAP validation scenario.
-- Run this once against aspros_category. The API does not create business tables.

CREATE TABLE IF NOT EXISTS category_cap_validation_receipt
(
    event_id VARCHAR(36) NOT NULL,
    run_id VARCHAR(32) NOT NULL,
    scenario VARCHAR(32) NOT NULL,
    processed_at DATETIME(6) NOT NULL,
    PRIMARY KEY (event_id),
    KEY ix_category_cap_receipt_run (run_id)
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_0900_ai_ci;
