-- The model that gave the verdict of a judged check (the name on its server, as configured when the verdict was given); NULL for a deterministic check and for older rows.
ALTER TABLE check_results ADD COLUMN judge_model text;
