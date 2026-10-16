-- A judge check whose model could not be asked (or answered unreadably) is "not judged": stored as failed, told apart by this flag.
ALTER TABLE check_results ADD COLUMN not_judged boolean NOT NULL DEFAULT false;
