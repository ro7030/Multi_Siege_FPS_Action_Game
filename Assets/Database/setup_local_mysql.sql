-- 로컬 MySQL 8.0 초기 설정 (root 계정으로 실행)
-- 사용법: /usr/local/mysql/bin/mysql -u root -p < Assets/Database/setup_local_mysql.sql

CREATE DATABASE IF NOT EXISTS multi_siege_fps
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

CREATE USER IF NOT EXISTS 'game_dev'@'localhost' IDENTIFIED BY 'game_dev';
GRANT ALL PRIVILEGES ON multi_siege_fps.* TO 'game_dev'@'localhost';
FLUSH PRIVILEGES;

USE multi_siege_fps;

CREATE TABLE IF NOT EXISTS session_results (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  session_id VARCHAR(64) NOT NULL,
  room_id VARCHAR(64),
  room_code VARCHAR(32),
  cleared TINYINT(1) NOT NULL DEFAULT 0,
  final_wave INT NOT NULL DEFAULT 0,
  max_wave INT NOT NULL DEFAULT 0,
  final_score INT NOT NULL DEFAULT 0,
  final_balance INT NOT NULL DEFAULT 0,
  play_seconds FLOAT NOT NULL DEFAULT 0,
  ended_at_utc DATETIME NULL,
  payload_json JSON NULL,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uq_session_id (session_id),
  KEY idx_created_at (created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS player_stats (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  session_result_id BIGINT NOT NULL,
  client_id INT NOT NULL DEFAULT 0,
  nickname VARCHAR(64),
  kills INT NOT NULL DEFAULT 0,
  harvest_count INT NOT NULL DEFAULT 0,
  repair_count INT NOT NULL DEFAULT 0,
  revive_count INT NOT NULL DEFAULT 0,
  damage_dealt FLOAT NOT NULL DEFAULT 0,
  final_score INT NOT NULL DEFAULT 0,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_session_result_id (session_result_id),
  CONSTRAINT fk_player_stats_session
    FOREIGN KEY (session_result_id) REFERENCES session_results(id)
    ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
