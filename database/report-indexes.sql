-- REV-02 / 3.1: indexes for the real report query. Safe to run repeatedly.
CREATE INDEX IF NOT EXISTS idx_transactions_wallet_date
    ON transactions (wallet_id, occurred_on);

CREATE INDEX IF NOT EXISTS idx_transactions_category_date
    ON transactions (category_id, occurred_on);

CREATE INDEX IF NOT EXISTS idx_transactions_created_by_date
    ON transactions (created_by_user_id, occurred_on);
