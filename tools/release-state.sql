\set ON_ERROR_STOP on

SELECT
    (SELECT count(*) FROM "__EFMigrationsHistory") AS "Migrations",
    (SELECT count(*) FROM "ConversationSimulations") AS "Simulations",
    (SELECT count(*) FROM "AnalyticsEvents") AS "AnalyticsEvents",
    (SELECT count(*) FROM "ExecutionAttributions") AS "ExecutionAttributions";
