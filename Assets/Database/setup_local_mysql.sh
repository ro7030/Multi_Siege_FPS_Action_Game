#!/usr/bin/env bash
# 로컬 MySQL 8.0.45-arm64 초기 설정 스크립트
# root 비밀번호 입력 후 DB/계정/테이블을 생성합니다.

set -euo pipefail

MYSQL_BIN="${MYSQL_BIN:-/usr/local/mysql/bin/mysql}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SQL_FILE="${SCRIPT_DIR}/setup_local_mysql.sql"

if [[ ! -x "$MYSQL_BIN" ]]; then
  echo "MySQL client not found: $MYSQL_BIN"
  exit 1
fi

if [[ ! -f "$SQL_FILE" ]]; then
  echo "SQL file not found: $SQL_FILE"
  exit 1
fi

echo "MySQL version:"
"$MYSQL_BIN" --version

echo ""
echo "Running setup script (root password required)..."
"$MYSQL_BIN" -u root -p < "$SQL_FILE"

echo ""
echo "Verifying game_dev connection..."
"$MYSQL_BIN" -u game_dev -pgame_dev -e "USE multi_siege_fps; SHOW TABLES;"

echo ""
echo "Setup complete."
echo "Connection string:"
echo "Server=127.0.0.1;Port=3306;User ID=game_dev;Password=game_dev;Database=multi_siege_fps;"
