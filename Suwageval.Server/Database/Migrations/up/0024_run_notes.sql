-- A free-text note on a run: why it was started. Editable after the run finished; it changes no verdict and no regression state.
ALTER TABLE runs ADD COLUMN note text NULL;
