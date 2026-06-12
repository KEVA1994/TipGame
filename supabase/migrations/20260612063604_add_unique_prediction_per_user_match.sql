-- One prediction per player per match; also enables ON CONFLICT upserts.
alter table "Predictions"
  add constraint "UQ_Predictions_UserId_MatchId" unique ("UserId", "MatchId");
