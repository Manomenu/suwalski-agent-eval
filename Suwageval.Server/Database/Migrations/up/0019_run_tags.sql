-- A run takes the scenarios having any of several tags (run --tag a --tag b). Empty: every scenario.
ALTER TABLE runs ADD COLUMN tags text[] NOT NULL DEFAULT '{}';
UPDATE runs SET tags = ARRAY[tag] WHERE tag IS NOT NULL;
ALTER TABLE runs DROP COLUMN tag;
