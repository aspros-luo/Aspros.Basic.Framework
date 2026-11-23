-- Manual schema for the Xr.Category Framework integration scenario.
-- Run this script explicitly against the aspros_category database.
-- The API does not create or migrate tables at startup.

CREATE TABLE IF NOT EXISTS category
(
    id BIGINT NOT NULL AUTO_INCREMENT,
    code VARCHAR(64) NOT NULL,
    name VARCHAR(128) NOT NULL,
    parent_id BIGINT NULL,
    sort_order INT NOT NULL DEFAULT 0,
    is_enabled TINYINT(1) NOT NULL DEFAULT 1,
    creator_id BIGINT NOT NULL DEFAULT 0,
    create_time DATETIME(6) NOT NULL,
    updater_id BIGINT NOT NULL DEFAULT 0,
    update_time DATETIME(6) NOT NULL,
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    PRIMARY KEY (id),
    UNIQUE KEY ux_category_code (code),
    KEY ix_category_parent_sort (parent_id, sort_order),
    CONSTRAINT fk_category_parent
        FOREIGN KEY (parent_id) REFERENCES category (id)
        ON DELETE RESTRICT
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_0900_ai_ci;
