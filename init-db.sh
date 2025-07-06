#!/bin/bash
set -e

DB_NAME="$POSTGRES_DB"
USER="$POSTGRES_USER"

echo "Initializing database $DB_NAME ..."
psql -U "$USER" -d postgres -f ./init-db.sql

echo "Initializing extensions and text search configuration in $DB_NAME..."
psql -U "$USER" -d "$DB_NAME" -f ./init-text-search.sql
