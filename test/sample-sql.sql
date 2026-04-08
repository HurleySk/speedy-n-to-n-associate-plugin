-- Sample SQL for Speedy N:N Associate plugin.
-- Targets the test entities created by create-test-data.ps1.
-- Returns all testwidget IDs paired with all testgadget IDs via cross-join.
-- Requires TDS endpoint enabled (Power Platform Admin Center > Settings > Features).

SELECT w.spdy_testwidgetid, g.spdy_testgadgetid
FROM spdy_testwidget w
CROSS JOIN spdy_testgadget g
WHERE w.statecode = 0
  AND g.statecode = 0
